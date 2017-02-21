
import bpy
from .obj_sync_srv import ObjectSyncService
from .obj_observer import ObjectObserver


def reimport_modules():
    if "bpy" in locals():
        import importlib

        if "ObjectSyncService" in locals():
            importlib.reload(ObjectSyncService)

        if "ObjectObserver" in locals():
            importlib.reload(ObjectObserver)

        if "ObjectPanelCDBlendSrv" in locals():
            importlib.reload(ObjectPanelCDBlendSrv)

        if "ObjectOpObserve" in locals():
            importlib.reload(ObjectOpObserve)

        if "ObjectOpUnobserve" in locals():
            importlib.reload(ObjectOpUnobserve)


def register_addon():
    ObjectSyncService().start()

    bpy.utils.register_class(ObjectPanelCDBlendSrv)
    bpy.utils.register_class(ObjectOpObserve)
    bpy.utils.register_class(ObjectOpUnobserve)


def unregister_addon():
    ObjectSyncService().stop()

    bpy.utils.unregister_class(ObjectPanelCDBlendSrv)
    bpy.utils.unregister_class(ObjectOpObserve)
    bpy.utils.unregister_class(ObjectOpUnobserve)


class ObjectPanelCDBlendSrv(bpy.types.Panel):
    bl_label = "CD Blender Service"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "object"

    def draw(self, context):
        row = self.layout.row()
        split = row.split(percentage=0.5)
        col_left = split.column()
        col_right = split.column()

        for obj in bpy.context.selected_objects:
            if not obj.type == 'MESH':
                continue

            col_left.label(text=obj.name)

            if ObjectObserver().is_observing_object(obj):
                col_right.operator("cdblendsrvop.unobserve", text='Unobserve').selected_objects_name = obj.name
            else:
                col_right.operator("cdblendsrvop.observe", text='Observe').selected_objects_name = obj.name

class ObjectOpObserve(bpy.types.Operator):
    bl_label = "CDBlendSrv Observe OP"
    bl_idname = "cdblendsrvop.observe"
    bl_description = "Sync Service Observe OP"
    selected_objects_name = bpy.props.StringProperty(name="selected_objects_name")

    def execute(self, context):
        selected_object = bpy.data.objects.get(self.selected_objects_name)
        ObjectObserver().register_object(selected_object)
        self.report({'INFO'}, "Observing {}".format(selected_object.name))
        return {'FINISHED'}

class ObjectOpUnobserve(bpy.types.Operator):
    bl_label = "CDBlendSrv Unobserve OP"
    bl_idname = "cdblendsrvop.unobserve"
    bl_description = "Sync Service Observe OP"
    selected_objects_name = bpy.props.StringProperty(name="selected_objects_name")

    def execute(self, context):
        selected_object = bpy.data.objects.get(self.selected_objects_name)
        ObjectObserver().unregister_object(selected_object)
        self.report({'INFO'}, "Removing observing from {}".format(selected_object.name))
        return {'FINISHED'}