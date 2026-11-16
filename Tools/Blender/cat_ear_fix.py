# Réparation : remet l'oreille droite (Ear_R) à sa place d'origine, celle du dernier export Unity (Cat.fbx,
# à côté de cat.blend), avec l'origine à sa base ; l'oreille gauche en miroir (cat_mirror.py) suit.
# À utiliser si l'oreille a été déplacée par erreur (ancien cat_ear_pivots.py lancé après cat_mirror.py).
# ⚠️ Remplace la forme de Ear_R par celle de Cat.fbx : une oreille remodelée depuis l'export serait perdue.
# Dans Blender : onglet Scripting > Open > ce fichier > Run Script, puis enregistrer (Ctrl+S).
import os

import bpy
from mathutils import Matrix, Vector

BASE_PART = 0.15   # part de la hauteur de l'oreille comptée comme sa base


def restore_shape(ear):
    fbx = os.path.join(os.path.dirname(bpy.data.filepath), "Cat.fbx")
    objects_before = set(bpy.data.objects)
    meshes_before = set(bpy.data.meshes)
    materials_before = set(bpy.data.materials)
    bpy.ops.import_scene.fbx(filepath=fbx)
    imported = [o for o in bpy.data.objects if o not in objects_before]
    try:
        source = next((o for o in imported if o.name.startswith("Ear_R")), None)
        if source is None or len(source.data.vertices) != len(ear.data.vertices):
            return False
        to_local = ear.matrix_world.inverted() @ source.matrix_world
        for v, s in zip(ear.data.vertices, source.data.vertices):
            v.co = to_local @ s.co
        ear.data.update()
        bpy.context.view_layer.update()
        return True
    finally:
        for o in imported:
            bpy.data.objects.remove(o, do_unlink=True)
        for m in [m for m in bpy.data.meshes if m not in meshes_before and m.users == 0]:
            bpy.data.meshes.remove(m)
        for m in [m for m in bpy.data.materials if m not in materials_before and m.users == 0]:
            bpy.data.materials.remove(m)


def origin_to_base(ear, skull):
    world = [ear.matrix_world @ v.co for v in ear.data.vertices]
    ear_center = sum(world, Vector()) / len(world)
    skull_center = sum((skull.matrix_world @ v.co for v in skull.data.vertices), Vector()) / len(skull.data.vertices)
    axis = (ear_center - skull_center).normalized()
    low = min(p.dot(axis) for p in world)
    high = max(p.dot(axis) for p in world)
    base_points = [p for p in world if p.dot(axis) <= low + (high - low) * BASE_PART]
    base = ear.matrix_world.inverted() @ (sum(base_points, Vector()) / len(base_points))
    ear.data.transform(Matrix.Translation(-base))
    ear.matrix_basis = ear.matrix_basis @ Matrix.Translation(base)
    ear.data.update()
    bpy.context.view_layer.update()


def main():
    ear = bpy.data.objects["Ear_R"]
    if not restore_shape(ear):
        print("Oreille : pas réparée (forme différente de celle de Cat.fbx)")
        return
    origin_to_base(ear, bpy.data.objects["Skull"])
    print("Oreille réparée")


main()
