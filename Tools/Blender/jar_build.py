# Crée le fichier Blender du boss « la Jarre aux âmes » : Assets/Models/Jar/jar.blend (+ texture jar_pottery.png).
#   blender -b --factory-startup --python Tools/Blender/jar_build.py -- Assets/Models/Jar/jar.blend [--force]
# À ne lancer qu'UNE fois : ensuite, le fichier se modifie à la main dans Blender. Sans --force, le script refuse
# d'écraser un jar.blend existant ; avec --force, l'ancien est d'abord copié dans References/Backups/.
# La texture n'est refaite que si elle n'existe pas (ou avec --force) : on peut la repeindre librement.
#
# Le boss : un vieux bol grec en terre cuite, resté des siècles sous l'eau, rempli de chair ; deux longs bras
# humains maigres, enracinés dans la chair, le traînent au sol.
# Style : low-poly à facettes. Repère : avant (vers le joueur) = +X, haut = +Z, côté caméra = -Y.
# Origine = centre du bol ; le sol (Ground, simple repère) est sous le pied.
#
# Objets (noms lus par Unity, à garder) :
#   Bowl              le bol entier (surface extérieure + modificateur Solidify pour l'épaisseur), UV = texture.
#                     À MODELER LIBREMENT ; puis relancer jar_fracture.py (texte inclus dans le .blend).
#   Fragments/Frag_P<n>_<k>  morceaux générés depuis Bowl par jar_fracture.py (ne pas retoucher à la main)
#   Handle_Front/Back anses (le jeu les accroche au morceau le plus proche)
#   FleshCore         la chair qui remplit le bol
#   Arm_Front / Arm_Back  bras (armature UpperArm, Forearm, Hand), maillage ArmMesh_* en enfant
#   Ground            repère du sol (ignoré par le jeu)
import math
import os
import random
import shutil
import sys
import time

import bmesh
import bpy
import numpy as np
from mathutils import Vector, noise

SEED = 7
SEGMENTS = 18                      # côtés du bol (facettes visibles mais pas grossières)
WALL = 0.12                        # épaisseur de la paroi (modificateur Solidify de Bowl)

# Couleurs (sRGB)
CLAY_RAW = (0.74, 0.45, 0.28)      # terre cuite à cru (tranches, intérieur)
FLESH = (0.58, 0.47, 0.50)         # chair grise, mauve
FLESH_DARK = (0.30, 0.20, 0.25)
FLESH_RAW = (0.66, 0.26, 0.28)     # chair à vif
SKIN = (0.70, 0.66, 0.66)          # peau des bras : grise, exsangue
SKIN_DARK = (0.42, 0.38, 0.42)
SKIN_RAW = (0.66, 0.38, 0.40)      # coudes, jointures
GROUND = (0.16, 0.18, 0.24)

# Profil extérieur du bol : (rayon, hauteur) du pied au bord roulé.
PROFILE = [
    (0.34, -0.95), (0.42, -0.90), (0.40, -0.80), (0.62, -0.70), (0.86, -0.48), (0.98, -0.20), (1.00, 0.05),
    (0.96, 0.28), (0.88, 0.44), (0.82, 0.52), (0.84, 0.58), (0.94, 0.63), (0.92, 0.72), (0.82, 0.73),
]
LIP_RINGS = (10, 11, 12, 13)       # anneaux du bord (ébréchés)
GROUND_Z = -0.95
NECK_R = 0.82                      # rayon du col (le bord roulé dépasse : la chair reste dedans)

# Texture : u = tour du bol (couture à l'arrière, +Y), v = hauteur de Z_BOTTOM à Z_TOP
TEX_W, TEX_H = 2048, 1024
Z_BOTTOM, Z_TOP = -0.95, 0.74
SEAM = math.pi / 2

rng = random.Random(SEED)
MATERIALS = {}


# ---------------------------------------------------------------- outils


def srgb(c, k=1.0):
    return (min(1.0, c[0] * k), min(1.0, c[1] * k), min(1.0, c[2] * k), 1.0)


