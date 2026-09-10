# 14-GpuPbr

`13-GpuSkinnedInstancing`과 같은 GPU 스키닝 파이프라인(본 팔레트 SSBO) 위에, 알베도 한 장뿐이던 재질을 Dota 2 히어로가 실제로 쓰는 텍스처 4장(알베도, 노멀, 마스크1, 마스크2)으로 확장하고 그 채널들을 실제 셰이더와 같은 의미로 해석해 조명합니다. 그리고 같은 히어로를 여러 번 찍어내던 `13`번과 달리, `scripts/npc/npc_heroes.txt`(실제 게임 데이터)에서 매번 다른 히어로 7명을 무작위로 골라(Axe는 항상 포함) 나란히 배치합니다.

## 실행

1. `config.sample.ini`를 `config.ini`로 복사하고 `[paths] game_root`를 설정합니다. `[characters] count`/`spacing`으로 배치할 히어로 수와 간격을 조절하고, `seed`를 지정하면 매번 같은 조합이 재현됩니다.
2. `dotnet run --project Chapter3/14-GpuPbr/14-GpuPbr.csproj`
3. **File > Reroll Heroes**로 재시작 없이 새 무작위 조합을 다시 뽑을 수 있습니다.

## 여러 명의 서로 다른 히어로 (`13`번과의 구조 차이)

`13-GpuSkinnedInstancing`은 **같은 히어로를 N번 반복**하는 것이었으므로, 인스턴스마다 다른 포즈만 다르고 지오메트리(정점/인덱스 버퍼)는 완전히 같아서 `GL.DrawElementsInstanced` 한 번으로 N개를 그릴 수 있었습니다. 이 예제는 히어로 자체가 서로 다르므로(액스와 메두사는 정점 수도, 본 개수도 다릅니다) 같은 방식의 배치 인스턴싱은 성립하지 않습니다 — 그래서 구조를 이렇게 바꿨습니다.

- `Character`: 히어로 한 명. 자신의 `Roles`(본체 + 기본 웨어러블들)와, 자신이 서 있는 위치 행렬 하나만 담은 `InstanceBuffer`(SSBO)를 가집니다.
- `ModelRole`: `13`번의 `ModelRole`과 이름은 같지만 이제 인스턴스 배열이 아니라 **캐릭터 한 명 몫**입니다 — `Animator?` 하나, 본 팔레트 SSBO 하나(크기 = 그 역할의 본 개수, 인스턴스 1개분).
- 렌더링은 여전히 `Mesh.DrawInstanced(1)`을 씁니다 — SSBO 인덱싱(`gl_InstanceID`)과 셰이더 코드를 `13`번과 그대로 재사용하기 위해 인스턴스 수 1인 특수한 경우로 다루는 것으로, `GL.DrawElements`로 되돌리지 않았습니다.

`GLView.LoadCharacters`가 매 로드마다 하는 일:

1. `HeroRoster.LoadAll(archive)`로 `scripts/npc/npc_heroes.txt`(KeyValues1)를 읽어 **실제** 플레이 가능한 히어로 목록(npc 이름 → 모델 경로)을 얻습니다. 이게 필요한 이유: 여러 히어로의 내부 모델 폴더 이름이 실제 이름과 다릅니다(`bloodseeker`→`blood_seeker`, `drow_ranger`→`drow`, `nevermore`→`shadow_fiend` 등) — 이름을 추측해 하드코딩하면 틀리기 쉬워서, 게임 자신의 데이터에서 읽습니다.
2. Axe를 목록에서 찾아 항상 포함시키고, 나머지에서 `config.CharacterCount - 1`명을 무작위로 더 뽑습니다.
3. 선택된 히어로마다 `LoadCharacter`가 본체+`HeroLoadout.LoadDefaultLoadout(archive, hero.NpcName)`(이번엔 모델 경로에서 npc 이름을 추정할 필요 없이, 로스터가 이미 정확한 npc 이름을 알려줍니다)로 기본 웨어러블을 로드하고, 격자 위치(열/행 기반, `SourceToWorldUp`으로 축 변환까지 합성)에 배치합니다.
4. 카메라 프레이밍용 전체 바운딩 박스는 각 히어로의 로컬 바운딩 박스를 그 히어로의 배치 행렬로 변환해 합집합을 구합니다(`10~13`번의 단일/그리드 바운딩 계산을 여러 개의 서로 다른 로컬 박스로 확장한 것).

