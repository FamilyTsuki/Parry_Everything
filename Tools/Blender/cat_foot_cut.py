# Découpe propre entre le jarret (Shin) et le pied (Foot) : un plan net à la cheville, au lieu de la découpe
# en dents de scie héritée de l'ancien découpage automatique (triangle par triangle, selon les poids des os).
#  - Shin et Foot sont réunis, puis coupés par un plan perpendiculaire au jarret (genou → cheville), placé juste
#    au-dessus du repère Ankle (ANKLE_RAISE) : tout ce qui est sous le plan devient le pied, le reste le jarret ;
#  - les deux pièces partagent exactement le bord de la coupe : Unity y pose la suture ;
#  - couleurs des sommets et matériaux gardés ; les maillages sont mis à jour en place (le côté loin, Far_Shin /
#    Far_Foot, les partage : il suit) ; places et origines des objets inchangées.
# Dans Blender : onglet Scripting > Open > ce fichier > Run Script, puis enregistrer (Ctrl+S). Peut être relancé
# (la coupe est refaite au même endroit).
# À noter : sous le talon, la jambe a un petit trou (et quelques arêtes partagées par plus de 2 faces) hérité de
# l'ancienne découpe ; ce script ne le répare pas (Maillage > Nettoyer > Remplir les trous, à la main).
import bmesh
import bpy

ANKLE_RAISE = 0.05   # plan remonté vers le genou, en part de la longueur genou → cheville (0 : sur le repère Ankle)
MERGE_DIST = 1e-5    # sommets confondus au raccord entre les deux pièces (unités du fichier)


def main():
    objects = bpy.data.objects
    shin, foot = objects["Shin"], objects["Foot"]
    knee, ankle = objects["Knee"], objects["Ankle"]
    if bpy.context.object is not None and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    # Plan de coupe (monde) : normale = genou → cheville, du côté du pied
    knee_at = knee.matrix_world.translation
    ankle_at = ankle.matrix_world.translation
    axis = ankle_at - knee_at
    normal = axis.normalized()
    point = ankle_at - axis * ANKLE_RAISE

    # Copie réunie des deux pièces (les originaux ne sont touchés qu'à la fin)
    copies = []
    for obj in (shin, foot):
        copy = obj.copy()
        copy.data = obj.data.copy()
        copy.parent = None
        copy.matrix_world = obj.matrix_world
        bpy.context.scene.collection.objects.link(copy)
        copies.append(copy)
    joined = copies[0]
    with bpy.context.temp_override(active_object=joined, selected_editable_objects=copies,
                                   selected_objects=copies, object=joined):
        bpy.ops.object.join()

    # Coupe, dans le repère de la copie réunie
    to_local = joined.matrix_world.inverted()
    plane_co = to_local @ point
    plane_no = (to_local.to_3x3() @ normal).normalized()
    bm = bmesh.new()
    bm.from_mesh(joined.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=MERGE_DIST)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=MERGE_DIST,
                           plane_co=plane_co, plane_no=plane_no)

    # Chaque côté vers sa pièce : sommets ramenés dans le repère de l'objet, maillage remplacé en place
    for target, keep_foot in ((shin, False), (foot, True)):
        part = bm.copy()
        doomed = [f for f in part.faces if ((f.calc_center_median() - plane_co).dot(plane_no) > 0.0) != keep_foot]
        bmesh.ops.delete(part, geom=doomed, context="FACES")
        loose = [v for v in part.verts if not v.link_faces]
        bmesh.ops.delete(part, geom=loose, context="VERTS")
        bmesh.ops.transform(part, matrix=target.matrix_world.inverted() @ joined.matrix_world, verts=part.verts)
        part.to_mesh(target.data)
        target.data.update()
        part.free()
        print("%s : %d faces" % (target.name, len(target.data.polygons)))
    bm.free()

    data = joined.data
    bpy.data.objects.remove(joined)
    bpy.data.meshes.remove(data)
    print("Pied recoupé à la cheville. Enregistrer (Ctrl+S) : Unity relit le fichier.")


main()
