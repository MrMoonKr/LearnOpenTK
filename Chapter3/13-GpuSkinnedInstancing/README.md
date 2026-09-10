# 13-GpuSkinnedInstancing

`12-GpuSkinning`과 같은 히어로 본체+기본 웨어러블을 로드하되, 하나가 아니라 `rows x columns` 그리드로 여러 인스턴스를 배치하고 **각 인스턴스가 서로 다른 애니메이션 클립을, 서로 다른 시점에** 재생합니다. 같은 GPU 지오메트리를 공유하면서 `GL.DrawElementsInstanced` 한 번으로 전체 그리드를 그립니다.

## 실행

1. `config.sample.ini`를 `config.ini`로 복사하고 `[paths] game_root`를 설정합니다. `[instancing] rows`/`columns`/`spacing`으로 그리드 크기와 간격(월드 유닛)을 조절할 수 있습니다.
2. `dotnet run --project Chapter3/13-GpuSkinnedInstancing/13-GpuSkinnedInstancing.csproj`

기본값은 5x5(25 인스턴스)이며, 각 인스턴스는 모델이 가진 애니메이션 목록을 순서대로 나눠 가지면서(115개 클립을 25 인스턴스가 나눠 재생) 시작 재생 시간도 인스턴스마다 다르게 어긋나 있어 같은 클립이 반복돼도 동기화되어 보이지 않습니다.

## 12-GpuSkinning과의 차이: "본 팔레트 하나" → "인스턴스별 본 팔레트"

`12-GpuSkinning`의 `uniform mat4 uBones[64]`는 그리기 한 번에 인스턴스 하나의 포즈만 담을 수 있습니다. 여러 인스턴스를 각자 다른 포즈로 그리려면 인스턴스 차원이 있는 저장소가 필요합니다 — 이 예제는 Shader Storage Buffer Object(SSBO)로 해결합니다.

- `Common.Graphics.StorageBuffer`: SSBO를 감싸는 새 클래스. `Allocate<T>(data, usage)`가 최초 크기를 잡고, 상속받은 `Buffer.Update<T>`(`GL.BufferSubData`)로 매 프레임 내용만 다시 씁니다. `BindBase(bindingIndex)`가 셰이더의 `layout(std430, binding = N)`과 짝을 맞춥니다.
- `ModelRole`(`GLView.cs`): 히어로의 "역할" 하나(본체, 또는 웨어러블 하나)를 표현합니다. 인스턴스 수만큼의 `Animator?[]`(스켈레톤이 없는 역할은 전부 null), 본 팔레트를 담는 `BoneBuffer`(SSBO), 그리고 매 프레임 다시 채우는 재사용 `Matrix4[] BoneScratch`(크기 `instanceCount * BoneCount`)를 가집니다.
- 인스턴스 배치는 별도의 공유 `StorageBuffer`(그리드 위치, `StaticDraw`, 로드 시 한 번만 채움)이며, 모든 역할이 같은 바인딩(1번)을 공유합니다. 본 팔레트 SSBO는 역할마다 다르므로 그리기 직전마다 0번 바인딩을 그 역할의 `BoneBuffer`로 바꿔 끼웁니다.

## 정점 셰이더 (`Shaders/shader.vert`, `#version 430`)

```glsl
layout(std430, binding = 0) readonly buffer BoneBuffer   { mat4 bBones[]; };
layout(std430, binding = 1) readonly buffer InstanceBuffer { mat4 bInstanceTransform[]; };

void main()
{
    int boneBase = gl_InstanceID * uBoneCountPerInstance;
    mat4 skin = aBoneWeights.x * bBones[boneBase + int(aBoneIndices.x)] + ... ; // 4개 본 가중 합, 12-GpuSkinning과 동일한 수식

    vec4 skinnedPosition = vec4(aPosition, 1.0) * skin;
    mat4 instanceTransform = bInstanceTransform[gl_InstanceID];
    vec4 worldPosition = skinnedPosition * instanceTransform;
    gl_Position = worldPosition * uView * uProjection;
}
```

`uniform mat4 uBones[64]`처럼 크기가 고정되지 않고, `bBones[]`는 필요한 만큼(인스턴스 수 × 본 수) 커질 수 있습니다. `gl_InstanceID`가 이 draw call 안에서 몇 번째 인스턴스를 그리는 중인지 알려주므로, 본 팔레트 시작 위치(`boneBase`)와 배치 행렬(`bInstanceTransform[gl_InstanceID]`)을 인스턴스마다 다르게 골라 씁니다. `12-GpuSkinning`에 있던 `uModel` uniform은 사라지고, 그 역할(Source Z-up → 월드 Y-up 축 변환)을 각 인스턴스 배치 행렬에 미리 합성해 넣었습니다(`GLView.SourceToWorldUp * Matrix4.CreateTranslation(gridOffset)`).

## 매 프레임 처리 순서 (`GLView.UpdateAnimation` / `RenderScene`)

1. 인스턴스마다: 본체 `Animator.Update(dt)` → `SkeletonMerge`로 그 인스턴스의 웨어러블 포즈 계산(`11-SkeletalAnimation`과 동일한 이름 매칭, 인스턴스마다 독립적으로 반복).
2. 역할마다: 인스턴스 전체를 순회하며 `BoneScratch[instance * BoneCount .. +BoneCount]`에 그 인스턴스의 `Animator.SkinningMatrices`를 복사하고, `BoneBuffer.Update(BoneScratch)`로 SSBO 전체를 한 번에 갱신.
3. 그리기: 공유 `InstanceBuffer`를 binding 1에 한 번 바인딩한 뒤, 역할마다 `uBoneCountPerInstance`를 설정하고 `BoneBuffer`를 binding 0에 바꿔 끼우고, 그 역할의 서브메시들을 `Mesh.DrawInstanced(instanceCount)`로 그립니다.

## 카메라 자동 프레이밍

인스턴스가 그리드로 퍼져 있으므로, 카메라가 맞춰야 할 대상은 히어로 한 개가 아니라 전체 그리드입니다. `ComputeWorldBounds`가 히어로 한 개의 로컬 바운딩 박스(본체+웨어러블, Source 공간)를 8개 코너로 펼친 뒤, 인스턴스별 배치 행렬(`SourceToWorldUp * 그리드 오프셋`)로 각각 변환해 합집합을 구합니다. `OrbitCamera.FarPlane`/`MaxDistance`도 이 전체 반지름 기준으로 넓힙니다(`10-ModelLoading`에서 히어로 한 개 기준으로 하던 것과 같은 계산을 그리드 전체로 확장).

## 리소스 수명

`Mesh`(16-float, `StaticDraw`)와 `Texture2D`는 역할당 한 벌만 만들어 모든 인스턴스가 공유합니다. 각 역할의 `BoneBuffer`(SSBO)와 공유 `InstanceBuffer`도 `GLView.OnUnload`(`UnloadScene`)에서 해제됩니다.
