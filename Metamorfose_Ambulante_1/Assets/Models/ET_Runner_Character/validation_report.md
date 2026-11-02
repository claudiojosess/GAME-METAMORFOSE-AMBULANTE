# ET Runner — relatório de validação

Gerado pelo Blender 5.1.2.

| Verificação | Resultado | Detalhes |
|---|---|---|
| Armature ET_Armature exists | OK | — |
| Humanoid core bone names present | OK | — |
| 13 named animation actions | OK | — |
| 14 facial shape key channels | OK | 14/14 present (prototype channels) |
| Main mesh collection populated | OK | 96 objects |
| Main collection triangulated budget (<40k tris) | OK | 38872 estimated triangles |
| UV map on each mesh | OK | — |
| At most 4 influences and no unweighted vertices | OK | Rigid one-bone weights are used in this prototype |
| No n-gons | OK | 0 faces over 4 sides |
| Scale settings metric | OK | METRIC |
| Relative Unity output FBX/GLB exist | OK | — |
| FBX reimport in Blender 5.1.2 | OK | Reopened in an empty scene; 97 objects |
| GLB reimport in Blender 5.1.2 | OK | Reopened in an empty scene; 98 objects |

## Limitações conhecidas

- Personagem de protótipo montado por volumes orgânicos separados; não tem retopologia quad-dominante nem escultura de poros/veias.
- Shape keys têm canais com nomes corretos, mas ainda não deformam os vértices.
- Pesos são rígidos por peça, sem transições anatômicas entre articulações; as Actions são poses/procedurais iniciais e precisam de revisão artística.
- Materiais PBR usam cores e nós procedurais do Blender; não há atlas/bakes de texturas.
- LOD1/LOD2 estão organizados em coleções com Decimate configurável e ocultos na cena para evitar duplicação no render.
- Faça revisão do Avatar Humanoid e dos clipes no Unity antes de uso em produção.