Scene TreeView는 캐릭터를 루트에, 부위(본체·웨어러블)를 자식으로 답니다:

```
Scene (7 characters)
├── Axe
│   ├── axe
│   └── Axe's Axe
├── Medusa
│   ├── medusa
│   └── Medusa's Bow
...
```

## Dota 2 히어로 재질의 실제 채널 의미

`Common.Dota2.ModelAsset`가 머티리얼에서 읽는 텍스처 파라미터는 4개입니다(`g_tColor`, `g_tNormal`, `g_tMasks1`, `g_tMasks2` — 이 이름들은 VRF의 `Renderer/Shaders/dota_hero.frag.slang`과 `ValveResourceFormat/IO/ShaderDataProvider.cs`에서 그대로 가져온 것이며, dota2-projects의 조사 문서와도 일치합니다). 각 마스크의 채널 의미는 히어로 셰이더 하나에만 해당하는 것이라 임의로 정할 수 없어서, 실제 셰이더 코드를 그대로 옮겼습니다.

| 텍스처 | 채널 | 의미 |
| --- | --- | --- |
| `g_tMasks1` | B | **금속성(metalness)** — PBR 금속/유전체 분리가 아니라, 단순히 diffuse 색을 절반까지 어둡게 하는 계수입니다(`mix(1.0, 0.5, metalness)`). 히어로 머티리얼에는 진짜 metalness/roughness PBR 워크플로가 없습니다. |
| `g_tMasks1` | A | **자체발광(self-illumination)** — 조명 계산 결과(illumination)에 그대로 더해집니다. |
| `g_tMasks2` | R | **스펙큘러 강도** |
| `g_tMasks2` | G | **머티리얼 자체 림 마스크** — 이 저장소의 Key+Fill+Rim 3점 조명 중 "Rim" 포인트 라이트와는 다른 개념입니다. 이건 광원 위치와 무관하게 시야각(Fresnel)에만 반응하는, 아티스트가 구워 넣은 상시 림 라이트입니다. |
| `g_tMasks2` | B | **스펙큘러 틴트** — 하이라이트 색을 흰색에서 알베도 쪽으로 섞는 비율(`mix(vec3(1.0), albedo, tint)`). |
| `g_tMasks2` | A | **스펙큘러 지수 배율** — 기본 지수(100)에 곱해집니다(`mask2.a * 100.0`). |

히어로 머티리얼에는 진짜 roughness/AO 텍스처가 없습니다 — 위 표가 전부입니다. `Common.Dota2`의 `LoadHeroMaterial`이 재질마다 이 4장을 한 번씩만 디코딩해 캐시하고(`ModelAsset.cs`), 어느 한 장이 없는 재질은 `SubMesh.Albedo`/`Normal`/`Mask1`/`Mask2`가 `null`이 됩니다 — `GLView`는 이 경우 1x1 크기의 대체 텍스처(흰색 알베도, 평탄한 노멀, 올제로 마스크)로 채워서 셰이더가 항상 네 장을 그대로 샘플링할 수 있게 합니다.

## 노멀 맵 디코딩: DXT5nm (BC5 아님)

`g_tNormal`은 X를 **알파**, Y를 **그린** 채널에 저장하고(빨강·파랑은 사용하지 않음), Y는 저장할 때 반전되어 있습니다. R/G(BC5 방식)로 잘못 풀면 얼굴처럼 UV가 대칭으로 이어붙는 지점에서 조명이 갈라지는 티가 납니다. `Shaders/shader.frag`의 `UnpackNormal`이 실제 셰이더와 같은 방식으로 푼다:

```glsl
vec2 xy = packedNormal.ag * 2.0 - 1.0;
xy.y = -xy.y;
float z = sqrt(clamp(1.0 - dot(xy, xy), 0.0, 1.0));
```

이 탄젠트 공간 노멀을 월드 공간으로 옮기려면 정점마다 탄젠트가 필요합니다. `Common.Dota2.SubMesh`는 이제 `TangentOffset`(xyz + 손잡이 부호 `w`)을 갖고 있고, `ModelAsset.BuildInterleavedVertices`가 `VBIB.GetNormalTangentArray`(이전까지는 법선만 쓰고 버리던 반환값)에서 탄젠트를 함께 채웁니다. 정점 셰이더가 스키닝 후 탄젠트도 같이 회전시키고, 프래그먼트 셰이더가 `bitangent = cross(normal, tangent) * handedness`로 TBN 행렬을 만듭니다.

