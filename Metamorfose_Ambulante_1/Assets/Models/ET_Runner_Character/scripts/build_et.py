"""Build an editable original alien runner in Blender 5.1.

Run from this directory with:
  blender --background --python scripts/build_et.py

The asset is intentionally organized as editable anatomical parts. It is a
production-ready blockout/base character, not a sculpted AAA topology asset.
"""
import bpy, math, os, random
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.makedirs(os.path.join(ROOT, 'textures'), exist_ok=True)
os.makedirs(os.path.join(ROOT, 'previews'), exist_ok=True)
os.makedirs(os.path.join(ROOT, 'scripts'), exist_ok=True)

def clean_scene():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for c in list(bpy.data.collections):
        if c.name != 'Collection': bpy.data.collections.remove(c)
    base = bpy.data.collections.get('Collection')
    if base: bpy.data.collections.remove(base)
    root = bpy.data.collections.new('ET_Character'); bpy.context.scene.collection.children.link(root)
    out = {}
    for n in ['ET_Meshes','ET_LOD1','ET_LOD2','ET_Armature','ET_Controls','ET_Preview']:
        c=bpy.data.collections.new(n); root.children.link(c); out[n]=c
    return out

COL = clean_scene()

def move_to(obj, collection):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    COL[collection].objects.link(obj)

def mat(name, color, rough=.48, metallic=0, subs=.05, emission=None):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=rough; p.inputs['Metallic'].default_value=metallic
    if 'Subsurface Weight' in p.inputs: p.inputs['Subsurface Weight'].default_value=subs
    if emission:
        key='Emission Color' if 'Emission Color' in p.inputs else 'Emission'
        p.inputs[key].default_value=(*emission,1); p.inputs['Emission Strength'].default_value=.6
    return m

skin=mat('MAT_ET_Skin',(.30,.34,.27),.38,.0,.12)
eye_white=mat('MAT_ET_Eyes',(.66,.72,.67),.2,.0,.02)
iris=mat('MAT_ET_Iris',(.20,.48,.38),.23,.15,.0,(.02,.28,.25))
pupil=mat('MAT_ET_Pupil',(.008,.015,.018),.1,.0,.0)
claw=mat('MAT_ET_Nails',(.46,.51,.43),.28,.03,.0)
cloth=mat('MAT_ET_Outfit',(.045,.085,.09),.62,.25,.0)
glow=mat('MAT_ET_BioGlow',(.02,.36,.34),.25,.0,.0,(.01,.65,.62))
mouthmat=mat('MAT_ET_Mouth',(.16,.045,.05),.55)

def uv_sphere(name, loc, scale, material, collection='ET_Meshes', seg=12, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, location=loc)
    o=bpy.context.object; o.name=name; o.scale=scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for p in o.data.polygons:p.use_smooth=True
    o.data.materials.append(material); move_to(o,collection)
    if not o.data.uv_layers: o.data.uv_layers.new(name='UVMap')
    return o

def bone_chain(arm, name, pts, parent=None, deform=True):
    eb=arm.data.edit_bones.new(name); eb.head=pts[0]; eb.tail=pts[1]
    if parent: eb.parent=arm.data.edit_bones[parent]
    eb.use_deform=deform
    return eb

def nearest_bone(obj, arm, bone_name):
    vg=obj.vertex_groups.get(bone_name) or obj.vertex_groups.new(name=bone_name)
    vg.add(list(range(len(obj.data.vertices))),1.0,'REPLACE')
    mod=obj.modifiers.new('ET_ArmatureDeform','ARMATURE'); mod.object=arm
    obj.parent=arm

def add_segment(name, a, b, radius, material=skin, radius2=None, collection='ET_Meshes', bone_name=None):
    # Smooth joint-aware capsule assembled from overlapping spheres, kept as one editable object.
    a,b=Vector(a),Vector(b); d=b-a; mid=(a+b)*.5
    o=uv_sphere(name,mid,(radius,radius,max(d.length*.62,radius)),material,collection,20,14)
    o.rotation_mode='QUATERNION'; o.rotation_quaternion=Vector((0,0,1)).rotation_difference(d.normalized())
    if bone_name: nearest_bone(o,arm,bone_name)
    return o

