# Yeux modelables : crée Eye_R (œil côté caméra : globe jaune en amande + pupille fendue, un seul objet)
# sous Head, à la place du repère Eye, et Eye_L en miroir (cat_mirror.py : modifier Eye_R modifie les deux).
# Dans Blender : onglet Scripting > Open > ce fichier > Run Script, puis enregistrer (Ctrl+S).
# Ne fait rien si Eye_R existe déjà (tes retouches ne sont pas écrasées) : supprimer Eye_R et Eye_L pour le refaire.
# Unity reprend les objets « Eye_… » de la tête tels quels, couleurs des sommets comprises (matériau lumineux),
# à la place des yeux qu'il générait ; sans eux, il les génère comme avant.
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

EYE_SIZE = 0.06                       # mètres (rayon de base)
EYE_TILT = 12.0                       # degrés, coin arrière relevé
EYE_COLOR = (1.0, 0.78, 0.12)         # jaune lumineux (sRGB)
PUPIL_COLOR = (0.02, 0.02, 0.03)
EYE_RADII = (1.15, 0.72, 0.5)         # × EYE_SIZE : long, haut, épais
PUPIL_RADII = (0.16, 0.6, 0.25)
PUPIL_OFFSET = (0.2, 0.4)             # × EYE_SIZE : vers l'avant, vers l'extérieur de la tête


def tools_dir():
    here = globals().get("__file__", "")
    if here and os.path.isfile(here):
        return os.path.dirname(here)
    # Lancé depuis l'éditeur de texte : chemin déduit de cat.blend (Assets/Models/Cat/)
    return os.path.normpath(os.path.join(os.path.dirname(bpy.data.filepath), "..", "..", "..", "Tools", "Blender"))


def add_ellipsoid(bm, center, radii):
    made = bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    for v in made["verts"]:
        v.co = Vector((center[0] + v.co.x * radii[0], center[1] + v.co.y * radii[1], center[2] + v.co.z * radii[2]))
    return len(made["verts"])


def main():
    objects = bpy.data.objects
    if "Eye_R" in objects:
        print("Yeux : Eye_R existe déjà, rien à faire")
        return
    head = objects["Head"]
    marker = objects.get("Eye")
    scale = head.matrix_world.to_scale()[0]
    e = EYE_SIZE / scale
    # Avant de la tête = côté de la truffe (axe X du repère de la tête, signe selon le fichier)
    nose = objects.get("Nose")
    forward = -1.0 if nose is None or nose.location.x < 0 else 1.0
    location = marker.location.copy() if marker is not None else Vector((forward * 0.19, 0.04, -0.15)) / scale
    out = -1.0 if location.z <= 0 else 1.0   # côté caméra : profondeur négative

    bm = bmesh.new()
    eye_verts = add_ellipsoid(bm, (0, 0, 0), [r * e for r in EYE_RADII])
    add_ellipsoid(bm, (forward * PUPIL_OFFSET[0] * e, 0, out * PUPIL_OFFSET[1] * e), [r * e for r in PUPIL_RADII])
    me = bpy.data.meshes.new("Eye")
    bm.to_mesh(me)
    bm.free()
    # Couleurs en sRGB (comme le reste du chat à l'import/export FBX) : globe puis pupille
    attr = me.color_attributes.new("VertexColors", 'BYTE_COLOR', 'CORNER')
    me.color_attributes.active_color = attr
    for loop in me.loops:
        color = EYE_COLOR if loop.vertex_index < eye_verts else PUPIL_COLOR
        attr.data[loop.index].color_srgb = (*color, 1.0)
    for p in me.polygons:
        p.use_smooth = True
    skull = objects.get("Skull")
    if skull is not None:
        for mat in skull.data.materials:
            me.materials.append(mat)

    eye = objects.new("Eye_R", me)
    for collection in head.users_collection:
        collection.objects.link(eye)
    eye.parent = head
    eye.location = location
    eye.rotation_euler = (0.0, 0.0, math.radians(EYE_TILT) * -forward)

    sys.path.insert(0, tools_dir())
    import cat_mirror
    cat_mirror.sync_far()
    print("Yeux : Eye_R créé, Eye_L en miroir")


if __name__ == "__main__":
    main()
