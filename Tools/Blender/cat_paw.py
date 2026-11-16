# Vraie patte de chat au bout du bras : paume, 4 doigts pliés en 2 phalanges, griffes claires recourbées (low-poly).
# Dans Blender : onglet Scripting > Open > ce fichier > Run Script, puis enregistrer (Ctrl+S).
#  - Forearm (origine = coude) garde seulement l'avant-bras : le plus long morceau de Forearm + Hand ;
#    l'ancienne paume, les anciens doigts et griffes sont retirés.
#  - Hand reçoit la nouvelle patte, posée au bout de l'avant-bras (poignet), paume tournée vers le corps,
#    doigts alignés de l'avant vers l'arrière, pliés vers le corps.
#  - Couleurs (couleur des sommets = fourrure) : celle du poignet pour la patte, CLAW_COLOR pour les griffes.
#  - Le côté loin (Far_Forearm, Far_Hand) partage ces maillages : il suit.
# Relancer le script refait la patte (les réglages ci-dessous peuvent être changés avant).
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

FINGERS = 4
SIDES = 6                       # faces autour d'un doigt (low-poly)
PALM_WIDTH = 1.25               # largeur de la paume / largeur du poignet
FINGER_LENGTH = (0.8, 0.65)     # phalanges / demi-largeur de la paume
FINGER_RADIUS = 0.32            # / demi-largeur de la paume
FINGER_CURL = (12.0, 22.0)      # pli de chaque phalange vers le corps (degrés)
FINGER_SPREAD = 8.0             # doigts écartés en éventail (degrés entre deux doigts)
CLAW_LENGTH = 0.75              # / demi-largeur de la paume
CLAW_CURL = 35.0                # griffe recourbée (degrés)
CLAW_COLOR = (0.86, 0.86, 0.9, 1.0)


def components(bm):
    seen, result = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            x = stack.pop()
            comp.append(x)
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen:
                    seen.add(y.index)
                    stack.append(y)
        result.append(comp)
    return result


