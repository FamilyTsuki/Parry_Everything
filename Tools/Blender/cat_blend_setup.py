# Lancé par Unity (Parry Everything > Exporter le chat vers Blender), Blender en arrière-plan :
#   blender -b --factory-startup --python cat_blend_setup.py -- Cat.fbx cat.blend
# Importe le chat exporté par Unity et en fait un fichier prêt à modifier :
#  - le bras et la patte du côté loin (Far_...) sont le miroir du côté caméra (cat_mirror.py) ;
#  - le sol (Ground) n'est pas sélectionnable : c'est la hauteur des pieds du joueur ;
#  - la vue 3D montre la couleur des sommets (= fourrure).
import os
import sys

import bpy

args = sys.argv[sys.argv.index("--") + 1:]
fbx_path, blend_path = args[0], args[1]

# Scène vide
for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
for collection in list(bpy.data.collections):
    bpy.data.collections.remove(collection)

bpy.ops.import_scene.fbx(filepath=fbx_path)
objects = {obj.name: obj for obj in bpy.data.objects}

# Côté loin : vrai miroir du côté caméra (cat_mirror.py, à côté de ce script)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cat_mirror  # noqa: E402

cat_mirror.sync_far()

if "Ground" in objects:
    objects["Ground"].hide_select = True

# Vue 3D : couleur des sommets, éclairage studio
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type != "VIEW_3D":
            continue
        for space in area.spaces:
            if space.type == "VIEW_3D":
                space.shading.type = "SOLID"
                space.shading.color_type = "VERTEX"

bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=blend_path)
print("CAT_BLEND_OK")
