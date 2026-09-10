# 11-SkeletalAnimation

`10-ModelLoading`과 같은 히어로 본체+기본 웨어러블을 로드한 뒤, 실제 애니메이션 클립을 재생하고 `Common.Animation`의 `Skeleton`/`Bone`/`AnimationClip`/`Animator`로 CPU linear-blend skinning을 수행합니다. 웨어러블은 본체 스켈레톤에 본 이름으로 병합해 같은 포즈를 따라갑니다.

## 실행

1. `config.sample.ini`를 `config.ini`로 복사하고 `[paths] game_root`를 설정합니다. `[animation] clip`에 재생할 클립 이름(예: `idle`)을 지정할 수 있고, 없으면 모델의 첫 애니메이션을 재생합니다.
2. `dotnet run --project Chapter3/11-SkeletalAnimation/11-SkeletalAnimation.csproj`
3. 메뉴의 **Animation**에서 이 모델이 가진 다른 클립(공격, 이동, 사망 등 실제 게임에 있는 임의의 시퀀스)으로 바꿔볼 수 있습니다.

## Common.Animation: 엔진 독립적인 스켈레톤/애니메이션 계층

`Common.Animation`(net8.0, `Common.Dota2`나 OpenGL을 전혀 모릅니다)은 어떤 포맷에서 왔든 상관없이 본 계층과 포즈만 다룹니다.

- `Bone`: 이름, 부모 인덱스(-1이면 루트), bind pose의 로컬 position/rotation/scale.
- `BonePose`: 한 시점의 로컬 변환. `ToMatrix()`가 `Scale * Rotation * Translation` 순서(행벡터 컨벤션)로 행렬을 만듭니다.
- `Skeleton`: `Bone[]`을 부모가 자식보다 먼저 오는 순서로 보관하고, 생성 시 `InverseBindPoses`(각 본의 bind pose 월드 행렬의 역행렬)를 한 번만 계산합니다. `ComputeWorldMatrices(pose, world)`가 로컬 포즈 배열을 계층을 따라 곱해 월드 행렬 배열을 만듭니다.
- `AnimationClip`: 고정 프레임레이트로 구운 `BonePose[][]`(프레임별 전체 본 배열). `Sample(time, outPose)`가 루핑 시간을 프레임 인덱스로 바꾸고 두 프레임 사이를 `Vector3.Lerp`/`Quaternion.Slerp`로 보간합니다.
- `Animator`: 한 스켈레톤의 재생 상태. `Update(dt)`가 시간을 진행하고 클립을 샘플링해 `BoneWorldMatrices`를 갱신한 뒤, `SkinningMatrices[i] = InverseBindPose[i] * BoneWorld[i]`를 계산합니다. 웨어러블처럼 클립이 아니라 병합된 포즈를 받는 경우는 `SetWorldMatrices(...)`로 월드 행렬을 직접 주입합니다.
- `SkeletonMerge`: `BuildBoneMatrixByName(skeleton, worldMatrices)`가 이름→월드 행렬 사전을 만들고, `MergePose(wearableSkeleton, byName)`이 웨어러블의 각 본을 같은 이름의 본체 본 월드 행렬로 바꿔치기합니다(이름이 없으면 웨어러블 자신의 bind pose로 대체). Dota 2에서 무기·장신구가 본체에 붙는 방식은 attachment 소켓이 아니라 **이 이름 매칭**이며, `GLView.UpdateAnimation`이 매 프레임 다시 계산합니다.

## 매 프레임 처리 순서 (`GLView.UpdateAnimation`)

1. 본체 `Animator.Update(deltaSeconds)` — 재생 중인 클립을 샘플링하고 본체의 `BoneWorldMatrices`/`SkinningMatrices`를 갱신합니다.
2. `SkeletonMerge.BuildBoneMatrixByName(bodySkeleton, bodyAnimator.BoneWorldMatrices)`로 이름 사전을 만듭니다.
3. 각 웨어러블에 대해 `SkeletonMerge.MergePose(...)`로 이 프레임의 월드 행렬을 계산하고 `wearableAnimator.SetWorldMatrices(...)`로 반영합니다.
4. 모든 모델의 모든 서브메시에 대해 `SkinVertices(bindVertices, animator.SkinningMatrices, scratch)`로 CPU에서 position/normal을 다시 계산하고, `Mesh.VertexBuffer.Update(scratch)`(`GL.BufferSubData`)로 GPU 버퍼를 덮어씁니다.

`SkinVertices`는 정점마다 최대 4개 본의 가중 평균입니다: `skinnedPosition += weight * Vector3.TransformPosition(bindPosition, skin)`, 법선은 `Vector3.TransformNormal`로 같은 방식으로 섞은 뒤 정규화합니다. 이 예제의 GPU 정점 레이아웃은 position/normal/uv 8-float뿐이고(본 인덱스·가중치는 CPU에서만 쓰고 GPU로 보내지 않습니다), 그래서 `Mesh`는 `BufferUsageHint.DynamicDraw`로 만들어 매 프레임 `Update`가 가능하게 합니다. 같은 계산을 정점 셰이더로 옮겨 GPU에서 수행하는 버전이 `12-GpuSkinning`입니다.

## 리소스 수명

`GLView.OnLoad`가 본체+웨어러블을 로드하며 각 서브메시의 원본 bind pose 정점(`ModelPart.BindVertices`, 16-float)과 GPU에 매 프레임 업로드할 8-float 스크래치 버퍼(`ModelPart.SkinnedScratch`)를 함께 보관합니다. `OnUnload`는 `10-ModelLoading`과 동일하게 각 `Mesh`/`Texture2D`를 해제합니다.