이 변경은 `SubMesh.FloatsPerVertex`를 16→20으로, 본 인덱스/가중치 오프셋을 8/12→12/16으로 옮겼습니다. `10~13`번 예제는 이 상수들을 하드코딩하지 않고 `SubMesh.BoneIndexOffset` 등을 그대로 참조하므로 코드 변경 없이 새 스트라이드에 맞춰 다시 빌드됩니다.

## 조명 (`Shaders/shader.frag`)

기존 예제들의 Key(태양 방향광)+Fill+Rim 포인트 라이트 3점 조명 구조는 그대로 유지하되, diffuse/specular 색상과 스펙큘러 지수를 전부 마스크에서 가져옵니다:

- Key: `illumination = (.12 + max(dot(N,-sunDir),0)*.85) + selfIllumination`, `sun = diffuseColor * illumination`.
- Fill/Rim: 기존 `pointLight` 계산과 같은 falloff 식이되, `diffuseColor`/`specularColor`/`specularExponent`/`specularIntensity`가 마스크에서 온 값으로 대체됩니다.
- `g_tMasks2.g` 기반의 머티리얼 자체 Fresnel 림을 알베도 색으로 더합니다 — 이건 포인트 라이트가 아니라 시야각에만 의존하는 항이라 별도로 계산합니다.

### 주변광(ambient)

Key/Fill/Rim 세 광원 중 어느 것도 닿지 않는 면(예: 캐릭터 등 뒤쪽 그늘)은 지금까지 완전히 검게 나왔습니다. 실제 dota2-projects 뷰어를 조사해 보면, 이 문제를 두 단계로 해결합니다 — (1) 프리필터된 환경 큐브맵(VRF 자신의 스튜디오 조명용 기본 큐브맵, `industrial_sunset_puresky.vtex_c`)에서 러프니스 기반 mip을 골라 diffuse/specular IBL을 근사하는 방식과, (2) 큐브맵이 없을 때 쓰는 **하늘/땅 2색 그라디언트** 폴백입니다. 이 예제는 우선 (2)를 그대로 이식했습니다 — 새 텍스처 에셋이 필요 없고, 그 자체로 항상 정확히 동작하는 뷰어 자신의 폴백이기 때문입니다:

```glsl
float skyWeight = clamp(normal.y * 0.5 + 0.5, 0.0, 1.0);
vec3 ambient = mix(uAmbientGroundColor, uAmbientSkyColor, skyWeight) * diffuseColor;
```

`uAmbientSkyColor`(0.18, 0.21, 0.28)와 `uAmbientGroundColor`(0.09, 0.08, 0.07)는 dota2-projects 뷰어가 실제로 쓰는 값을 그대로 옮겼습니다. 큐브맵 기반 IBL(방식 (1))은 실제 프리필터된 밉맵 체인을 GPU에 올리는 새 텍스처 클래스와 에셋 확보(라이선스 확인 필요)가 더 필요해서 이번 범위에는 넣지 않았습니다 — 다음 단계로 시도해볼 만합니다.

## 리소스 수명

캐릭터마다, 그리고 그 안의 역할(본체/웨어러블)마다 자신만의 `Mesh`(20-float, `StaticDraw`)·재질 4장짜리 `Texture2D`·본 팔레트 `StorageBuffer`·배치 행렬 `StorageBuffer`를 갖습니다(`13`번처럼 여러 인스턴스가 하나를 공유하지 않습니다 — 히어로가 서로 다르니 공유할 지오메트리가 없습니다). 대체 텍스처(흰색/평탄 노멀/올제로 마스크) 4장만 `GLView.OnLoad`에서 한 번 만들어 재질이 비어 있는 모든 파트가 공유합니다(`Texture2D.Dispose`는 이미 해제된 핸들에 안전하므로 여러 파트가 같은 대체 텍스처를 참조해도 문제없습니다). **File > Reroll Heroes**는 `DisposeCharacters()`로 현재 캐스트의 모든 GPU 자원을 해제한 뒤 새로 로드합니다.
