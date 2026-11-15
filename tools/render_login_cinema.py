"""Render the Login success film locally with Blender 4.5 LTS.

blender --background --factory-startup --python tools/render_login_cinema.py --
    --output C:/path/to/render --preview
Remove --preview to render and encode the 72-frame, silent H.264 film.
No downloaded scripts, paid services, or application/Identity dependencies.
"""
import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

args = argparse.ArgumentParser()
args.add_argument('--output', required=True)
args.add_argument('--preview', action='store_true')
args.add_argument('--resume', action='store_true')
args.add_argument('--samples', type=int, default=64)
args = args.parse_args(sys.argv[sys.argv.index('--') + 1:])
out = Path(args.output).resolve()
out.mkdir(parents=True, exist_ok=True)
frames = out / 'frames'
frames.mkdir(exist_ok=True)
WIDTH, HEIGHT, FPS = 960, 640, 24


def smooth(a, b, x):
    t = max(0, min(1, (x-a)/(b-a)))
    return t*t*(3-2*t)


def aim(obj, point):
    obj.rotation_euler = (Vector(point)-obj.location).to_track_quat('-Z', 'Y').to_euler()


def principled(name, color, rough=.4, metal=0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    p = mat.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = rough
    p.inputs['Metallic'].default_value = metal
    return mat, p


def noise_surface(mat, shader, scale, distance, strength=.15):
    n, links = mat.node_tree.nodes, mat.node_tree.links
    noise = n.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = scale
    noise.inputs['Detail'].default_value = 3
    bump = n.new('ShaderNodeBump')
    bump.inputs['Distance'].default_value = distance
    bump.inputs['Strength'].default_value = strength
    links.new(noise.outputs['Fac'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], shader.inputs['Normal'])
    return noise


olive, olive_shader = principled('Olive skin - natural wax and fine pores', (.23,.29,.09), .32)
olive_shader.inputs['Coat Weight'].default_value = .2
olive_shader.inputs['Coat Roughness'].default_value = .23
olive_shader.inputs['Subsurface Weight'].default_value = 0
micro = noise_surface(olive, olive_shader, 110, .00010, .3)
nodes, links = olive.node_tree.nodes, olive.node_tree.links
mottle = nodes.new('ShaderNodeTexNoise')
mottle.inputs['Scale'].default_value = 3.8
mottle.inputs['Detail'].default_value = 4
ramp = nodes.new('ShaderNodeValToRGB')
ramp.color_ramp.elements[0].position = .15
ramp.color_ramp.elements[0].color = (.028,.058,.009,1)
ramp.color_ramp.elements[1].position = .85
ramp.color_ramp.elements[1].color = (.14,.19,.045,1)
links.new(mottle.outputs['Fac'], ramp.inputs[0])
freckles = nodes.new('ShaderNodeTexVoronoi')
freckles.inputs['Scale'].default_value = 85
spots = nodes.new('ShaderNodeValToRGB')
spots.color_ramp.elements[0].position = .04
spots.color_ramp.elements[0].color = (1.55,1.45,1.2,1)
spots.color_ramp.elements[1].position = .14
spots.color_ramp.elements[1].color = (1,1,1,1)
color_mix = nodes.new('ShaderNodeMixRGB')
color_mix.blend_type = 'MULTIPLY'
color_mix.inputs[0].default_value = 1
links.new(freckles.outputs['Distance'],spots.inputs[0])
links.new(ramp.outputs['Color'],color_mix.inputs[1])
links.new(spots.outputs['Color'],color_mix.inputs[2])
links.new(color_mix.outputs[0],olive_shader.inputs['Base Color'])

leaf_mat, leaf_shader = principled('Olive leaf - leathery green, silver underside', (.12,.17,.055), .5)
leaf_shader.inputs['Subsurface Weight'].default_value = .04
n, l = leaf_mat.node_tree.nodes, leaf_mat.node_tree.links
geometry = n.new('ShaderNodeNewGeometry')
mix = n.new('ShaderNodeMixRGB')
mix.inputs[1].default_value = (.09,.14,.04,1)
mix.inputs[2].default_value = (.34,.39,.25,1)
l.new(geometry.outputs['Backfacing'], mix.inputs[0])
l.new(mix.outputs[0], leaf_shader.inputs['Base Color'])
noise_surface(leaf_mat, leaf_shader, 130, .000018, .12)
twig_mat, twig_shader = principled('Woody stem', (.14,.11,.045), .78)
noise_surface(twig_mat, twig_shader, 45, .00015, .35)
vein_mat, _ = principled('Leaf midrib', (.19,.24,.08), .6)
backdrop, _ = principled('Warm cream photographic backdrop', (.80,.74,.60), .92)

metal, metal_shader = principled('Brushed stainless steel', (.58,.61,.57), .24, 1)
metal_shader.inputs['Anisotropic'].default_value = .65
noise_surface(metal, metal_shader, 430, .000008, .2)
oil, oil_shader = principled('Extra virgin olive oil - refractive volume', (.99,.97,.79), .055)
oil_shader.inputs['Transmission Weight'].default_value = 1
oil_shader.inputs['IOR'].default_value = 1.47
oil_shader.inputs['Coat Weight'].default_value = .15
absorb = oil.node_tree.nodes.new('ShaderNodeVolumeAbsorption')
absorb.inputs['Color'].default_value = (.93,.8,.3,1)
absorb.inputs['Density'].default_value = 240
oil.node_tree.links.new(absorb.outputs['Volume'], oil.node_tree.nodes.get('Material Output').inputs['Volume'])


def mesh(name, verts, faces, material):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def tube(name, points, radius, material):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 16
    curve.bevel_depth = radius
    curve.bevel_resolution = 5
    spline = curve.splines.new('BEZIER')
    spline.bezier_points.add(len(points)-1)
    for bp, point in zip(spline.bezier_points, points):
        bp.co = point
        bp.handle_left_type = bp.handle_right_type = 'AUTO'
    obj = bpy.data.objects.new(name, curve)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def leaf(name, location, direction, length=.045, width=.004):
    verts, faces = [], []
    rows, columns = 25, 9
    for i in range(rows):
        t = i/(rows-1)
        for j in range(columns):
            side = j/(columns-1)*2-1
            verts.append((side*width*math.sin(math.pi*t)**.72,
                          length*t, .00055*side*side*math.sin(math.pi*t)-.003*t*t))
    for i in range(rows-1):
        for j in range(columns-1):
            a = i*columns+j
            faces.append((a,a+1,a+columns+1,a+columns))
    obj = mesh(name, verts, faces, leaf_mat)
    obj.location = location
    obj.rotation_euler = Vector(direction).to_track_quat('Y','Z').to_euler()
    solid = obj.modifiers.new('Natural leaf thickness', 'SOLIDIFY')
    solid.thickness = .00007
    rib = tube(name+' midrib', [(0,0,0),(0,length*.5,-.0006),(0,length,-.003)], .000065, vein_mat)
    rib.parent = obj
    return obj


def studio(name):
    scene = bpy.data.scenes.new(name)
    bpy.context.window.scene = scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = args.samples
    scene.cycles.use_denoising = True
    scene.cycles.adaptive_threshold = .025
    scene.cycles.max_bounces = 10
    scene.cycles.transmission_bounces = 8
    scene.render.resolution_x, scene.render.resolution_y = WIDTH, HEIGHT
    scene.render.resolution_percentage = 100
    scene.render.fps = FPS
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGB'
    scene.render.use_motion_blur = True
    scene.render.motion_blur_shutter = .18
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.exposure = -1.5
    scene.world = bpy.data.worlds.new(name+' environment')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.82,.85,.8,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .35
    try:
        prefs = bpy.context.preferences.addons['cycles'].preferences
        prefs.compute_device_type = 'OPTIX'
        prefs.get_devices()
        enabled = False
        for device in prefs.devices:
            device.use = device.type == 'OPTIX'
            enabled |= device.use
        if enabled:
            scene.cycles.device = 'GPU'
            print('RENDER GPU:', [(d.name,d.type,d.use) for d in prefs.devices], flush=True)
    except Exception as exc:
        print('CPU renderer fallback:', exc, flush=True)
    for label, position, energy, size, tint in [
        ('Large soft key',(-.22,-.22,.33),18,.22,(1,.94,.82)),
        ('Cool broad fill',(.22,-.06,.13),4,.20,(.9,.95,1)),
        ('Edge reflection',(.06,.16,.21),12,.14,(1,.96,.88)),
    ]:
        light = bpy.data.lights.new(label,'AREA')
        light.energy, light.size, light.color = energy, size, tint
        obj = bpy.data.objects.new(label,light)
        scene.collection.objects.link(obj)
        obj.location = position
        aim(obj,(0,0,.02))
    bpy.ops.mesh.primitive_plane_add(size=3, location=(0,.18,0), rotation=(math.pi/2,0,0))
    bpy.context.object.name = name+' seamless background'
    bpy.context.object.data.materials.append(backdrop)
    return scene


def camera(scene, location, target, lens=90, aperture=5.6):
    data = bpy.data.cameras.new(scene.name+' macro lens')
    obj = bpy.data.objects.new(data.name,data)
    scene.collection.objects.link(obj)
    obj.location = location
    data.lens, data.sensor_width = lens, 36
    aim(obj,target)
    focus = bpy.data.objects.new(scene.name+' focal plane',None)
    focus.location = target
    scene.collection.objects.link(focus)
    data.dof.use_dof = True
    data.dof.focus_object = focus
    data.dof.aperture_fstop = aperture
    data.dof.aperture_blades = 9
    scene.camera = obj
    return obj


shot_a = studio('01 - olive on a branch')
verts, faces = [], []
rings, slices = 80, 112
for i in range(rings+1):
    t = i/rings*math.pi
    for j in range(slices+1):
        a = j/slices*math.tau
        contour = 1-.075*math.cos(t)+.012*math.sin(a*3)*math.sin(t)
        verts.append((.011*math.sin(t)*math.cos(a)*contour,
                      .010*math.sin(t)*math.sin(a)*contour,
                      .018*math.cos(t)-.00045*math.exp(-t*t/.05)))
for i in range(rings):
    for j in range(slices):
        a = i*(slices+1)+j
        faces.append((a,a+slices+1,a+slices+2,a+1))
hero = mesh('Hero olive',verts,faces,olive)
tube('Olive stem',[(0,0,.0175),(.001,.001,.024),(.004,.003,.032)], .00055, twig_mat)
tube('Diagonal olive branch',[(-.055,.017,.044),(.004,.003,.032),(.058,.018,.047)], .0018, twig_mat)
leaf('Right olive leaf',(.004,.003,.032),(.8,.35,.05),.047,.0048)
leaf('Rear silver leaf',(-.015,.012,.037),(-.9,.7,.12),.044,.004)
leaf('Distant leaf',(.033,.018,.04),(.15,.8,.45),.052,.0045)
leaf('Soft background olive leaf',(.016,.018,.003),(.35,.2,.8),.044,.0045)
cam_a = camera(shot_a,(.012,-.155,.066),(0,0,.005),85,22)
cam_a.data.dof.focus_object.location = (.0005,-.009,.006)
cam_a.keyframe_insert('location',frame=1)
cam_a.location = (.008,-.144,.062)
cam_a.keyframe_insert('location',frame=24)

shot_b = studio('02 - oil forms on the press lip')
verts, faces = [], []
rows, columns = 20, 65
for i in range(rows):
    v = i/(rows-1)
    for j in range(columns):
        u = j/(columns-1)*2-1
        verts.append((.028*u,-.005+.008*u*u+.07*v,.035+.008*u*u+.002*v))
for i in range(rows-1):
    for j in range(columns-1):
        a = i*columns+j
        faces.append((a,a+1,a+columns+1,a+columns))
spout = mesh('Bevelled stainless press channel',verts,faces,metal)
solid = spout.modifiers.new('Steel gauge','SOLIDIFY')
solid.thickness = .0012
bevel = spout.modifiers.new('Rolled lip reflections','BEVEL')
bevel.width, bevel.segments = .00055, 4

verts, faces = [], []
for i in range(32):
    v = i/31
    for j in range(9):
        u = j/8*2-1
        x = .007*u*(.85+.08*math.sin(v*5))
        verts.append((x,-.005+.008*(x/.028)**2+.065*v,
                      .035+.008*(x/.028)**2+.002*v+.00022))
for i in range(31):
    for j in range(8):
        a = i*9+j
        faces.append((a,a+1,a+10,a+9))
film = mesh('Thin oil film in channel',verts,faces,oil)
solid = film.modifiers.new('Oil film thickness','SOLIDIFY')
solid.thickness = .00022

DROP_RINGS, DROP_SLICES = 64, 96


def drop_positions(frame):
    grow = smooth(25,51,frame)
    pinch = smooth(49,56,frame)
    radius = .00055+.00365*grow
    height = .0016+.0084*grow
    detached = frame >= 56
    seconds = max(0,(frame-56)/FPS)*.19
    fall = .5*9.81*seconds*seconds
    coords = []
    for i in range(DROP_RINGS+1):
        t = i/DROP_RINGS
        if detached:
            r = radius*math.sin(math.pi*t)*(1+.045*math.sin(seconds*160))
            z = .034-height*t-fall
        else:
            neck = .00065*(1-pinch)*math.exp(-t*10)
            r = radius*math.sin(math.pi*t)**.82*(.36+.8*t)+neck
            z = .0354-height*t
        for j in range(DROP_SLICES+1):
            angle = j/DROP_SLICES*math.tau
            base_y = -.0062 if detached else -.0052-.001*smooth(0,.35,t)
            coords.extend((r*math.cos(angle),base_y+r*math.sin(angle)*.94,z))
    return coords


initial = drop_positions(25)
verts = [initial[i:i+3] for i in range(0,len(initial),3)]
faces = []
for i in range(DROP_RINGS):
    for j in range(DROP_SLICES):
        a = i*(DROP_SLICES+1)+j
        faces.append((a,a+DROP_SLICES+1,a+DROP_SLICES+2,a+1))
faces.append(tuple(range(DROP_SLICES+1)))
drop = mesh('Pendant oil bead - formation, pinch and gravity',verts,faces,oil)
drop.shape_key_add(name='Basis')
for frame in range(25,73):
    key = drop.shape_key_add(name=f'Fluid pose {frame:02d}')
    key.data.foreach_set('co',drop_positions(frame))
    for f,value in [(frame-1,0),(frame,1),(frame+1,0)]:
        key.value = value
        key.keyframe_insert('value',frame=f)
if drop.data.shape_keys.animation_data:
    for curve in drop.data.shape_keys.animation_data.action.fcurves:
        for point in curve.keyframe_points:
            point.interpolation = 'LINEAR'
camera(shot_b,(.009,-.151,.073),(0,-.006,.027),90,7.1)

bpy.ops.wm.save_as_mainfile(filepath=str(out/'olive-to-oil-studio.blend'), compress=True)

if args.preview:
    for scene,frame,label in [(shot_a,12,'olive'),(shot_b,50,'oil'),(shot_b,63,'fall')]:
        bpy.context.window.scene = scene
        scene.render.resolution_percentage = 65
        scene.cycles.samples = min(args.samples,32)
        scene.frame_set(frame)
        scene.render.filepath = str(out/f'preview-{label}.png')
        bpy.ops.render.render(write_still=True)
        print('PREVIEW READY', scene.render.filepath, flush=True)
else:
    for frame in range(1,73):
        scene = shot_a if frame <= 24 else shot_b
        bpy.context.window.scene = scene
        scene.frame_set(frame)
        scene.render.filepath = str(frames/f'{frame:04d}.png')
        if not args.resume or not Path(scene.render.filepath).exists():
            bpy.ops.render.render(write_still=True)
        print(f'FRAME {frame}/72 READY',flush=True)
    export = bpy.data.scenes.new('Web export - silent H264')
    bpy.context.window.scene = export
    export.render.resolution_x, export.render.resolution_y = WIDTH, HEIGHT
    export.render.resolution_percentage = 100
    export.render.fps = FPS
    export.frame_start, export.frame_end = 1,72
    export.view_settings.view_transform = 'Standard'
    editor = export.sequence_editor_create()
    strips = getattr(editor,'strips',None) or editor.sequences
    strip = strips.new_image('Rendered macro sequence',str(frames/'0001.png'),channel=1,frame_start=1)
    for frame in range(2,73):
        strip.elements.append(f'{frame:04d}.png')
    strip.frame_final_duration = 72
    export.render.image_settings.file_format = 'FFMPEG'
    export.render.ffmpeg.format = 'MPEG4'
    export.render.ffmpeg.codec = 'H264'
    export.render.ffmpeg.constant_rate_factor = 'HIGH'
    export.render.ffmpeg.ffmpeg_preset = 'GOOD'
    export.render.filepath = str(out/'olive-to-oil-cinema.mp4')
    bpy.ops.render.render(animation=True)
    print('FILM READY', export.render.filepath, flush=True)
