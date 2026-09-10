# 10-ModelLoading

실제 Dota 2 설치본에서 히어로 모델(`.vmdl_c`)과 기본 장착 웨어러블(무기·머리·갑옷 등)을 읽어와 여러 메시와 재질로 함께 렌더링하는 예제입니다. 목표는 합성 지오메트리가 아니라 외부 포맷에서 로드한 실제 모델을 다루는 것입니다.

## 실행

1. `config.sample.ini`를 `config.ini`로 복사하고 `[paths] game_root`를 Dota 2 설치 폴더(`game\dota`의 상위 폴더)로 바꿉니다. `config.ini`는 머신마다 다르므로 git에 커밋하지 않습니다.
2. `dotnet run --project Chapter3/10-ModelLoading/10-ModelLoading.csproj`

Dota 2 설치본이나 `E:\M-Github-Game\DOTA2\ValveResourceFormat` 참조 프로젝트가 없으면 상태 표시줄에 오류 메시지가 뜨고 빈 화면으로 남습니다.

## 모델 로딩 파이프라인

이 예제는 OpenGL 호출만이 아니라 **에셋을 어떻게 우리 자체 렌더 구조로 들여오는지**가 핵심입니다. 파싱은 `Common.Dota2`(net10.0, `ValveResourceFormat`을 참조)가 담당하고, `Common`/`Common.Graphics`/`Common.Rendering`(net10.0에서도 그대로 참조 가능한 net8.0 라이브러리)은 여전히 GPU 리소스만 다룹니다. `Common.Dota2`는 OpenGL을 전혀 알지 못하고 순수 CPU 데이터(`float[]`, `uint[]`, `byte[]`)만 만듭니다.

1. `GameArchive.Open(gameRoot)`가 `game\dota\gameinfo.gi`를 찾아 `ValveResourceFormat.IO.GameFileLoader`로 VPK 검색 경로를 구성합니다.
2. `ModelAsset.Load(archive, "models/heroes/antimage/antimage.vmdl")`가 `.vmdl_c`를 압축 리소스로 읽고, `Model.GetEmbeddedMeshesAndLoD()`/`GetReferenceMeshNamesAndLoD()`로 최고 디테일 LOD의 메시만 골라 각 draw call을 `SubMesh`(position/normal/uv/boneindex/boneweight을 16-float으로 interleave)로 변환합니다. 재질의 `g_tColor` 텍스처는 `Texture.GenerateBitmap()`으로 CPU에서 BGRA 픽셀로 복호화합니다.
3. `HeroLoadout.HeroNpcNameFromModelPath(...)`가 모델 경로에서 `npc_dota_hero_antimage` 같은 npc 이름을 추정하고, `HeroLoadout.LoadDefaultLoadout(...)`이 `scripts/items/items_game.txt`(KeyValues1, `ValveKeyValue`로 파싱)에서 `prefab == "default_item"`이고 이 히어로를 사용하며 persona가 아닌 항목만 골라 무기·머리·갑옷 등의 `.vmdl` 경로를 돌려줍니다.
4. `GLView.LoadHeroAndDefaultWearables()`가 본체와 각 웨어러블을 동일한 방식으로 `ModelAsset.Load`하고, `Mesh`(position/normal/uv 3개 attribute만 사용)와 텍스처가 있으면 `Texture2D`를 만들어 `LoadedModel`로 묶습니다.

본체와 웨어러블은 파일 안에서 이미 같은 bind pose 좌표계에 맞춰 모델링되어 있으므로, 이 예제에서는 애니메이션이나 스켈레톤 병합 없이 **원본 정점 그대로** 함께 그려도 자연스럽게 맞습니다. 스켈레톤을 실제로 움직이면서도 웨어러블이 따라오게 만드는 것은 `11-SkeletalAnimation`의 주제입니다.

## 렌더링

`OrbitCamera`가 로드된 전체 모델의 AABB로 자동으로 카메라 거리/타깃을 맞춥니다(`OrbitCamera.SetView`, `MinDistance`/`MaxDistance`/`FarPlane`는 사람 크기 모델에 맞게 기존 예제보다 넓게 설정). 오른쪽 드래그는 orbit, 중간 드래그는 pan, 휠은 zoom입니다.

매 프레임 `uModel`은 단위 행렬이고, `uView`/`uProjection`은 카메라에서, `uSunDirection`/`uFillPosition`/`uRimPosition`은 `7-Lighting`과 같은 Key+Fill+Rim 3점 조명 구성을 그대로 재사용합니다. 차이는 `baseColor`가 정점 색상이 아니라 `uAlbedo` 텍스처 샘플이라는 점입니다.

## 리소스 수명

`Mesh`/`Texture2D`는 `GLView.OnLoad`(`LoadScene`)에서 만들어지고 `OnUnload`(`UnloadScene`)에서 각 `LoadedModel`을 순회하며 해제합니다. `GameArchive`는 `using`으로 로딩이 끝나면 즉시 닫히며, VPK 핸들 자체는 GPU 리소스와 수명이 다르므로 별도로 관리합니다. `Shaders/shader.vert`/`shader.frag`는 다른 예제와 같이 `Shaders\**`를 출력 폴더로 복사해 `AppContext.BaseDirectory`에서 읽습니다. `config.sample.ini`도 같은 방식으로 복사되고, 로컬 `config.ini`가 있으면 함께 복사됩니다.
