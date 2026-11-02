"""Structural checks for the ET runner prototype. Writes validation_report.md."""
import bpy, os, math
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
checks=[]
def check(label,ok,detail=''):
    checks.append((label,bool(ok),detail))
scene=bpy.context.scene
arm=bpy.data.objects.get('ET_Armature')
check('Armature ET_Armature exists',bool(arm))
required=['Hips','Spine','Chest','Neck','Head','UpperArm_L','LowerArm_L','Hand_L','UpperArm_R','LowerArm_R','Hand_R','UpperLeg_L','LowerLeg_L','Foot_L','Toes_L','UpperLeg_R','LowerLeg_R','Foot_R','Toes_R']
missing=[n for n in required if not arm or n not in arm.data.bones]
check('Humanoid core bone names present',not missing,', '.join(missing))
expected={'ET_Idle':90,'ET_Start':30,'ET_Run':24,'ET_Sprint':20,'ET_Jump':36,'ET_Land':18,'ET_Slide':30,'ET_Roll':30,'ET_StrafeLeft':14,'ET_StrafeRight':14,'ET_Hit':24,'ET_Death':60,'ET_Celebrate':48}
missing_actions=[n for n in expected if not bpy.data.actions.get(n)]
check('13 named animation actions',not missing_actions,', '.join(missing_actions))
shape_names=['Blink_L','Blink_R','Blink','EyeWide','EyeSquint','BrowUp','BrowDown','Smile','Frown','MouthOpen','MouthO','Surprise','Pain','Focus']
head=bpy.data.objects.get('ET_Head'); have=set(head.data.shape_keys.key_blocks.keys()) if head and head.data.shape_keys else set()
check('14 facial shape key channels',set(shape_names).issubset(have),f'{len(have.intersection(shape_names))}/14 present (prototype channels)')
meshcoll=bpy.data.collections.get('ET_Meshes'); objects=list(meshcoll.objects) if meshcoll else []
check('Main mesh collection populated',bool(objects),f'{len(objects)} objects')
alltris=0; unweighted=[]; over4=[]; ngons=0; uv_missing=[]
for ob in objects:
    if ob.type!='MESH':continue
    me=ob.data
    alltris += sum(max(0,len(p.vertices)-2) for p in me.polygons)
    ngons += sum(len(p.vertices)>4 for p in me.polygons)
    if not me.uv_layers:uv_missing.append(ob.name)
    if arm:
        groups={g.index:g.name for g in ob.vertex_groups}
        for v in me.vertices:
            weights=[x.weight for x in v.groups if x.weight>0]
            if not weights:unweighted.append(ob.name);break
            if len(weights)>4:over4.append(ob.name);break
check('Main collection triangulated budget (<40k tris)',alltris<=40000,f'{alltris} estimated triangles')
check('UV map on each mesh',not uv_missing,', '.join(uv_missing[:8]))
check('At most 4 influences and no unweighted vertices',not unweighted and not over4,'Rigid one-bone weights are used in this prototype')
check('No n-gons',ngons==0,f'{ngons} faces over 4 sides')
check('Scale settings metric',scene.unit_settings.system=='METRIC',scene.unit_settings.system)
check('Relative Unity output FBX/GLB exist',os.path.exists(os.path.join(ROOT,'ET_Runner.fbx')) and os.path.exists(os.path.join(ROOT,'ET_Runner.glb')))
lines=['# ET Runner — relatório de validação','',f'Gerado pelo Blender {bpy.app.version_string}.','', '| Verificação | Resultado | Detalhes |','|---|---|---|']
for label,ok,detail in checks: lines.append(f"| {label} | {'OK' if ok else 'PENDENTE'} | {detail or '—'} |")
lines += ['', '## Limitações conhecidas','', '- Personagem de protótipo montado por volumes orgânicos separados; não tem retopologia quad-dominante nem escultura de poros/veias.','- Shape keys têm canais com nomes corretos, mas ainda não deformam os vértices.','- Pesos são rígidos por peça, sem transições anatômicas entre articulações; as Actions são poses/procedurais iniciais e precisam de revisão artística.','- Materiais PBR usam cores e nós procedurais do Blender; não há atlas/bakes de texturas.','- LOD1/LOD2 estão organizados em coleções com Decimate configurável e ocultos na cena para evitar duplicação no render.','- Faça revisão do Avatar Humanoid e dos clipes no Unity antes de uso em produção.','']
out=os.path.join(ROOT,'validation_report.md'); open(out,'w',encoding='utf-8').write('\n'.join(lines))
print('\n'.join(lines))
