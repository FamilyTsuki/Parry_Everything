# Hanche (origine de Leg) recollée à la jambe : si Thigh / Shin / Foot ont été déplacés ensemble dans Leg,
# l'origine de Leg est déplacée d'autant (la hanche suit la jambe), leurs places remises à 0.
# Genou (Knee) et cheville (Ankle) remis dans le plan de la jambe (profondeur 0 dans le repère de Leg) ;
# rien ne bouge à l'écran sauf les repères. Le côté loin (Far_) suit par ses pilotes.
# Dans Blender : onglet Scripting > Open > ce fichier > Run Script, puis enregistrer (Ctrl+S). Peut être relancé.
import bpy
from mathutils import Matrix, Vector

PARTS = ("Thigh", "Shin", "Foot")
DEPTH = 2   # axe de profondeur (Z) dans le repère de Leg


def main():
    objects = bpy.data.objects
    leg = objects["Leg"]
    parts = [objects[n] for n in PARTS if n in objects and objects[n].parent == leg]
    knee = objects.get("Knee")
    ankle = objects.get("Ankle")
    # Repères gardés à leur place dans le monde pendant le déplacement de l'origine
    knee_world = knee.matrix_world.copy() if knee else None
    ankle_world = ankle.matrix_world.copy() if ankle else None

    # Décalage commun des morceaux (moyenne s'ils diffèrent)
    offset = sum((p.location for p in parts), Vector()) / max(1, len(parts))
    if offset.length > 1e-6:
        # Origine de Leg déplacée de offset (dans son propre repère), morceaux compensés : la forme ne bouge pas
        leg.matrix_world = leg.matrix_world @ Matrix.Translation(offset)
        bpy.context.view_layer.update()
        for p in parts:
            p.location -= offset
        if knee:
            knee.matrix_world = knee_world
        if ankle:
            ankle.matrix_world = ankle_world
        bpy.context.view_layer.update()

    # Genou et cheville dans le plan de la hanche (le jeu plie la patte dans ce plan)
    if knee:
        knee.location[DEPTH] = 0.0
        bpy.context.view_layer.update()
    if ankle:
        at = leg.matrix_world.inverted() @ ankle.matrix_world.translation
        at[DEPTH] = 0.0
        ankle.matrix_world.translation = leg.matrix_world @ at
    bpy.context.view_layer.update()
    print("Hanche recollée : décalage", tuple(round(x, 2) for x in offset))


if __name__ == "__main__":
    main()
