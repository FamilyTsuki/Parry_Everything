# Inclinaison du torse au repos : le torse est mis à TORSO_TILT degrés (rotation Z de Torso, vers l'avant).
# Dans Blender : onglet Scripting > Open > ce fichier > Run Script, puis enregistrer (Ctrl+S).
# Peut être relancé (valeur absolue, pas cumulée).
# Tête, bras et jambes gardent leur orientation (ils tournent à l'inverse du torse) ; la queue suit le torse.
# Le torse est remonté/descendu pour que les hanches restent à la même hauteur (pieds sur Ground).
import math

import bpy
from mathutils import Matrix

TORSO_TILT = 12.0   # degrés (avant : 22)
KEEP_ORIENTATION = ("Head", "UpperArm", "Leg")   # enfants du torse qui ne penchent pas avec lui


def hip_height(torso, leg):
    return (Matrix.Rotation(torso.rotation_euler.z, 3, 'Z') @ leg.location).y


def main():
    objects = bpy.data.objects
    torso = objects["Torso"]
    leg = objects["Leg"]
    delta = math.radians(TORSO_TILT) - torso.rotation_euler.z
    before = hip_height(torso, leg)
    torso.rotation_euler.z += delta
    for name in KEEP_ORIENTATION:
        ob = objects.get(name)
        if ob is not None and ob.parent == torso:
            ob.rotation_euler.z -= delta
    torso.location.y += before - hip_height(torso, leg)
    bpy.context.view_layer.update()
    print("Torse incliné à", TORSO_TILT, "°")


if __name__ == "__main__":
    main()
