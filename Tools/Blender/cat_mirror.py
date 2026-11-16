# Côté loin du chat (objets Far_…) : vrai miroir du bras et de la patte côté caméra.
#  - Far_UpperArm / Far_Leg : place et rotation en miroir (profondeur inversée), échelle −1 en profondeur :
#    la forme est retournée (une pièce tirée vers l'extérieur d'un côté va vers l'extérieur de l'autre) ;
#  - leurs morceaux (Far_Forearm, Far_Thigh…) suivent ceux du côté caméra à l'identique ;
#  - maillages partagés : modifier le côté caméra modifie les deux ;
#  - un morceau ajouté sous le bras ou la patte côté caméra reçoit sa copie Far_ (relancer ce script) ;
#  - oreille gauche (Ear_L) = miroir de l'oreille droite (Ear_R, côté caméra) : modifier Ear_R modifie les deux ;
#    de même œil gauche (Eye_L) = miroir de Eye_R (créé s'il manque).
# Les copies ne sont pas sélectionnables. Unity ignore les Far_ (le jeu retourne lui-même le côté loin) ;
# Ear_L et Eye_L sont relues telles quelles (retournées) avec la tête.
#
# Dans Blender : onglet Scripting > Open > ce fichier > Run Script, puis enregistrer (Ctrl+S).
# Aussi appelé par cat_blend_setup.py à la création du fichier.
import bpy

ROOTS = ("UpperArm", "Leg")
HEAD_PAIRS = (("Ear_R", "Ear_L"), ("Eye_R", "Eye_L"))   # (côté caméra, miroir)


def depth_axis():
    # Axe de profondeur dans le repère du torse : la coordonnée la plus différente entre le bras et sa copie
    near, far = bpy.data.objects["UpperArm"], bpy.data.objects["Far_UpperArm"]
    return max(range(3), key=lambda i: abs(near.location[i] - far.location[i]))


def add_driver(target, path, index, source, sign):
    curve = target.driver_add(path, index)
    driver = curve.driver
    driver.type = "SCRIPTED"
    for var in list(driver.variables):
        driver.variables.remove(var)
    var = driver.variables.new()
    var.name = "v"
    var.type = "SINGLE_PROP"
    var.targets[0].id = source
    var.targets[0].data_path = "%s[%d]" % (path, index)
    driver.expression = "v" if sign > 0 else "-v"


def ensure_copies(near, far_parent):
    # Copie Far_ de chaque morceau rangé sous la pièce (créée si elle manque)
    for child in near.children:
        name = "Far_" + child.name
        far = bpy.data.objects.get(name)
        if far is None:
            far = bpy.data.objects.new(name, child.data)
            for collection in child.users_collection:
                collection.objects.link(far)
        far.parent = far_parent
        far.matrix_parent_inverse = child.matrix_parent_inverse.copy()
        ensure_copies(child, far)


def link(far, near, is_root, depth):
    near.rotation_mode = "XYZ"
    far.rotation_mode = "XYZ"
    if near.type == "MESH" and far.type == "MESH" and far.data != near.data:
        old = far.data
        far.data = near.data
        if old.users == 0:
            bpy.data.meshes.remove(old)
    if far.animation_data:
        far.animation_data_clear()
    for i in range(3):
        if is_root:
            # Miroir : profondeur inversée, rotations autour des 2 autres axes inversées, forme retournée
            add_driver(far, "location", i, near, -1 if i == depth else 1)
            add_driver(far, "rotation_euler", i, near, 1 if i == depth else -1)
            add_driver(far, "scale", i, near, -1 if i == depth else 1)
        else:
            add_driver(far, "location", i, near, 1)
            add_driver(far, "rotation_euler", i, near, 1)
            add_driver(far, "scale", i, near, 1)
    far.hide_select = True


def link_tree(far, near, is_root, depth):
    link(far, near, is_root, depth)
    for child in near.children:
        far_child = bpy.data.objects.get("Far_" + child.name)
        if far_child is not None:
            link_tree(far_child, child, False, depth)


def sync_far():
    depth = depth_axis()
    for name in ROOTS:
        near = bpy.data.objects.get(name)
        far = bpy.data.objects.get("Far_" + name)
        if near is None or far is None:
            continue
        ensure_copies(near, far)
        link_tree(far, near, True, depth)
    # Oreille et œil gauches = miroirs des droits (côté caméra), par rapport au milieu de la tête
    for right_name, left_name in HEAD_PAIRS:
        right = bpy.data.objects.get(right_name)
        if right is None:
            continue
        left = bpy.data.objects.get(left_name)
        if left is None:
            # Copie gauche créée si elle manque (même maillage, même parent)
            left = bpy.data.objects.new(left_name, right.data)
            for collection in right.users_collection:
                collection.objects.link(left)
            left.parent = right.parent
            left.matrix_parent_inverse = right.matrix_parent_inverse.copy()
        link(left, right, True, depth)
    if "Ground" in bpy.data.objects:
        bpy.data.objects["Ground"].hide_select = True
    bpy.context.view_layer.update()
    print("Chat : côté loin en miroir.")


if __name__ == "__main__":
    sync_far()
