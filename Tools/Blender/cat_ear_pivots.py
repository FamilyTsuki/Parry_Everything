# Origine des oreilles à leur base : une rotation de l'oreille tourne autour de sa base.
# Dans Blender : onglet Scripting > Open > ce fichier > Run Script, puis enregistrer (Ctrl+S).
# La forme ne bouge pas. Peut être relancé.
# Base = centre des sommets de l'oreille les plus proches du crâne (le long de l'axe centre du crâne → centre de l'oreille).
# Si Ear_L est le miroir de Ear_R (cat_mirror.py : même maillage, pilotée), seule Ear_R est traitée : Ear_L suit.
import bpy
from mathutils import Matrix, Vector

BASE_PART = 0.15   # part de la hauteur de l'oreille comptée comme sa base


def center_world(ob):
    return sum((ob.matrix_world @ v.co for v in ob.data.vertices), Vector()) / len(ob.data.vertices)


def is_driven(ob):
    return ob.animation_data is not None and len(ob.animation_data.drivers) > 0


def move_origin_to_base(ob, skull):
    world = [ob.matrix_world @ v.co for v in ob.data.vertices]
    ear_center = sum(world, Vector()) / len(world)
    head_center = center_world(skull) if skull is not None else ob.parent.matrix_world.translation
    axis = (ear_center - head_center).normalized()   # du crâne vers la pointe de l'oreille
    low = min(p.dot(axis) for p in world)
    high = max(p.dot(axis) for p in world)
    base_points = [p for p in world if p.dot(axis) <= low + (high - low) * BASE_PART]
    base = ob.matrix_world.inverted() @ (sum(base_points, Vector()) / len(base_points))
    # Origine sur la base : maillage décalé à l'inverse, objet déplacé d'autant (rien ne bouge à l'écran)
    ob.data.transform(Matrix.Translation(-base))
    ob.matrix_basis = ob.matrix_basis @ Matrix.Translation(base)
    ob.data.update()
    bpy.context.view_layer.update()


def main():
    objects = bpy.data.objects
    skull = objects.get("Skull")
    done = set()   # un maillage partagé n'est décalé qu'une fois
    for name in ("Ear_R", "Ear_L"):
        ob = objects.get(name)
        if ob is None or not ob.data.vertices or ob.data in done or is_driven(ob):
            continue
        move_origin_to_base(ob, skull)
        done.add(ob.data)
    print("Oreilles : origine à la base")


if __name__ == "__main__":
    main()
