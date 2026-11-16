# Queue fine et lisse de chat.
# Dans Blender : onglet Scripting > Open > ce fichier > Run Script, puis enregistrer (Ctrl+S).
# Peut être relancé : chaque segment (TailSeg0…N) est refait à neuf, la couleur (fourrure) est reprise de l'ancien.
# Segment = tube effilé le long de son axe (+X local, vers le segment suivant) + demi-boule à l'arrière, du même rayon :
#    le rayon est continu d'un segment au suivant (pas de pointes aux articulations quand la queue se plie).
#    Le dernier segment finit par une demi-boule (bout arrondi).
import math

import bmesh
import bpy
from mathutils import Vector

TAIL_BASE_RADIUS = 0.06   # mètres, à la naissance de la queue
TAIL_TIP_RADIUS = 0.035   # mètres, au bout
SIDES = 8                 # faces autour de la queue
CAP_RINGS = 2             # anneaux de chaque demi-boule (hors pôle)


def segments():
    found = []
    while "TailSeg%d" % len(found) in bpy.data.objects:
        found.append(bpy.data.objects["TailSeg%d" % len(found)])
    return found


def vertex_colors(me):
    """Couleur moyenne de chaque sommet (les couleurs sont stockées par coin)."""
    attr = me.color_attributes.active_color
    if attr is None:
        return [], []
    sums = [Vector((0, 0, 0, 0)) for _ in me.vertices]
    counts = [0] * len(me.vertices)
    if attr.domain == 'CORNER':
        for loop in me.loops:
            sums[loop.vertex_index] += Vector(attr.data[loop.index].color)
            counts[loop.vertex_index] += 1
    else:
        for i, d in enumerate(attr.data):
            sums[i] += Vector(d.color)
            counts[i] += 1
    points = [v.co.copy() for v in me.vertices]
    return points, [s / max(1, c) for s, c in zip(sums, counts)]


def ring(bm, x, radius, sides):
    return [bm.verts.new((x, radius * math.cos(2 * math.pi * k / sides), radius * math.sin(2 * math.pi * k / sides)))
            for k in range(sides)]


def bridge(bm, a, b):
    n = len(a)
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))


def cap(bm, center_x, radius, direction, edge):
    """Demi-boule collée à l'anneau `edge` (en center_x), vers -X (direction -1) ou +X (+1)."""
    rings = [edge]
    for j in range(1, CAP_RINGS + 1):
        a = math.pi / 2 * j / (CAP_RINGS + 1)
        rings.append(ring(bm, center_x + direction * radius * math.sin(a), radius * math.cos(a), SIDES))
    pole = bm.verts.new((center_x + direction * radius, 0, 0))
    for a, b in zip(rings, rings[1:]):
        bridge(bm, a, b)
    last = rings[-1]
    for k in range(SIDES):
        bm.faces.new((last[k], last[(k + 1) % SIDES], pole))


def rebuild(ob, length, r0, r1, tip):
    old = ob.data
    points, colors = vertex_colors(old)

    bm = bmesh.new()
    start = ring(bm, 0.0, r0, SIDES)
    end = ring(bm, length, r1, SIDES)
    bridge(bm, start, end)
    cap(bm, 0.0, r0, -1, start)
    if tip:
        cap(bm, length, r1, 1, end)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    me = bpy.data.meshes.new(old.name)
    bm.to_mesh(me)
    bm.free()
    for mat in old.materials:
        me.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = True

    # Fourrure : chaque nouveau sommet prend la couleur de l'ancien sommet le plus proche
    if colors:
        attr = me.color_attributes.new("VertexColors", 'BYTE_COLOR', 'CORNER')
        me.color_attributes.active_color = attr
        nearest = []
        for v in me.vertices:
            best = min(range(len(points)), key=lambda i: (points[i] - v.co).length_squared)
            nearest.append(colors[best])
        for loop in me.loops:
            attr.data[loop.index].color = nearest[loop.vertex_index]

    ob.data = me
    if old.users == 0:
        bpy.data.meshes.remove(old)


def main():
    segs = segments()
    n = len(segs)
    if n == 0:
        print("Queue : aucun TailSeg0")
        return
    scale = segs[0].matrix_world.to_scale()[0]
    length = segs[1].location.length if n > 1 else 0.15 / scale

    def radius(t):
        return (TAIL_BASE_RADIUS + (TAIL_TIP_RADIUS - TAIL_BASE_RADIUS) * t) / scale

    for i, ob in enumerate(segs):
        rebuild(ob, length, radius(i / n), radius((i + 1) / n), i == n - 1)
    bpy.context.view_layer.update()
    print("Queue refaite :", n, "segments")


if __name__ == "__main__":
    main()