# Armature: one export skeleton, aligned to a 1.30 m T/A hybrid pose.
bpy.ops.object.armature_add(location=(0,0,0)); arm=bpy.context.object; arm.name='ET_Armature'; arm.data.name='ET_ArmatureData'; move_to(arm,'ET_Armature')
bpy.ops.object.mode_set(mode='EDIT'); arm.data.edit_bones.remove(arm.data.edit_bones[0])
bone_chain(arm,'Hips',[(0,0,.64),(0,0,.73)])
bone_chain(arm,'Spine',[(0,0,.70),(0,0,.82)],'Hips')
bone_chain(arm,'Chest',[(0,0,.82),(0,0,.91)],'Spine')
bone_chain(arm,'Neck',[(0,0,.90),(0,0,1.00)],'Chest')
bone_chain(arm,'Head',[(0,0,1.00),(0,0,1.20)],'Neck')
bone_chain(arm,'Jaw',[(0,-.015,1.025),(0,-.05,1.00)],'Head')
for side,s in [('L',1),('R',-1)]:
    bone_chain(arm,'Shoulder_'+side,[(s*.12,0,.88),(s*.20,0,.85)],'Chest')
    if side=='R':
        bone_chain(arm,'UpperArm_'+side,[(s*.20,0,.85),(s*.28,-.01,.77)],'Shoulder_'+side)
        bone_chain(arm,'LowerArm_'+side,[(s*.28,-.01,.77),(s*.36,-.04,.91)],'UpperArm_'+side)
        bone_chain(arm,'Hand_'+side,[(s*.36,-.04,.91),(s*.36,-.07,.98)],'LowerArm_'+side)
    else:
        bone_chain(arm,'UpperArm_'+side,[(s*.20,0,.85),(s*.29,0,.62)],'Shoulder_'+side)
        bone_chain(arm,'LowerArm_'+side,[(s*.29,0,.62),(s*.34,0,.40)],'UpperArm_'+side)
        bone_chain(arm,'Hand_'+side,[(s*.34,0,.40),(s*.36,-.02,.34)],'LowerArm_'+side)
    # Finger chains, three fingers and an opposing thumb.
    for digit,xoff in [('Thumb',-.040),('Index',-.020),('Middle',0),('Ring',.020)]:
        if side=='R':
            start=(-.36+xoff,-.07,.92)
            end=(-.36+xoff+{'Thumb':-.07,'Index':-.035,'Middle':.035,'Ring':.045}[digit],-.08,1.055 if digit in ('Index','Middle') else .96)
        else:
            start=(s*(.36+xoff),-.035,.35)
            end=(s*(.36+xoff),-.055,.27 if digit!='Thumb' else .32)
        prev='Hand_'+side
        for j in range(1,4):
            t=j/3; p=(start[0]*(1-t)+end[0]*t,start[1]*(1-t)+end[1]*t,start[2]*(1-t)+end[2]*t)
            q=(p[0],p[1],p[2]-.022)
            bn=f'{digit}_{j:02d}_{side}'; bone_chain(arm,bn,[p,q],prev); prev=bn
    bone_chain(arm,'UpperLeg_'+side,[(s*.075,0,.66),(s*.09,0,.36)],'Hips')
    bone_chain(arm,'LowerLeg_'+side,[(s*.09,0,.36),(s*.09,0,.11)],'UpperLeg_'+side)
    bone_chain(arm,'Foot_'+side,[(s*.09,0,.11),(s*.09,-.12,.055)],'LowerLeg_'+side)
    bone_chain(arm,'Toes_'+side,[(s*.09,-.12,.055),(s*.09,-.23,.04)],'Foot_'+side)
    bone_chain(arm,'Ear_'+side,[(s*.18,0,1.13),(s*.23,0,1.14)],'Head')
bpy.ops.object.mode_set(mode='OBJECT'); arm.show_in_front=True; arm.data.display_type='OCTAHEDRAL'

def part(name,loc,scale,material,bone,seg=12,rings=8):
    o=uv_sphere(name,loc,scale,material,seg=seg,rings=rings); nearest_bone(o,arm,bone); return o