def mix(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def bumpy(p, scale, seed):
    return noise.noise(Vector((p[0] * scale + seed, p[1] * scale - seed * 0.7, p[2] * scale + seed * 1.3)))


def make_object(name, bm, material_names, face_colors, parent=None, location=(0, 0, 0), uv_fn=None):
    """Crée l'objet depuis le bmesh : facettes plates, une couleur par face ; UV (u, v) par sommet si uv_fn."""
    bm.faces.ensure_lookup_table()
    if uv_fn is not None:
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            coords = [uv_fn(l.vert.co) for l in f.loops]
            us = [c[0] for c in coords]
            if max(us) - min(us) > 0.5:                     # face à cheval sur la couture : pas d'étirement
                coords = [(c[0] + 1.0 if c[0] < 0.5 else c[0], c[1]) for c in coords]
            for l, c in zip(f.loops, coords):
                l[uv].uv = c
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    attr = mesh.color_attributes.new(name="Col", type='BYTE_COLOR', domain='CORNER')
    for poly in mesh.polygons:
        c = face_colors[poly.index]
        for li in poly.loop_indices:
            attr.data[li].color_srgb = c
        poly.use_smooth = False
    for m in material_names:
        mesh.materials.append(MATERIALS[m])
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    if parent is not None:
        obj.parent = parent
        obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj


def triangulate(bm, faces=None):
    bmesh.ops.triangulate(bm, faces=faces if faces is not None else bm.faces[:], quad_method='BEAUTY',
                          ngon_method='BEAUTY')


def bowl_uv(co):
    theta = math.atan2(co.y, co.x)
    u = ((theta - SEAM) / (2 * math.pi)) % 1.0
    v = (co.z - Z_BOTTOM) / (Z_TOP - Z_BOTTOM)
    return (u, min(max(v, 0.0), 1.0))


def make_materials(texture):
    mats = {}

    def material(name, rough, image=None, color=None):
        m = bpy.data.materials.new(name)
        try:
            m.use_nodes = True
        except Exception:
            pass
        nt = m.node_tree
        bsdf = nt.nodes.get("Principled BSDF")
        bsdf.inputs["Roughness"].default_value = rough
        if color is not None:
            bsdf.inputs["Base Color"].default_value = color
        else:
            col = nt.nodes.new("ShaderNodeVertexColor")
            col.layer_name = "Col"
            if image is None:
                nt.links.new(col.outputs["Color"], bsdf.inputs["Base Color"])
            else:
                tex = nt.nodes.new("ShaderNodeTexImage")
                tex.image = image
                mul = nt.nodes.new("ShaderNodeMix")
                mul.data_type = 'RGBA'
                mul.blend_type = 'MULTIPLY'
                mul.inputs["Factor"].default_value = 1.0
                nt.links.new(tex.outputs["Color"], mul.inputs["A"])
                nt.links.new(col.outputs["Color"], mul.inputs["B"])
                nt.links.new(mul.outputs["Result"], bsdf.inputs["Base Color"])
        mats[name] = m

    material("Pottery", 0.75, image=texture)                    # texture × couleur des sommets (nuance par face)
    lin = tuple(c ** 2.2 for c in CLAY_RAW) + (1.0,)
    material("PotteryRaw", 0.9, color=lin)                      # terre cuite à cru : tranches et intérieur
    material("Flesh", 0.7)
    material("Ground", 0.9)
    return mats


# ---------------------------------------------------------------- texture : poterie grecque restée sous l'eau


def make_texture(path):
    """Terre cuite à vernis noir (pied, rayons, vagues, méandres, languettes, col), vieillie par des siècles
    sous l'eau : vernis délavé et écaillé, croûtes calcaires, traînées d'algues, coquillages incrustés.
    Motifs calculés en unités du monde (pas déformés) et raccordés à la couture."""
    r = np.random.default_rng(SEED)
    radius = 0.95
    circ = 2 * math.pi * radius
    height = Z_TOP - Z_BOTTOM
    u = (np.arange(TEX_W) + 0.5) / TEX_W
    v = (np.arange(TEX_H) + 0.5) / TEX_H
    U, V = np.meshgrid(u, v)                                     # ligne 0 = bas du bol
    X = U * circ
    Z = Z_BOTTOM + V * height

    def vnoise(scale, seed):
        g = np.random.default_rng(seed)
        nx = max(1, round(circ / scale))
        ny = int(math.ceil(height / scale)) + 2
        grid = g.random((ny, nx))
        fx = U * nx
        fy = (Z - Z_BOTTOM) / scale
        x0 = np.floor(fx).astype(int)
        y0 = np.floor(fy).astype(int)
        tx = fx - x0
        ty = fy - y0
        tx = tx * tx * (3 - 2 * tx)
        ty = ty * ty * (3 - 2 * ty)
        x1 = (x0 + 1) % nx
        x0 %= nx
        y1 = np.minimum(y0 + 1, ny - 1)
        a = grid[y0, x0] * (1 - tx) + grid[y0, x1] * tx
        b = grid[y1, x0] * (1 - tx) + grid[y1, x1] * tx
        return a * (1 - ty) + b * ty

    def fbm(scale, seed, octaves=4):
        total = np.zeros_like(U)
        amp, norm = 1.0, 0.0
        for k in range(octaves):
            total += vnoise(scale / (2 ** k), seed + k * 17) * amp
            norm += amp
            amp *= 0.5
        return total / norm

    def band(z0, z1):
        return (Z >= z0) & (Z < z1)

    def repeats(cell):
        return max(1, round(circ / cell))

    terracotta = np.array((0.70, 0.40, 0.23))
    glaze = np.array((0.07, 0.065, 0.06))
    black = np.zeros_like(U, dtype=bool)

    # Pied et col : vernis noir
    black |= Z < -0.80
    black |= Z > 0.50
    # Rayons au pied : triangles noirs pointés vers le haut
    n = repeats(0.16)
    fx = (U * n) % 1.0
    t = (Z - (-0.80)) / 0.28
    black |= band(-0.80, -0.52) & (np.abs(fx - 0.5) * 2 < (1 - t) * 0.9)
    # Filets
    for z0, z1 in ((-0.50, -0.475), (-0.43, -0.415), (-0.19, -0.175), (0.235, 0.25), (0.27, 0.28), (0.47, 0.50)):
        black |= band(z0, z1)
    # Vagues (spirales courantes stylisées)
    n = repeats(0.2)
    fx = (U * n) % 1.0
    wz = (Z - (-0.40)) / 0.18
    crest = 0.5 + 0.38 * np.sin(fx * 2 * math.pi) + 0.12 * np.sin(fx * 4 * math.pi + 1.0)
    black |= band(-0.40, -0.22) & (wz < crest)
    # Méandre (grecque) : motif de 10 × 10 cases
    key = [
        "1111111111",
        "1000000001",
        "1011111101",
        "1010000101",
        "1010110101",
        "1010010101",
        "1011110101",
        "1000000101",
        "1111111101",
        "0000000001",
    ]
    keyarr = np.array([[c == "1" for c in row] for row in reversed(key)])
    cell = 0.2
    n = repeats(cell)
    ix = np.clip(((U * n) % 1.0 * 10).astype(int), 0, 9)
    iz = np.clip(((Z - 0.02) / cell * 10).astype(int), 0, 9)
    black |= band(0.02, 0.22) & keyarr[iz, ix]
    # Languettes sous le col : arcs alternés
    n = repeats(0.12)
    fx = (U * n) % 1.0
    k = np.floor(U * n).astype(int) % 2
    tz = (0.46 - Z) / 0.15
    tongue = ((fx - 0.5) / 0.42) ** 2 + tz ** 2 < 1.0
    black |= band(0.31, 0.46) & tongue & (k == 0)

    rgb = np.where(black[..., None], glaze, terracotta)
    # Terre cuite inégale
    rgb *= (0.88 + 0.24 * fbm(0.3, 3))[..., None]

    # --- Usure par l'eau ---
    # Vernis délavé, gris-vert ; écaillé par plaques (la terre cuite réapparaît)
    worn = fbm(0.25, 11)
    faded = np.array((0.20, 0.23, 0.21))
    rgb = np.where(black[..., None], rgb * 0.4 + faded * 0.6 * (0.7 + 0.6 * worn[..., None]), rgb)
    flake = (fbm(0.08, 13) > 0.68) & black
    rgb[flake] = (terracotta * 0.85)
    # Terre cuite ternie, un peu verdie
    rgb = np.where(black[..., None], rgb, rgb * 0.75 + np.array((0.40, 0.40, 0.33)) * 0.25)
    # Traînées d'algues (allongées vers le bas)
    algae_n = vnoise(0.05, 21) * 0.4 + fbm(0.35, 23) * 0.6
    streak = np.clip((algae_n - 0.55) * 4.0, 0, 1) * np.clip(0.4 + 0.6 * (1 - V), 0, 1)
    rgb = rgb * (1 - streak[..., None] * 0.7) + np.array((0.24, 0.30, 0.17)) * streak[..., None] * 0.7
    # Croûtes calcaires : plaques blanchâtres, plus nombreuses en bas et d'un côté
    side = 0.5 + 0.5 * np.cos((U - 0.65) * 2 * math.pi)
    crust_n = fbm(0.22, 31, octaves=5) + 0.12 * side + 0.1 * (1 - V)
    crust = np.clip((crust_n - 0.70) * 14.0, 0, 1)                 # plaques plus rares, bords nets
    speck = 0.85 + 0.3 * vnoise(0.012, 33)
    rgb = rgb * (1 - crust[..., None]) + (np.array((0.74, 0.73, 0.66)) * speck[..., None]) * crust[..., None]
    # Coquillages incrustés (balanes) : anneaux clairs à cœur sombre, surtout dans les croûtes
    px_per_unit_u = TEX_W / circ
    px_per_unit_v = TEX_H / height
    # Colonies : quelques foyers dans les croûtes, des coquillages serrés autour, de tailles variées
    crust_px = np.argwhere(crust > 0.5)
    spots = []
    for _ in range(min(18, len(crust_px))):
        cy, cx = crust_px[r.integers(len(crust_px))]
        for _ in range(r.integers(4, 12)):
            spots.append(((cx / TEX_W + r.normal(0, 0.012)) % 1.0, min(max(cy / TEX_H + r.normal(0, 0.03), 0.0), 0.999)))
    for cu, cv in spots:
        iu, iv = int(cu * TEX_W), int(cv * TEX_H)
        rad = r.uniform(0.008, 0.028) * (1.0 if r.random() > 0.2 else 1.6)
        ru = int(rad * px_per_unit_u) + 1
        rv = int(rad * px_per_unit_v) + 1
        ys = np.arange(max(0, iv - rv), min(TEX_H, iv + rv + 1))
        xs = np.arange(iu - ru, iu + ru + 1)
        yy, xx = np.meshgrid(ys, xs, indexing='ij')
        d = np.sqrt(((xx - iu) / ru) ** 2 + ((yy - iv) / rv) ** 2)
        xx %= TEX_W
        shade = r.uniform(0.85, 1.05)
        ring = (d < 1.0) & (d > 0.45)
        core = d <= 0.45
        rgb[yy[ring], xx[ring]] = np.array((0.86, 0.84, 0.78)) * shade
        rgb[yy[core], xx[core]] = np.array((0.22, 0.22, 0.20))

    rgba = np.concatenate([np.clip(rgb, 0, 1), np.ones_like(U)[..., None]], axis=2).astype(np.float32)
    img = bpy.data.images.new("jar_pottery", width=TEX_W, height=TEX_H, alpha=False)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


# ---------------------------------------------------------------- bol


def profile_radius(z):
    """Rayon du profil extérieur à la hauteur z (interpolation, partie montante du profil)."""
    outer = PROFILE[:13]
    if z <= outer[0][1]:
        return outer[0][0]
    for (r0, z0), (r1, z1) in zip(outer, outer[1:]):
        if z0 <= z <= z1:
            t = (z - z0) / max(z1 - z0, 1e-6)
            return r0 + (r1 - r0) * t
    return outer[-1][0]


def surface_point(theta, r, z):
    """Point de la surface déformée par un bruit continu (pas de cercle parfait)."""
    base = Vector((math.cos(theta) * r, math.sin(theta) * r, z))
    r *= 1.0 + 0.045 * bumpy(base, 1.6, 3.1) + 0.015 * bumpy(base, 5.0, 8.4)
    return Vector((math.cos(theta) * r, math.sin(theta) * r, z))


def build_bowl():
    """Surface extérieure du bol, triangulée ; l'épaisseur vient du modificateur Solidify."""
    bm = bmesh.new()
    chips = [rng.uniform(0, 2 * math.pi) for _ in range(3)]
    rings = []
    for i, (r, z) in enumerate(PROFILE):
        ring = []
        for s in range(SEGMENTS):
            theta = (s + (0.5 if i % 2 else 0.0)) / SEGMENTS * 2 * math.pi   # anneaux décalés : facettes en losanges
            p = surface_point(theta, r, z)
            if i in LIP_RINGS:
                for c in chips:
                    d = abs((theta - c + math.pi) % (2 * math.pi) - math.pi)
                    if d < 0.3:
                        p.z -= 0.08 * (1 - d / 0.3)                      # bord ébréché
            ring.append(bm.verts.new(p))
        rings.append(ring)
    bottom = bm.verts.new((0, 0, PROFILE[0][1]))
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for s in range(SEGMENTS):
            s1 = (s + 1) % SEGMENTS
            if i % 2 == 0:
                bm.faces.new((a[s], a[s1], b[s]))
                bm.faces.new((a[s1], b[s1], b[s]))
            else:
                bm.faces.new((a[s], a[s1], b[s1]))
                bm.faces.new((a[s], b[s1], b[s]))
    for s in range(SEGMENTS):
        bm.faces.new((rings[0][(s + 1) % SEGMENTS], rings[0][s], bottom))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.faces.ensure_lookup_table()
    f = max(bm.faces, key=lambda f: f.calc_center_median().x)
    if f.normal.x < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    triangulate(bm)
    colors = [srgb((1, 1, 1), 1.0 + rng.uniform(-0.06, 0.0)) for _ in bm.faces]   # nuance par facette
    return bm, colors


# ---------------------------------------------------------------- tubes (anses, bras, doigts)


def bezier(p0, p1, p2, p3, t):
    u = 1 - t
    return p0 * u ** 3 + p1 * 3 * u * u * t + p2 * 3 * u * t * t + p3 * t ** 3


def tube(bm, points, radii, sides=6, flat=1.0, up=Vector((0, 1, 0)), seed=0.0, bump=0.0, cap_end=True):
    """Tube le long d'une polyligne. 'flat' < 1 : section aplatie (anse en ruban)."""
    rings = []
    n = len(points)
    for i, p in enumerate(points):
        a = points[min(i + 1, n - 1)] - points[max(i - 1, 0)]
        t = a.normalized()
        b = up - t * up.dot(t)
        if b.length < 1e-4:
            b = Vector((1, 0, 0)) - t * t.x
        b.normalize()
        nn = t.cross(b)
        ring = []
        for s in range(sides):
            ang = (s + (0.5 if i % 2 else 0.0)) / sides * 2 * math.pi
            rr = radii[i] * (1.0 + bump * bumpy(p + Vector((math.cos(ang), math.sin(ang), 0)) * 0.1, 7.0, seed))
            off = nn * math.cos(ang) * rr + b * math.sin(ang) * rr * flat
            ring.append(bm.verts.new(p + off))
        rings.append(ring)
    for i in range(n - 1):
        a, c = rings[i], rings[i + 1]
        for s in range(sides):
            s1 = (s + 1) % sides
            bm.faces.new((a[s], a[s1], c[s1], c[s]))
    if cap_end:
        tip = bm.verts.new(points[-1] + (points[-1] - points[-2]).normalized() * radii[-1] * 0.8)
        for s in range(sides):
            bm.faces.new((rings[-1][s], rings[-1][(s + 1) % sides], tip))
    return rings


def build_handle(front):
    """Anse en anneau sur le flanc, sous le bord. Renvoie aussi son point d'attache."""
    sx = 1.0 if front else -1.0
    p0 = Vector((0.94 * sx, 0, 0.22))
    p1 = Vector((1.38 * sx, 0, 0.12))
    p2 = Vector((1.42 * sx, 0, 0.62))
    p3 = Vector((0.88 * sx, 0, 0.50))
    n = 14
    pts = [bezier(p0, p1, p2, p3, i / n) for i in range(n + 1)]
    radii = [0.11 * (1.0 - 0.45 * math.sin(i / n * math.pi)) for i in range(n + 1)]
    bm = bmesh.new()
    tube(bm, pts, radii, sides=6, flat=0.6, seed=4.0 + sx, bump=0.08, cap_end=False)
    triangulate(bm)
    colors = [srgb((1, 1, 1), 1.0 + rng.uniform(-0.06, 0.0)) for _ in bm.faces]   # nuance par facette
    return bm, colors, (p0 + p3) * 0.5


# ---------------------------------------------------------------- bras humains


def sagging(a, b, sag, n):
    """Points de a à b, légèrement bombés de 'sag' (un os n'est jamais une règle)."""
    a, b = Vector(a), Vector(b)
    mid = (a + b) * 0.5 + Vector(sag)
    return [bezier(a, a + (mid - a) * 0.66, b + (mid - b) * 0.66, b, i / n) for i in range(n + 1)]


def bone_profile(t, r_start, r_end, knob_start, knob_end, belly=0.0):
    """Rayon le long d'un os : renflé aux articulations, maigre au milieu."""
    r = r_start + (r_end - r_start) * t
    r *= 1.0 + belly * math.sin(t * math.pi)
    r *= 1.0 + knob_start * math.exp(-(t / 0.08) ** 2) + knob_end * math.exp(-((1 - t) / 0.08) ** 2)
    return r


def build_hand(bm, wrist, direction, palm_side, scale, curl_axis, curl, seed):
    """Main humaine maigre : paume plate, 4 longs doigts osseux à jointures, pouce sur le côté."""
    d = direction.normalized()
    w = palm_side.normalized()
    t = w.cross(d).normalized()                 # épaisseur de la paume
    palm_len, palm_wid, palm_thk = 0.26 * scale, 0.18 * scale, 0.06 * scale
    palm_c = wrist + d * palm_len * 0.5
    made = bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    for v in made["verts"]:
        l = v.co.copy()
        widen = 0.8 + 0.25 * (l.z + 1) * 0.5    # paume en trapèze : plus large côté doigts
        v.co = palm_c + d * l.z * palm_len * 0.55 + w * l.x * palm_wid * 0.5 * widen + t * l.y * palm_thk * 0.5
    fingers = [(-0.75, 1.0), (-0.25, 1.08), (0.25, 1.02), (0.75, 0.85)]   # position sur la largeur, longueur relative
    for i, (pos, rel) in enumerate(fingers):
        base = palm_c + d * palm_len * 0.48 + w * pos * palm_wid * 0.45
        length = 0.32 * scale * rel
        pts = [base]
        dd = (d + w * pos * 0.12).normalized()
        p = base.copy()
        for k in range(3):                      # 3 phalanges
            seg = length * (0.42, 0.33, 0.25)[k]
            pts.append(p + dd * seg * 0.5)
            p = p + dd * seg
            pts.append(p.copy())
            dd = (dd + curl_axis * curl * (0.7 + 0.3 * k)).normalized()
        radii = []
        for k in range(len(pts)):
            rr = 0.024 * scale * (1 - 0.35 * k / (len(pts) - 1))
            if k in (0, 2, 4):
                rr *= 1.25                      # jointures saillantes
            radii.append(rr)
        tube(bm, pts, radii, sides=5, seed=seed + i, bump=0.03)
    # Pouce : part du bas de la paume, sur le côté
    base = wrist + d * palm_len * 0.25 - w * palm_wid * 0.5
    dd = (d * 0.6 - w * 0.8 + t * 0.2).normalized()
    pts = [base]
    p = base.copy()
    for k in range(2):
        seg = 0.12 * scale * (1.0 if k == 0 else 0.8)
        pts.append(p + dd * seg * 0.5)
        p = p + dd * seg
        pts.append(p.copy())
        dd = (dd + d * 0.5 + curl_axis * curl * 0.5).normalized()
    tube(bm, pts, [0.03 * scale * (1 - 0.3 * k / 4) * (1.2 if k in (0, 2) else 1.0) for k in range(5)],
         sides=5, seed=seed + 9, bump=0.03)
    return palm_c + d * palm_len * 0.5


def build_arm(shoulder, elbow, wrist, hand_dir, scale=1.0, seed=1.0, curl=0.25, palm_side=None,
              upper_sag=(0, 0, 0.06), fore_sag=(0, 0, 0.0)):
    """Bras humain très long et maigre : épaule, bras, coude saillant, avant-bras fin, poignet étroit, main."""
    bm = bmesh.new()
    upper = sagging(shoulder, elbow, upper_sag, 10)
    fore = sagging(elbow, wrist, fore_sag, 12)
    pts = upper + fore[1:]
    radii = []
    for i in range(len(upper)):
        radii.append(bone_profile(i / (len(upper) - 1), 0.11 * scale, 0.08 * scale, 0.1, 0.35, belly=0.12))
    for i in range(1, len(fore)):
        radii.append(bone_profile(i / (len(fore) - 1), 0.082 * scale, 0.05 * scale, 0.3, 0.15, belly=0.1))
    tube(bm, pts, radii, sides=7, seed=seed, bump=0.05, cap_end=False)
    hand_start = len(bm.verts)

    wrist_v = Vector(wrist)
    d = Vector(hand_dir).normalized()
    if palm_side is None:
        side = d.cross(Vector((0, 1, 0)))
        if side.length < 1e-3:
            side = Vector((1, 0, 0))
    else:
        side = Vector(palm_side)
    side.normalize()
    curl_axis = side.cross(d).normalized()
    hand_tip = build_hand(bm, wrist_v, d, side, scale, curl_axis, curl, seed)
    triangulate(bm)

    colors = []
    for f in bm.faces:
        c = f.calc_center_median()
        col = mix(SKIN_DARK, SKIN, 0.55 + 0.45 * bumpy(c, 5.0, seed))
        if (c - Vector(elbow)).length < 0.09 * scale:
            col = mix(col, SKIN_RAW, 0.6)                       # coude rougi
        if bumpy(c, 13.0, seed + 3) > 0.55:
            col = mix(col, SKIN_RAW, 0.5)
        colors.append(srgb(col))
    bones = [("UpperArm", Vector(shoulder), Vector(elbow)), ("Forearm", Vector(elbow), wrist_v)]
    return bm, colors, bones, hand_tip, hand_start


def make_arm(name, shoulder, elbow, wrist, hand_dir, **kw):
    bm, colors, bones, hand_tip, hand_start = build_arm(shoulder, elbow, wrist, hand_dir, **kw)
    coords = [v.co.copy() for v in bm.verts]
    mesh_obj = make_object("ArmMesh_" + name.split("_", 1)[1], bm, ["Flesh"], colors)

    arm_data = bpy.data.armatures.new(name)
    arm_obj = bpy.data.objects.new(name, arm_data)
    bpy.context.scene.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='EDIT')
    prev = None
    names = []
    for bone_name, head, tail in bones:
        b = arm_data.edit_bones.new(bone_name)
        b.head = head
        b.tail = tail
        if prev is not None:
            b.parent = prev
            b.use_connect = True
        prev = b
        names.append(bone_name)
    hand = arm_data.edit_bones.new("Hand")
    hand.head = bones[-1][2]
    hand.tail = hand_tip
    hand.parent = prev
    bpy.ops.object.mode_set(mode='OBJECT')

    # Poids : chaque sommet suit les deux os les plus proches (pli du coude) ; la main suit l'os Hand
    groups = {n: mesh_obj.vertex_groups.new(name=n) for n in names + ["Hand"]}
    segs = [(head, tail) for _, head, tail in bones]
    for vi, p in enumerate(coords):
        if vi >= hand_start:
            groups["Hand"].add([vi], 1.0, 'REPLACE')
            continue
        ds = []
        for k, (a, b) in enumerate(segs):
            ab = b - a
            t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
            ds.append(((a + ab * t - p).length, k))
        ds.sort()
        w = [1.0 / (d ** 4 + 1e-5) for d, _ in ds[:2]]
        s = sum(w)
        for (d, k), wk in zip(ds[:2], w):
            groups[names[k]].add([vi], wk / s, 'REPLACE')
    mod = mesh_obj.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm_obj
    mesh_obj.parent = arm_obj
    return arm_obj, mesh_obj


