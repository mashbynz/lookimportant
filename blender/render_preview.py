"""Render a Cycles preview of workbench.blend from the player's camera.

    blender -b blender/workbench.blend -P blender/render_preview.py -- out.png [overview]

Lights approximate the Unity setup (ceiling fluorescent + desk lamp) so the
layout can be judged before importing. Nothing here is exported.
"""
import math
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out = args[0] if args else "/tmp/preview.png"
overview = len(args) > 1 and args[1] == "overview"
scene = bpy.context.scene
obj = bpy.data.objects


def aim(ob, target):
    ob.rotation_euler = (Vector(target) - ob.location).to_track_quat("-Z", "Y").to_euler()


cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
scene.collection.objects.link(cam)
cam.data.sensor_fit = "VERTICAL"
cam.data.angle_y = math.radians(52)
if overview:
    cam.location = (0.9, -1.6, 2.0)
    aim(cam, (0, 0.4, 1.1))
else:
    cam.location = obj["Camera_Point"].matrix_world.translation
    aim(cam, obj["Camera_Target"].matrix_world.translation)
scene.camera = cam

ceil = bpy.data.objects.new("CeilLight", bpy.data.lights.new("CeilLight", "AREA"))
ceil.data.shape = "RECTANGLE"
ceil.data.size, ceil.data.size_y = 1.2, 0.12
ceil.data.energy = 60
ceil.data.color = (0.92, 0.96, 1.0)
scene.collection.objects.link(ceil)
ceil.location = obj["CeilingLight_Point"].matrix_world.translation
aim(ceil, obj["CeilingLight_Target"].matrix_world.translation)

lamp = bpy.data.objects.new("DeskLight", bpy.data.lights.new("DeskLight", "SPOT"))
lamp.data.energy = 25
lamp.data.spot_size = math.radians(80)
lamp.data.spot_blend = 0.5
lamp.data.shadow_soft_size = 0.02
lamp.data.color = (1.0, 0.85, 0.65)
scene.collection.objects.link(lamp)
lamp.location = obj["DeskLamp_Point"].matrix_world.translation
aim(lamp, obj["DeskLamp_Target"].matrix_world.translation)

world = bpy.data.worlds.new("W")
world.color = (0.03, 0.035, 0.04)
scene.world = world
if world.node_tree is None:
    world.use_nodes = True
bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (0.03, 0.035, 0.04, 1)

scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 48
scene.cycles.use_denoising = True
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
scene.view_settings.view_transform = "AgX"
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
print("PREVIEW", out)