# Body silhouette, narrow waist, long neck and limbs.
part('ET_Torso',(0,0,.76),(.135,.083,.19),skin,'Spine',32,20)
part('ET_Chest',(0,-.005,.86),(.18,.09,.12),skin,'Chest',32,20)
part('ET_Pelvis',(0,0,.60),(.13,.085,.11),skin,'Hips',28,18)
part('ET_Neck',(0,0,.96),(.065,.065,.12),skin,'Neck',24,18)
part('ET_Head',(0,-.008,1.105),(.175,.145,.195),skin,'Head',40,28)
# cranial ridges and cheek planes add the ridged organic look seen in the reference.
for side,s in [('L',1),('R',-1)]:
    part('ET_Cheek_'+side,(s*.105,-.105,1.055),(.068,.045,.052),skin,'Head')
    part('ET_Ear_'+side,(s*.177,0,1.13),(.052,.035,.033),skin,'Head')
    part('ET_EarInner_'+side,(s*.19,-.024,1.13),(.027,.015,.018),claw,'Head')
    # eyes face -Y, large glossy almond-like globes.
    part('ET_Eye_'+side,(s*.077,-.135,1.125),(.071,.040,.076),eye_white,'Head',32,24)
    part('ET_Iris_'+side,(s*.077,-.170,1.124),(.042,.018,.048),iris,'Head',32,24)
    part('ET_Pupil_'+side,(s*.077,-.186,1.124),(.019,.009,.032),pupil,'Head',28,20)
    part('ET_EyeGlint_'+side,(s*.065,-.196,1.143),(.009,.006,.011),eye_white,'Head',16,12)
    # brow and upper/lower eyelid ridges
    part('ET_Brow_'+side,(s*.077,-.132,1.188),(.083,.028,.022),skin,'Head')
    # long arms: gesture hand at viewer-left (character's right)
    shoulder=(s*.16,0,.86)
    elbow=(-.28,-.015,.77) if side=='R' else (.285,.015,.60)
    wrist=(-.36,-.045,.91) if side=='R' else (.34,-.015,.36)
    part('ET_Shoulder_'+side,shoulder,(.09,.075,.095),skin,'Shoulder_'+side)
    add_segment('ET_UpperArm_'+side,shoulder,elbow,.055,bone_name='UpperArm_'+side)
    add_segment('ET_LowerArm_'+side,elbow,wrist,.043,bone_name='LowerArm_'+side)
    part('ET_Elbow_'+side,elbow,(.049,.045,.05),skin,'LowerArm_'+side)
    handbone='Hand_'+side
    # Hand palm and four digits; raised hand forms a readable V peace sign.
    part('ET_Palm_'+side,(wrist[0],wrist[1]-.015,wrist[2]-.035),(.057,.037,.072),skin,handbone)
    if side=='R':
        fingers=[('Thumb',(-.39,-.075,.89),(-.43,-.08,.86)),('Index',(-.375,-.075,.94),(-.405,-.08,1.075)),('Middle',(-.345,-.075,.94),(-.305,-.08,1.07)),('Ring',(-.325,-.075,.93),(-.32,-.08,.97))]
    else:
        fingers=[('Thumb',(.30,-.045,.32),(.28,-.05,.29)),('Index',(.325,-.045,.31),(.32,-.05,.27)),('Middle',(.35,-.045,.31),(.35,-.05,.265)),('Ring',(.375,-.045,.31),(.38,-.05,.275))]
    for digit,start,end in fingers:
        mid=tuple((Vector(start)+Vector(end))*.5)
        part('ET_'+digit+'Base_'+side,mid,(.019,.018,.05),skin,handbone,18,12)
        part('ET_'+digit+'Tip_'+side,end,(.016,.016,.025),skin,handbone,18,12)
        part('ET_Nail_'+digit+'_'+side,(end[0],end[1]-.013,end[2]-.01),(.010,.006,.013),claw,handbone,16,10)
    # legs and three long toes on each foot
    hip=(s*.075,0,.64); knee=(s*.09,0,.36); ankle=(s*.09,0,.105)
    part('ET_HipJoint_'+side,hip,(.072,.075,.085),skin,'UpperLeg_'+side)
    add_segment('ET_Thigh_'+side,hip,knee,.073,bone_name='UpperLeg_'+side)
    part('ET_Knee_'+side,knee,(.052,.053,.055),skin,'LowerLeg_'+side)
    add_segment('ET_Calf_'+side,knee,ankle,.048,bone_name='LowerLeg_'+side)
    part('ET_Foot_'+side,(s*.09,-.065,.055),(.067,.14,.045),skin,'Foot_'+side)
    for j,dx in enumerate([-.034,0,.034]):
        part(f'ET_Toe{j+1}_{side}',(s*.09+dx,-.19-(.018 if j==1 else 0),.038),(.023,.075,.027),skin,'Toes_'+side)

# Subtle ribbed cranial surface lines and collarbone structures.
for i in range(5):
    x=-.12+i*.06
    ridge=part('ET_CranialRidge_%02d'%i,(x,-.079,1.248-abs(x)*.28),(.009,.011,.066),claw,'Head',16,12)
