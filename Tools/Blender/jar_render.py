# Rendus de validation du boss « la Jarre aux âmes », faits depuis le fichier tel quel (retouches comprises) :
#   blender -b Assets/Models/Jar/jar.blend --python Tools/Blender/jar_render.py -- References/Boss
# Une image par phase (vue du jeu), une planche des 5 phases, deux vues de 3/4. Le .blend n'est pas modifié.
# Phases (comme en jeu) : I bol entier ; II morceaux disjoints (fentes) ; III, IV, V : les morceaux Frag_P<n>
# tombent à la phase n ; une anse disparaît avec le morceau le plus proche.
import math
import os
import sys

import bpy
from mathutils import Vector


def centroid(obj):
    vs = obj.data.vertices
    return obj.matrix_world @ (sum((v.co for v in vs), Vector()) / max(len(vs), 1))


def set_phase(phase):
    frags = [o for o in bpy.data.objects if o.name.startswith("Frag_P")]
    handle_frag = {}
    for h in ("Handle_Front", "Handle_Back"):
        obj = bpy.data.objects.get(h)
        if obj is not None and frags:
            c = centroid(obj)
            handle_frag[h] = min(frags, key=lambda f: (f.matrix_world.translation - c).length)
    for obj in frags:
        obj.hide_render = phase < 2 or phase >= int(obj.name[6])
        obj.scale = (0.94, 0.94, 0.94) if phase >= 2 else (1, 1, 1)
    bowl = bpy.data.objects.get("Bowl")
    if bowl is not None:
        bowl.hide_render = phase >= 2
    for h, f in handle_frag.items():
        bpy.data.objects[h].hide_render = phase >= 2 and f.hide_render


def setup():
    scene = bpy.context.scene
    try:
        scene.render.engine = 'BLENDER_EEVEE'
    except TypeError:
        scene.render.engine = 'CYCLES'
    world = bpy.data.worlds.new("RenderWorld")
    scene.world = world
    try:
        world.use_nodes = True
    except Exception:
        pass
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.012, 0.016, 0.028, 1)
    transforms = [i.identifier for i in scene.view_settings.bl_rna.properties['view_transform'].enum_items]
    scene.view_settings.view_transform = 'AgX' if 'AgX' in transforms else 'Filmic'

    def light(name, rot, energy, color):
        data = bpy.data.lights.new(name, 'SUN')
        data.energy = energy
        data.color = color
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.rotation_euler = [math.radians(a) for a in rot]

    light("Key", (55, 0, -35), 3.2, (1.0, 0.93, 0.85))
    light("Rim", (-60, 0, 160), 2.0, (0.55, 0.7, 1.0))
    light("Fill", (80, 0, 60), 0.6, (0.7, 0.75, 0.9))
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    # Les collections cachées dans la vue doivent quand même être rendues
    for lc in bpy.context.view_layer.layer_collection.children:
        lc.hide_viewport = False
        lc.exclude = False
    return cam


def look_at(cam, eye, target):
    cam.location = eye
    cam.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat('-Z', 'Y').to_euler()


def render(path, w, h):
    scene = bpy.context.scene
    scene.render.resolution_x = w
    scene.render.resolution_y = h
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def contact_sheet(paths, out_path, w, h):
    import numpy as np
    tiles = []
    for p in paths:
        img = bpy.data.images.load(p)
        tiles.append(np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4))
        bpy.data.images.remove(img)
    out = bpy.data.images.new("Sheet", width=w * len(paths), height=h, alpha=True)
    out.pixels = np.concatenate(tiles, axis=1).ravel()
    out.filepath_raw = out_path
    out.file_format = 'PNG'
    out.save()


def main():
    out_dir = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
    os.makedirs(out_dir, exist_ok=True)
    for f in os.listdir(out_dir):
        if f.startswith("jar_") and f.endswith(".png"):
            os.remove(os.path.join(out_dir, f))
    cam = setup()
    w, h = 700, 640
    paths = []
    for phase in range(1, 6):
        set_phase(phase)
        look_at(cam, (0.0, -8.2, 1.4), (0.0, 0, 0.0))           # vue du jeu (côté caméra, -Y)
        p = os.path.join(out_dir, "jar_phase%d.png" % phase)
        render(p, w, h)
        paths.append(p)
    contact_sheet(paths, os.path.join(out_dir, "jar_phases.png"), w, h)
    set_phase(1)
    look_at(cam, (4.8, -5.6, 3.2), (0.1, 0, -0.1))
    render(os.path.join(out_dir, "jar_34.png"), 1000, 1000)
    set_phase(4)
    look_at(cam, (-4.4, -5.8, 3.0), (0.0, 0, -0.1))
    render(os.path.join(out_dir, "jar_phase4_34.png"), 1000, 1000)
    print("JAR_RENDER_OK")


if __name__ == "__main__":
    main()
