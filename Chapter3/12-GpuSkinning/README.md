# 12-GpuSkinning

`11-SkeletalAnimation`과 같은 히어로+웨어러블 애니메이션이지만, 정점을 매 프레임 CPU에서 다시 계산하는 대신 본 행렬 팔레트만 GPU에 uniform으로 올리고 정점 셰이더가 skinning을 수행합니다.

## 실행

1. `config.sample.ini`를 `config.ini`로 복사하고 `[paths] game_root`와 필요하면 `[animation] clip`을 설정합니다.
2. `dotnet run --project Chapter3/12-GpuSkinning/12-GpuSkinning.csproj`
3. 메뉴의 **Animation**에서 클립을 바꿀 수 있습니다.

## CPU skinning과의 차이

`Common.Dota2.ModelAsset`가 만드는 `SubMesh`는 처음부터 position/normal/uv/boneindex/boneweight을 16-float으로 interleave해 두므로, 두 예제 모두 같은 CPU 데이터에서 출발합니다. 차이는 그 다음부터입니다.

- `11-SkeletalAnimation`: GPU 정점 버퍼는 position/normal/uv 8-float뿐이고, 본 인덱스/가중치는 CPU에만 남아 매 프레임 `Animator.SkinningMatrices`로 정점을 직접 변형한 뒤 `Buffer.Update`로 재업로드합니다.
- `12-GpuSkinning`(이 예제): `Mesh`가 16-float 전체를 `BufferUsageHint.StaticDraw`로 **한 번만** GPU에 올립니다(`GLView.BuildLoadedModel`). 본 인덱스(`location=3`, `vec4`)와 본 가중치(`location=4`, `vec4`)가 정점 attribute로 그대로 전달되고, 매 프레임 CPU가 하는 일은 `Animator.Update`/`SkeletonMerge`로 스켈레톤 포즈를 계산해 `Shader.SetMatrix4Array("uBones", animator.SkinningMatrices)`로 본 행렬 배열 uniform 하나를 올리는 것뿐입니다.

`Common.Shader.SetMatrix4Array`는 배열 uniform이 활성 uniform 목록에 `uBones[0]`으로만 보고될 수 있다는 점 때문에(GLSL은 배열의 나머지 원소가 바로 다음 location에 온다고 보장합니다) 캐시된 uniform 사전을 쓰지 않고 `GL.GetUniformLocation(handle, "uBones[0]")`을 직접 조회한 뒤 `GL.UniformMatrix4(location, count, transpose, ...)`로 한 번에 업로드합니다.

## 정점 셰이더의 skinning (`Shaders/shader.vert`)

```glsl
mat4 skin =
    aBoneWeights.x * uBones[int(aBoneIndices.x)] +
    aBoneWeights.y * uBones[int(aBoneIndices.y)] +
    aBoneWeights.z * uBones[int(aBoneIndices.z)] +
    aBoneWeights.w * uBones[int(aBoneIndices.w)];

vec4 skinnedPosition = vec4(aPosition, 1.0) * skin;
vec3 skinnedNormal = aNormal * mat3(skin);
```

`11-SkeletalAnimation`의 `SkinVertices`(C#, `Vector3.TransformPosition`/`TransformNormal`의 가중 합)와 정확히 같은 계산을, 정점 하나당 한 번씩 GPU 코어가 병렬로 수행합니다. 이후 `skinnedPosition * uModel * uView * uProjection`으로 이어지는 부분은 다른 예제들과 같은 행벡터 컨벤션입니다. 스켈레톤이 없는 모델(웨어러블 중 본이 하나도 없는 경우)은 `uBones`에 항등 행렬 하나만 올려 `boneIndex == 0, weight == 1`인 기본값이 원본 정점을 그대로 통과시키게 합니다.

셰이더 배열 크기 `uBones[64]`는 안티메이지 본체(34본)보다 넉넉하지만, 각 모델은 자신의 스켈레톤 범위 안의 인덱스만 참조하므로 서로 다른 모델이 이 배열을 같은 프레임에 다른 값으로 여러 번(모델마다 한 번) 다시 채워도 안전합니다.

## 매 프레임 처리 순서 (`GLView.UpdateAnimation` / `RenderScene`)

1. 본체 `Animator.Update(deltaSeconds)`.
2. `SkeletonMerge`로 각 웨어러블의 이 프레임 월드 행렬을 계산해 `wearableAnimator.SetWorldMatrices(...)`.
3. 그리기 직전 모델마다 `_shader.SetMatrix4Array("uBones", model.Animator.SkinningMatrices)`를 호출한 뒤 그 모델의 서브메시들을 그립니다. 정점 버퍼 자체는 로드 시점 이후로 다시 건드리지 않습니다.

## 리소스 수명

`Mesh`(16-float, `StaticDraw`)와 `Texture2D`는 `10-ModelLoading`과 동일하게 `OnLoad`/`OnUnload`에서 생성·해제됩니다. `11-SkeletalAnimation`과 달리 본체 bind pose 정점을 별도로 들고 있을 필요가 없습니다 — GPU 버퍼 자체가 이미 bind pose 데이터를 담고 있고, 애니메이션은 `uBones`로만 표현되기 때문입니다.