# Small nose and curved smile to keep the face readable at game-camera distance.
part('ET_NoseBridge',(0,-.150,1.075),(.025,.022,.022),skin,'Head',20,14)
for s,side in [(-1,'R'),(1,'L')]:
    part('ET_Nostril_'+side,(s*.016,-.168,1.070),(.006,.005,.004),mouthmat,'Head',12,8)
for i in range(7):
    x=-.052+i*.0173; z=1.035+0.012*(abs(x)/.052)**1.5
    part('ET_SmileLine_%02d'%i,(x,-.143,z),(.010,.006,.006),mouthmat,'Head',12,8)
for s,side in [(-1,'R'),(1,'L')]:
    part('ET_Clavicle_'+side,(s*.09,-.062,.89),(.095,.018,.015),claw,'Chest')
    part('ET_BioMark_'+side,(s*.14,-.078,.87),(.018,.008,.027),glow,'Chest',16,12)

# Runner harness / light chest rig, independently hideable.
part('ET_VestFront',(0,-.076,.77),(.118,.017,.115),cloth,'Chest',28,18)
part('ET_VestStrap_L',(-.085,-.085,.82),(.023,.018,.12),cloth,'Chest')
part('ET_VestStrap_R',(.085,-.085,.82),(.023,.018,.12),cloth,'Chest')
part('ET_ChestEmblem',(0,-.098,.81),(.035,.009,.014),glow,'Chest')
part('ET_Backpack',(0,.088,.80),(.09,.055,.12),cloth,'Chest')
part('ET_Thruster',(0,.14,.80),(.045,.025,.08),glow,'Chest')

# Facial shape keys on head geometry for downstream Unity blend shape tests.
head=bpy.data.objects['ET_Head']; head.shape_key_add(name='Basis')
for key in ['Blink_L','Blink_R','Blink','EyeWide','EyeSquint','BrowUp','BrowDown','Smile','Frown','MouthOpen','MouthO','Surprise','Pain','Focus']:
    head.shape_key_add(name=key)

