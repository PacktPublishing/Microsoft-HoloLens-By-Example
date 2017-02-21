
from .singleton import Singleton
from .scene_parser import SceneObject, SceneObjectParser
from .obj_sync_srv import ObjectSyncService
import bpy
import json
import time


class ObjectObserver(metaclass=Singleton):

    HISTORY_SIZE = 5

    def __init__(self):
        self.observed_objects = []
        self.history = {}

    def register_object(self, obj):
        if obj in self.observed_objects:
            return

        print("registering {}".format(obj))

        self.observed_objects.append(obj)
        self.history[obj.name] = []

        self.on_object_updated(obj)

        if len(self.observed_objects) == 1:
            # start listening to update events
            bpy.app.handlers.scene_update_post.append(self.on_scene_update_post)


    def unregister_object(self, obj):
        if obj not in self.observed_objects:
            return

        self.observed_objects.remove(obj)
        del self.history[obj.name]

        if len(self.observed_objects) == 0:
            # stop listening for scene updates
            bpy.app.handlers.scene_update_post.clear()


    def is_observing_object(self, obj):
        return obj in self.observed_objects


    def on_scene_update_post(self, blender_scene):
        for obj in blender_scene.objects:
            if obj not in self.observed_objects:
                continue

            if not obj.is_updated and not obj.data.is_updated:
                continue

            self.on_object_updated(
                obj,
                bpy.context.window_manager.operators[-1].bl_idname if len(bpy.context.window_manager.operators) > 0 else "UNKNOWN"
            )


    def on_object_updated(self, obj, tag='REFRESH'):
        # create packet (appending tag to let the receiver know of the operation applied)
        packet = SceneObjectParser.serailise_scene_object(obj).to_dict()
        packet['tag'] = tag
        packet['ts'] = time.time() * 1000

        self.history[obj.name].append(packet)

        exceeded_size = len(self.history[obj.name]) - ObjectObserver.HISTORY_SIZE

        if  exceeded_size > 0:
            self.history[obj.name] = self.history[obj.name][exceeded_size:]

        ObjectSyncService().enqueue_packet(
            json.dumps(
                packet
            )
        )