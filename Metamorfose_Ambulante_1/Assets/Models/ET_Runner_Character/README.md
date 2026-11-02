# ET Runner — personagem editável

Pacote inicial de personagem original para o projeto **Metamorfose Ambulante**. A imagem de referência orienta a silhueta alta e esguia, cabeça grande, olhos escuros e gesto de paz. O modelo não reproduz um personagem de filme.

## Abrir e editar

Abra `ET_Runner.blend` no Blender 5.1.2 (compatível com a linha 5.1). A cena está em metros, com o eixo frontal em `-Y` e os pés próximos a `Z=0`. As peças estão separadas e nomeadas em `ET_Meshes`; colete e mochila também são peças independentes. A armature `ET_Armature` e as coleções `ET_LOD1`, `ET_LOD2`, `ET_Controls` e `ET_Preview` deixam clara a organização do projeto.

Para reconstruir a cena, a partir desta pasta:

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" --background --python scripts/build_et.py
```

O script atualiza `ET_Runner.blend` e renderiza `previews/ET_Runner_3-4.png`. Edite `scripts/build_et.py` para alterações reproduzíveis ou faça mudanças diretamente no `.blend`.

## Exportar para Unity 6

Com o `.blend` salvo, rode:

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" --background ET_Runner.blend --python scripts/export_unity.py
```

Isso gera `ET_Runner.fbx` e `ET_Runner.glb` na pasta do personagem. Importe a pasta em `Assets/Models/ET_Runner_Character/` ou copie o FBX para `Assets/Characters/ET/`. No Unity, escolha **Rig > Humanoid**, confirme o mapeamento do Avatar, habilite **Import BlendShapes**, e revise escala, materiais e clipes. O Unity poderá exigir ajustes manuais de mapeamento porque a anatomia alienígena e a convenção de nomes ainda não equivalem a um rig Humanoid final.

## Conteúdo e estado atual

- `ET_Runner.blend`: cena-fonte editável.
- `ET_Runner.fbx` / `ET_Runner.glb`: exports destinados a avaliação/importação no Unity.
- `scripts/build_et.py`: gera o modelo, rig, materiais, ações e prévia.
- `scripts/export_unity.py`: exporta as malhas e armature principais.
- `scripts/validate_et.py`: verifica a estrutura e escreve `validation_report.md`.
- `previews/`: render de apresentação e imagem de referência.

Este arquivo é uma **base de prototipagem**, não um personagem AAA pronto para produção. A geometria usa volumes orgânicos separados, pesos rígidos por peça e materiais procedurais. Os 14 canais de shape keys faciais existem como ponto de partida, sem deformações esculpidas. As Actions são movimentos iniciais para revisão; LODs têm modificadores Decimate configuráveis. Ainda faltam retopologia quad-dominante, UVs/atlas PBR e mapas bakeados, pesos suaves nas juntas, blend shapes funcionais e polimento/validação de cada animação. Leia `validation_report.md` antes de integrar no jogo.

Validação executada com Blender 5.1.2: 38.872 triângulos estimados em LOD0; FBX e GLB foram reimportados em uma cena vazia (97 e 98 objetos, respectivamente). A validação do Avatar Humanoid e do comportamento dos clipes ainda deve ser feita dentro do Unity 6.

## Próximo passo artístico recomendado

No Blender, ajuste a pose da mão levantada para a silhueta de paz desejada, modele transições dos ombros/joelhos, esculpa e retopologize a pele, faça UV unwrap e bake PBR. Depois teste `ET_Run` em loop e o Avatar no Unity, corrija o retargeting se necessário e regenere os exports.
