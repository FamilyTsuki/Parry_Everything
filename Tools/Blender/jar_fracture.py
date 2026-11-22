# Découpe le bol (objet « Bowl ») de jar.blend en morceaux épais : Frag_P<n>_<k>, dans la collection « Fragments ».
# À relancer après chaque retouche de Bowl :
#   - dans Blender : onglet Scripting > texte « jar_fracture.py » > Run Script, puis enregistrer (Ctrl+S) ;
#   - en ligne de commande : blender -b Assets/Models/Jar/jar.blend --python Tools/Blender/jar_fracture.py
# Les anciens morceaux sont supprimés et refaits. Bowl n'est pas modifié.
#
# Bowl = surface extérieure du bol (UV = texture de poterie) ; son modificateur Solidify donne l'épaisseur
# (le jeu montre Bowl entier en phase I, puis les morceaux à partir de la phase II).
# Chaque morceau garde les UV de Bowl sur sa face extérieure ; intérieur et tranches prennent le matériau 2
# (terre crue / intérieur incrusté, couleurs des sommets).
# Ordre de chute : le haut d'abord (la chair se voit), le morceau qui porte Handle_Back en phase III.
import math
import random

import bmesh
import bpy
from mathutils import Vector, noise

FRAGMENTS = 22                     # nombre de morceaux
MIN_SPACING = 0.38                 # écart minimum entre deux centres de morceaux
BREAK_COUNTS = {3: 3, 4: 5}        # morceaux qui tombent en phase III, puis IV (le reste en V)
SEED = 8
CLAY_RAW = (0.74, 0.45, 0.28)      # tranche : terre cuite à cru
INSIDE = (0.22, 0.24, 0.22)        # intérieur : sombre, incrusté


def bumpy(p, scale, seed):
    return noise.noise(Vector((p[0] * scale + seed, p[1] * scale - seed * 0.7, p[2] * scale + seed * 1.3)))


def solidify_settings(bowl):
    for mod in bowl.modifiers:
        if mod.type == 'SOLIDIFY':
            return mod.thickness, mod.offset
    return 0.07, -1.0


def cut(shell, faces, uv_src, thickness, offset):
    """Copie les faces de la cellule (avec leurs UV) et leur donne une épaisseur, comme le Solidify de Bowl.
    Sommets d'origine marqués : face extérieure = que des sommets d'origine, intérieure = aucun, tranche = mélange."""
    bm = bmesh.new()
    tag = bm.verts.layers.int.new("outer")
    uv = bm.loops.layers.uv.new("UVMap")
    vmap = {}
    for fi in faces:
        f = shell.faces[fi]
        verts = []
        for v in f.verts:
            if v.index not in vmap:
                nv = bm.verts.new(v.co)
                nv[tag] = 1
                vmap[v.index] = nv
            verts.append(vmap[v.index])
        nf = bm.faces.new(verts)
        for l_new, l_old in zip(nf.loops, f.loops):
            l_new[uv].uv = l_old[uv_src].uv
    bm.normal_update()
    n_orig = len(bm.verts)
    # Solidify de Blender : offset -1 = épaisseur vers l'intérieur (contre les normales)
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=thickness * -offset)
    # Solidify recopie le marqueur sur les sommets qu'il crée (ajoutés après ceux d'origine) : on l'efface
    bm.verts.index_update()
    for v in bm.verts:
        if v.index >= n_orig:
            v[tag] = 0
    outer = [v for v in bm.verts if v[tag] == 1]
    inner = [v for v in bm.verts if v[tag] != 1]
    rad = lambda vs: sum(Vector((v.co.x, v.co.y)).length for v in vs) / max(len(vs), 1)
    if inner and rad(inner) > rad(outer):
        bm.free()
        return cut(shell, faces, uv_src, -thickness, offset)
    return bm, tag, uv


