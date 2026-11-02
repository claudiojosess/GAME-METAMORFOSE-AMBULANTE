"""Export ET_Runner.fbx and ET_Runner.glb from the saved Blender scene."""
import bpy, os
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FBX=os.path.join(ROOT,'ET_Runner.fbx'); GLB=os.path.join(ROOT,'ET_Runner.glb')
for o in bpy.context.selected_objects:o.select_set(False)
export_objects=[]
for name in ['ET_Armature','ET_Meshes']:
    c=bpy.data.collections.get(name)
    if c:
        export_objects.extend([o for o in c.objects if o.type in {'ARMATURE','MESH'}])
for o in export_objects:o.hide_set(False); o.select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects.get('ET_Armature')
op=bpy.ops.export_scene.fbx
props={p.identifier for p in op.get_rna_type().properties}
opts={
 'filepath':FBX,'use_selection':True,'object_types':{'ARMATURE','MESH'},
 'apply_scale_options':'FBX_SCALE_UNITS','global_scale':1.0,'apply_unit_scale':True,
 'use_space_transform':True,'bake_space_transform':True,'axis_forward':'-Z','axis_up':'Y',
 'mesh_smooth_type':'FACE','use_tspace':True,'use_mesh_modifiers':True,
 'add_leaf_bones':False,'primary_bone_axis':'Y','secondary_bone_axis':'X',
 'use_armature_deform_only':True,'bake_anim':True,'bake_anim_use_all_actions':True,
 'bake_anim_use_nla_strips':False,'bake_anim_force_startend_keying':True,
 'bake_anim_simplify_factor':0.0,'bake_anim_step':1.0,'path_mode':'COPY',
 'embed_textures':False,'colors_type':'SRGB'
}
opts={k:v for k,v in opts.items() if k in props}
op(**opts)
print('FBX_OK',FBX,os.path.getsize(FBX))
# glTF exports the active scene and all named actions; hidden LOD and preview collections stay out.
for o in bpy.context.selected_objects:o.select_set(False)
for o in export_objects:o.select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects.get('ET_Armature')
bpy.ops.export_scene.gltf(filepath=GLB,export_format='GLB',use_selection=True,export_animations=True,export_skins=True,export_morph=True,export_nla_strips=False,export_frame_range=True)
print('GLB_OK',GLB,os.path.getsize(GLB))