# ---------------------------------------------------------------- chair


def build_flesh():
    """Chair qui remplit le bol : épouse l'intérieur de la paroi (sans la traverser), bombée et grumeleuse
    au-dessus du bord. Seule en phase V, elle garde la forme du bol qu'elle a remplie."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1.0)
    z_bottom, z_lip, z_top = -0.82, 0.62, 0.92
    for v in bm.verts:
        d = v.co.normalized()
        u = (d.z + 1) * 0.5                                    # 0 en bas, 1 en haut
        z = z_bottom + (z_top - z_bottom) * u
        h = 0.7 * bumpy(d, 2.2, 53) + 0.3 * bumpy(d, 5.5, 57)
        if z <= z_lip:
            # Dans le bol : juste sous la paroi, creusé par endroits (jamais vers l'extérieur)
            r = (min(profile_radius(z), NECK_R) if z > 0.44 else profile_radius(z)) - WALL - 0.03 - 0.05 * abs(h)
        else:
            # Au-dessus du bord : dôme grumeleux qui déborde un peu
            # (même rayon que l'intérieur au bord : pas de marche)
            k = (z - z_lip) / (z_top - z_lip)
            r = (NECK_R - WALL - 0.03 - 0.05 * abs(h)) * math.sqrt(max(0.0, 1 - k * k)) * (1 + 0.12 * h * k)
            z += 0.08 * h * k
        horiz = Vector((d.x, d.y, 0))
        horiz = horiz.normalized() if horiz.length > 1e-5 else Vector((0, 0, 0))
        v.co = horiz * max(r, 0.0) + Vector((0, 0, z))
    triangulate(bm)
    colors = []
    for f in bm.faces:
        c = f.calc_center_median()
        col = mix(FLESH_DARK, FLESH, min(1.0, max(0.0, 0.55 + 0.6 * bumpy(c, 4.0, 61))))
        if bumpy(c, 3.5, 65) > 0.45:
            col = mix(col, FLESH_RAW, 0.7)                      # plaques à vif (larges, pas des confettis)
        if abs(bumpy(c, 3.0, 69)) < 0.03:
            col = FLESH_DARK                                    # plis sombres
        colors.append(srgb(col))
    return bm, colors


# ---------------------------------------------------------------- scène


def build(texture_path):
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    texture = bpy.data.images.load(texture_path)
    texture.filepath = "//" + os.path.basename(texture_path)     # chemin relatif au .blend
    global MATERIALS
    MATERIALS = make_materials(texture)

    bm, colors = build_bowl()
    bowl = make_object("Bowl", bm, ["Pottery", "PotteryRaw"], colors, uv_fn=bowl_uv)
    mod = bowl.modifiers.new("Solidify", 'SOLIDIFY')
    mod.thickness = WALL
    mod.offset = -1.0                                           # épaisseur vers l'intérieur
    mod.material_offset = 1                                     # intérieur et tranche : terre crue
    mod.material_offset_rim = 1

    for front in (True, False):
        h_bm, h_colors, _ = build_handle(front)
        make_object("Handle_Front" if front else "Handle_Back", h_bm, ["Pottery"], h_colors, uv_fn=bowl_uv)

    f_bm, f_colors = build_flesh()
    make_object("FleshCore", f_bm, ["Flesh"], f_colors)

    # Deux bras enracinés dans la chair ; posés au sol comme des béquilles, ils traînent le bol
    make_arm("Arm_Front", (0.3, -0.25, 0.7), (1.15, -0.42, 1.25), (1.85, -0.5, GROUND_Z + 0.08), (1, 0, -0.15),
             palm_side=(0, 1, 0), seed=1.0, curl=0.12)
    make_arm("Arm_Back", (-0.3, 0.25, 0.7), (-1.1, 0.4, 1.2), (-1.75, 0.45, GROUND_Z + 0.08), (-1, 0, -0.15),
             palm_side=(0, -1, 0), seed=2.0, curl=0.12)

    # Repère du sol
    g = bmesh.new()
    s = 6.0
    vs = [g.verts.new((x, y, GROUND_Z)) for x, y in ((-s, -s), (s, -s), (s, s), (-s, s))]
    g.faces.new(vs)
    ground = make_object("Ground", g, ["Ground"], [srgb(GROUND)])
    ground.hide_select = True


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    blend_path = os.path.abspath(args[0])
    force = "--force" in args
    tools = os.path.dirname(os.path.abspath(__file__))
    project = os.path.normpath(os.path.join(tools, "..", ".."))
    if os.path.exists(blend_path):
        if not force:
            print("JAR_BUILD_REFUSED : %s existe déjà (retouches à la main ?). Relancer avec --force pour le refaire."
                  % blend_path)
            return
        backups = os.path.join(project, "References", "Backups")
        os.makedirs(backups, exist_ok=True)
        backup = os.path.join(backups, "jar_%s.blend" % time.strftime("%Y%m%d_%H%M%S"))
        shutil.copy2(blend_path, backup)
        print("Ancien jar.blend copié dans " + backup)
    os.makedirs(os.path.dirname(blend_path), exist_ok=True)
    texture_path = os.path.join(os.path.dirname(blend_path), "jar_pottery.png")
    if force or not os.path.exists(texture_path):
        make_texture(texture_path)

    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    build(texture_path)

    # Découpage des morceaux, et script inclus dans le .blend pour le relancer après une retouche de Bowl
    sys.path.insert(0, tools)
    import jar_fracture  # noqa: E402
    jar_fracture.fracture()
    text = bpy.data.texts.load(os.path.join(tools, "jar_fracture.py"))
    text.name = "jar_fracture.py"

    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                for space in area.spaces:
                    if space.type == "VIEW_3D":
                        space.shading.type = "MATERIAL"
    # Morceaux cachés dans la vue (on modèle Bowl) ; Unity les lit quand même
    frag = bpy.data.collections.get("Fragments")
    if frag is not None:
        frag.hide_viewport = False
        for layer_coll in bpy.context.view_layer.layer_collection.children:
            if layer_coll.name == "Fragments":
                layer_coll.hide_viewport = True
    bpy.ops.wm.save_as_mainfile(filepath=blend_path, relative_remap=True)
    print("JAR_BUILD_OK")


if __name__ == "__main__":
    main()