def fracture():
    bowl = bpy.data.objects.get("Bowl")
    if bowl is None:
        raise RuntimeError("jar_fracture : objet « Bowl » introuvable")
    thickness, offset = solidify_settings(bowl)
    rng = random.Random(SEED)

    # Collection des morceaux (refaite à chaque fois)
    coll = bpy.data.collections.get("Fragments")
    if coll is None:
        coll = bpy.data.collections.new("Fragments")
        bpy.context.scene.collection.children.link(coll)
    for obj in list(coll.objects):
        mesh = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        if mesh is not None and mesh.users == 0:
            bpy.data.meshes.remove(mesh)

    shell = bmesh.new()
    shell.from_mesh(bowl.data)
    shell.transform(bowl.matrix_world)
    bmesh.ops.triangulate(shell, faces=shell.faces[:])
    shell.faces.ensure_lookup_table()
    uv_src = shell.loops.layers.uv.active
    centers = [f.calc_center_median() for f in shell.faces]

    # Graines tirées parmi les centres des faces, assez écartées
    seeds = []
    pool = list(range(len(centers)))
    rng.shuffle(pool)
    for fi in pool:
        p = centers[fi]
        if all((p - q).length > MIN_SPACING for q in seeds):
            seeds.append(p)
        if len(seeds) >= FRAGMENTS:
            break
    cells = [[] for _ in seeds]
    for fi, c in enumerate(centers):
        # Distance un peu bruitée : bords de cassure irréguliers
        k = min(range(len(seeds)), key=lambda j: (c - seeds[j]).length + 0.06 * bumpy(c, 6.0, j))
        cells[k].append(fi)

    pieces = []
    for faces in cells:
        if not faces:
            continue
        bm, tag, uv = cut(shell, faces, uv_src, thickness, offset)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        center = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
        pieces.append((bm, tag, center))
    shell.free()

    # Ordre de chute
    order = sorted(range(len(pieces)), key=lambda i: -(pieces[i][2].z + rng.uniform(0, 0.7)))
    handle = bpy.data.objects.get("Handle_Back")
    if handle is not None:
        hp = handle.matrix_world @ (sum((v.co for v in handle.data.vertices), Vector()) / max(len(handle.data.vertices), 1))
        hb = min(range(len(pieces)), key=lambda i: (pieces[i][2] - hp).length)
        order.remove(hb)
        order.insert(0, hb)
    phase_of = {}
    pos = 0
    for phase in (3, 4):
        for i in order[pos:pos + BREAK_COUNTS[phase]]:
            phase_of[i] = phase
        pos += BREAK_COUNTS[phase]
    for i in order[pos:]:
        phase_of[i] = 5

    mats = [m for m in bowl.data.materials]
    for i, (bm, tag, center) in enumerate(pieces):
        kinds = []
        for f in bm.faces:
            n_outer = sum(1 for v in f.verts if v[tag] == 1)
            kinds.append(0 if n_outer == len(f.verts) else 1 if n_outer == 0 else 2)   # 0 ext., 1 int., 2 tranche
            f.material_index = 0 if kinds[-1] == 0 else 1
        # Normales : les faces extérieures regardent dehors
        bm.normal_update()
        score = 0.0
        for f, k in zip(bm.faces, kinds):
            if k == 0:
                c = f.calc_center_median()
                score += f.normal.dot(Vector((c.x, c.y, 0)) if c.z > -0.88 else Vector((0, 0, -1)))
        if score < 0:
            bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        for v in bm.verts:
            v.co -= center
        name = "Frag_P%d_%02d" % (phase_of[i], i)
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        col = mesh.color_attributes.new(name="Col", type='BYTE_COLOR', domain='CORNER')
        r = random.Random(i)
        for poly, k in zip(mesh.polygons, kinds):
            base = (1.0, 1.0, 1.0) if k == 0 else INSIDE if k == 1 else CLAY_RAW
            j = 1.0 + r.uniform(-0.05, 0.05)
            c = (min(1.0, base[0] * j), min(1.0, base[1] * j), min(1.0, base[2] * j), 1.0)
            for li in poly.loop_indices:
                col.data[li].color_srgb = c
            poly.use_smooth = False
        for m in mats:
            mesh.materials.append(m)
        obj = bpy.data.objects.new(name, mesh)
        obj.location = center
        coll.objects.link(obj)
    print("JAR_FRACTURE_OK %d morceaux" % len(pieces))


if __name__ == "__main__":
    fracture()
    if bpy.app.background and bpy.data.filepath:
        bpy.context.preferences.filepaths.save_version = 0      # pas de copie .blend1 dans Assets
        bpy.ops.wm.save_mainfile()