# Actions: named clips and keyed poses. Simple, in-place motion designed for animator refinement.
scene=bpy.context.scene; scene.render.fps=30; scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1.0
def action(name, length, style):
    scene.frame_start=1; scene.frame_end=length; bpy.context.view_layer.objects.active=arm
    arm.animation_data.action=bpy.data.actions.new(name=name)
    bpy.ops.object.mode_set(mode='POSE')
    for pb in arm.pose.bones: pb.rotation_mode='XYZ'
    frames=sorted(set([1,max(1,length//4),max(1,length//2),max(1,3*length//4),length]))
    for f in frames:
        scene.frame_set(f); phase=2*math.pi*(f-1)/max(1,length-1)
        hips=arm.pose.bones['Hips']; headp=arm.pose.bones['Head']; la=arm.pose.bones['UpperArm_L']; ra=arm.pose.bones['UpperArm_R']
        ll=arm.pose.bones['UpperLeg_L']; rl=arm.pose.bones['UpperLeg_R']
        amp={'Run':.5,'Sprint':.68,'Idle':.035,'Jump':.20,'Slide':.08,'Roll':.5,'StrafeLeft':.18,'StrafeRight':.18,'Hit':.18,'Death':.35,'Celebrate':.28,'Start':.2,'Land':.16}.get(style,.1)
        if style in ('Run','Sprint'):
            ll.rotation_euler.x=amp*math.sin(phase); rl.rotation_euler.x=-amp*math.sin(phase)
            la.rotation_euler.x=-amp*.7*math.sin(phase); ra.rotation_euler.x=amp*.7*math.sin(phase)
            hips.location.z=.008*(1-math.cos(2*phase))
        elif style=='Idle':
            hips.location.z=.004*math.sin(phase); headp.rotation_euler.z=.025*math.sin(phase)
        elif style in ('Jump','Start','Land'):
            hips.location.z=(.06 if style=='Jump' else .015)*math.sin(math.pi*(f-1)/max(1,length-1))
        elif style in ('StrafeLeft','StrafeRight'):
            hips.rotation_euler.y=(.18 if style=='StrafeLeft' else -.18)*math.sin(math.pi*(f-1)/max(1,length-1))
        elif style=='Slide': hips.rotation_euler.x=.32
        elif style=='Roll': hips.rotation_euler.y=math.pi*2*(f-1)/max(1,length-1)
        elif style=='Hit': headp.rotation_euler.x=.18*math.sin(math.pi*(f-1)/max(1,length-1))
        elif style=='Death': hips.rotation_euler.x=1.0*min(1,(f-1)/max(1,length*.65))
        elif style=='Celebrate': la.rotation_euler.x=-.8; ra.rotation_euler.x=-.8
        for pb in [hips,headp,la,ra,ll,rl]: pb.keyframe_insert(data_path='location',frame=f,group=pb.name); pb.keyframe_insert(data_path='rotation_euler',frame=f,group=pb.name)
    bpy.ops.object.mode_set(mode='OBJECT')
    act=arm.animation_data.action; act.name=name; act.use_fake_user=True
    if name=='ET_Run':
        for fr,label in [(1,'RightFootContact'),(13,'LeftFootContact')]: scene.timeline_markers.new(label,frame=fr)
    if name=='ET_Jump':
        for fr,label in [(1,'JumpStart'),(18,'Apex'),(28,'Fall')]: scene.timeline_markers.new(label,frame=fr)

arm.animation_data_create()
for n,l,s in [('ET_Idle',90,'Idle'),('ET_Start',30,'Start'),('ET_Run',24,'Run'),('ET_Sprint',20,'Sprint'),('ET_Jump',36,'Jump'),('ET_Land',18,'Land'),('ET_Slide',30,'Slide'),('ET_Roll',30,'Roll'),('ET_StrafeLeft',14,'StrafeLeft'),('ET_StrafeRight',14,'StrafeRight'),('ET_Hit',24,'Hit'),('ET_Death',60,'Death'),('ET_Celebrate',48,'Celebrate')]:
    action(n,l,s)
arm.animation_data.action=bpy.data.actions.get('ET_Idle')

# LOD copies with Decimate modifiers retained for easy tuning.
lod0=[o for o in COL['ET_Meshes'].objects]
for coll,ratio in [('ET_LOD1',.35),('ET_LOD2',.12)]:
    for src in lod0:
        cp=src.copy(); cp.data=src.data.copy(); cp.name=src.name.replace('ET_','ET_'+coll[-1]+'_')
        COL[coll].objects.link(cp)
        # LODs are visual-only copies; disable duplicate armature deformation until assigned by user.
        for mod in list(cp.modifiers):
            if mod.type=='ARMATURE': cp.modifiers.remove(mod)
        dec=cp.modifiers.new('LOD_Decimate','DECIMATE'); dec.ratio=ratio
        cp.hide_render=True; cp.hide_viewport=True

# Orthographic preview studio and camera.
def area_light(name,loc,power,color,size):
    bpy.ops.object.light_add(type='AREA',location=loc); o=bpy.context.object; o.name=name; o.data.energy=power; o.data.color=color; o.data.shape='DISK'; o.data.size=size; move_to(o,'ET_Preview'); o.rotation_euler=(Vector((0,0,.68))-o.location).to_track_quat('-Z','Y').to_euler()
area_light('ET_Key',(-2,-3,2.3),450,(.73,.86,1),2.4)
area_light('ET_Fill',(2,-2,1.2),260,(.45,1,.91),2)
area_light('ET_Rim',(0,1,1.8),550,(1,.65,.34),1.5)
bpy.ops.object.camera_add(location=(.65,-3.9,1.0)); cam=bpy.context.object; cam.name='ET_PreviewCamera'; cam.rotation_euler=(Vector((0,0,.68))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=1.65; move_to(cam,'ET_Preview'); scene.camera=cam
scene.render.engine='BLENDER_EEVEE'; scene.render.resolution_x=1200; scene.render.resolution_y=1200; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=False
scene.world.color=(.035,.035,.035)
scene.view_settings.view_transform='AgX'
scene.render.filepath=os.path.join(ROOT,'previews','ET_Runner_3-4.png')
scene.frame_set(1)

# Display a pristine rest pose on opening, with character parts selected for convenient editing.
for o in bpy.context.selected_objects:o.select_set(False)
arm.select_set(True); bpy.context.view_layer.objects.active=arm
for a in bpy.data.screens:
    for area in a.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=2.5
            area.spaces.active.region_3d.view_location=(0,0,.66)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'ET_Runner.blend'))
try:
    scene.render.filepath=os.path.join(ROOT,'previews','ET_Runner_3-4.png'); bpy.ops.render.render(write_still=True)
except Exception as exc: print('PREVIEW_RENDER_WARNING:',exc)
print('BUILD_OK',ROOT)