def main():
    objects = bpy.data.objects
    fore, hand = objects["Forearm"], objects["Hand"]
    # Repère du bras dans les coordonnées de Forearm (Hand est dessus, sans décalage)
    to_hand = hand.matrix_world.inverted() @ fore.matrix_world
    inv = fore.matrix_world.inverted()

    # Tous les morceaux de l'avant-bras et de la main, dans le repère de Forearm
    bm = bmesh.new()
    bm.from_mesh(fore.data)
    other = hand.data.copy()
    other.transform(to_hand.inverted())
    bm.from_mesh(other)
    bpy.data.meshes.remove(other)
    bm.verts.index_update()   # numéros uniques après la fusion des deux maillages
    bm.verts.ensure_lookup_table()

    # Axe du bras : du coude (origine) vers le centre de tous les morceaux
    center = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    down = center.normalized()
    # Avant du chat (vers la truffe) et côté du corps, ramenés dans ce repère
    nose, tail = objects["Nose"].matrix_world.translation, objects["TailSeg0"].matrix_world.translation
    forward = (inv.to_3x3() @ (nose - tail))
    forward = (forward - down * forward.dot(down)).normalized()
    side = down.cross(forward).normalized()
    torso = inv @ objects["Torso"].matrix_world.translation
    inward = side if (torso - center).dot(side) > 0 else -side

    # Avant-bras = le morceau le plus long le long du bras ; le reste (paume, doigts, griffes) part
    def extent(comp):
        values = [v.co.dot(down) for v in comp]
        return max(values) - min(values)
    comps = components(bm)
    arm = max(comps, key=extent)
    keep = set(v.index for v in arm)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context="VERTS")
    bm.verts.index_update()
    bm.verts.ensure_lookup_table()

    # Poignet : bout de l'avant-bras
    color_layer = bm.loops.layers.color.get("VertexColors") or bm.loops.layers.color.new("VertexColors")
    ds = [v.co.dot(down) for v in bm.verts]
    end = max(ds)
    span = end - min(ds)
    tip = [v for v in bm.verts if v.co.dot(down) > end - span * 0.15]
    wrist = sum((v.co for v in tip), Vector()) / len(tip)
    half_fwd = max(abs((v.co - wrist).dot(forward)) for v in tip)
    half_side = max(abs((v.co - wrist).dot(side)) for v in tip)
    colors = [l[color_layer] for v in tip for l in v.link_loops]
    fur = tuple(sum(c[i] for c in colors) / len(colors) for i in range(3)) + (1.0,) if colors else (0.15, 0.16, 0.24, 1.0)
    bm.to_mesh(fore.data)
    bm.free()
    fore.data.update()

    # ---------- Nouvelle patte ----------
    paw = bmesh.new()
    col = paw.loops.layers.color.new("VertexColors")
    hw = max(half_fwd, half_side) * PALM_WIDTH        # demi-largeur de la paume (le long de l'avant)
    ht = hw * 0.55                                    # demi-épaisseur (vers le corps)

    def ring(at, axis, u, w, ru, rw, sides, phase=0.0):
        verts = []
        for k in range(sides):
            a = phase + 2.0 * math.pi * k / sides
            verts.append(paw.verts.new(at + u * (math.cos(a) * ru) + w * (math.sin(a) * rw)))
        return verts

    def bridge(r0, r1, color):
        n = len(r0)
        for k in range(n):
            face = paw.faces.new((r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k]))
            for loop in face.loops:
                loop[col] = color

    def cap(r, point, color, flip=False):
        tip_v = paw.verts.new(point)
        n = len(r)
        for k in range(n):
            verts = (r[k], tip_v, r[(k + 1) % n]) if not flip else (r[(k + 1) % n], tip_v, r[k])
            face = paw.faces.new(verts)
            for loop in face.loops:
                loop[col] = color

    # Paume : prisme aplati du poignet vers le bas, s'élargit puis se referme
    palm_len = hw * 1.1
    start = wrist - down * (span * 0.05)
    rings = [
        ring(start, down, forward, inward, hw * 0.8, ht * 0.9, 6),
        ring(start + down * palm_len * 0.55, down, forward, inward, hw, ht, 6),
        ring(start + down * palm_len, down, forward, inward, hw * 0.9, ht * 0.85, 6),
    ]
    cap(rings[0], start - down * ht * 0.3, fur, flip=True)
    bridge(rings[0], rings[1], fur)
    bridge(rings[1], rings[2], fur)
    cap(rings[2], start + down * (palm_len + ht * 0.4), fur)

    # Doigts : en ligne de l'avant vers l'arrière au bout de la paume, en léger éventail, pliés vers le corps
    radius = hw * FINGER_RADIUS
    for f in range(FINGERS):
        t = (f - (FINGERS - 1) / 2.0) / ((FINGERS - 1) / 2.0) if FINGERS > 1 else 0.0
        base = start + down * (palm_len * 0.92) + forward * (t * hw * 0.72)
        direction = (Matrix.Rotation(math.radians(t * FINGER_SPREAD * (FINGERS - 1) / 2.0), 3, inward) @ down).normalized()
        point = base
        r = radius
        previous = None
        for p, length in enumerate(FINGER_LENGTH):
            # Plier vers le corps : tourner autour de l'axe qui amène « direction » vers « inward »
            axis = direction.cross(inward).normalized()
            direction = (Matrix.Rotation(math.radians(FINGER_CURL[p]), 3, axis) @ direction).normalized()
            u = forward
            w = direction.cross(u).normalized()
            u = w.cross(direction).normalized()
            if previous is None:
                previous = ring(point, direction, u, w, r, r * 0.9, SIDES)
                cap(previous, point - direction * r * 0.5, fur, flip=True)
            point = point + direction * hw * length
            r_next = r * 0.85
            current = ring(point, direction, u, w, r_next, r_next * 0.9, SIDES)
            bridge(previous, current, fur)
            previous, r = current, r_next
        # Bout du doigt, puis griffe recourbée vers le corps
        cap(previous, point + direction * r * 0.6, fur)
        axis = direction.cross(inward).normalized()
        claw_dir = (Matrix.Rotation(math.radians(CLAW_CURL * 0.5), 3, axis) @ direction).normalized()
        u = forward
        w = claw_dir.cross(u).normalized()
        u = w.cross(claw_dir).normalized()
        claw_base = point + direction * r * 0.3 - inward * r * 0.25
        claw_ring = ring(claw_base, claw_dir, u, w, r * 0.55, r * 0.42, 4, math.pi / 4)
        mid_dir = (Matrix.Rotation(math.radians(CLAW_CURL * 0.5), 3, axis) @ claw_dir).normalized()
        mid = claw_base + claw_dir * hw * CLAW_LENGTH * 0.55
        mid_ring = ring(mid, mid_dir, u, w, r * 0.3, r * 0.22, 4, math.pi / 4)
        cap(claw_ring, claw_base - claw_dir * r * 0.1, CLAW_COLOR, flip=True)
        bridge(claw_ring, mid_ring, CLAW_COLOR)
        cap(mid_ring, mid + mid_dir * hw * CLAW_LENGTH * 0.5, CLAW_COLOR)

    bmesh.ops.recalc_face_normals(paw, faces=paw.faces)
    paw.transform(to_hand)   # repère de Forearm → repère de Hand
    paw.to_mesh(hand.data)
    paw.free()
    hand.data.update()
    # Côté loin : mêmes maillages (pour une mise en miroir complète, lancer aussi cat_mirror.py)
    for name, near in (("Far_Forearm", fore), ("Far_Hand", hand)):
        far = objects.get(name)
        if far is not None and far.type == "MESH" and far.data != near.data:
            old = far.data
            far.data = near.data
            if old.users == 0:
                bpy.data.meshes.remove(old)
    bpy.context.view_layer.update()
    print("Patte refaite :", FINGERS, "doigts")


main()
