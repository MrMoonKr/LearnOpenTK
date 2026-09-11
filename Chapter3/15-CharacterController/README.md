# 15-CharacterController

`14-GpuPbr`의 히어로 렌더링 파이프라인(GPU 스키닝, PBR 재질) 위에 트리뷰 히어로 피커와 평면 지형 위 이동을 얹은 예제입니다. Unity의 `CharacterController`(캡슐 기반 kinematic 이동: `Move()`로 충돌·슬라이드, `isGrounded`, 중력/점프)를 본떠 카메라 상대 이동과 중력·점프·지면 스냅을 구현했습니다. 다만 이 씬의 충돌 대상은 평면 지형 하나뿐이라(임의 지형/장애물 없음) "스윕 캡슐이 지오메트리에 부딪혀 슬라이드"하는 부분은 Y=0 지면 스냅으로 단순화했고, `AGENTS.md`의 원래 로드맵 문구(Idle/Walk/Run/Jump/Fall)와 달리 상태 이름은 유지하되 상세 충돌(계단/경사 처리 등)은 범위 밖입니다 — 이 예제의 핵심은 "애니메이션 상태를 하드코딩하지 않고 각 히어로의 클립 목록에서 데이터 기반으로 추론한다"는 것입니다.

## 실행

1. `config.sample.ini`를 `config.ini`로 복사하고 `[paths] game_root`를 설정합니다.
2. `[character] initial_hero`로 시작 히어로를(기본 `npc_dota_hero_axe`), `move_speed`/`jump_speed`로 이동·점프 속도를 조절합니다. `[world] ground_half_size`는 지형 평면의 절반 크기, `gravity`는 중력 가속도입니다.
3. `dotnet run --project Chapter3/15-CharacterController/15-CharacterController.csproj`
4. 좌측 트리에서 히어로를 클릭하면 원점에 스폰됩니다. **W/A/S/D 또는 방향키**로 카메라가 보는 방향 기준 이동, **Space**로 점프, **F**로 공격, 마우스 오른쪽 드래그로 오빗, 가운데 드래그로 팬, 휠로 줌.

## 히어로 트리 선택과 지연 로딩

`14-GpuPbr`은 로드 시점에 무작위로 고른 히어로 여러 명을 한 번에 배치했지만, 이 예제는 **한 번에 한 명만 조종**하고 나머지 전체 로스터(120여 명)는 트리에 목록만 보여줍니다. 그래서 두 단계로 나눴습니다.

- `HeroRoster.LoadAll(archive)`로 `scripts/npc/npc_heroes.txt`를 읽는 건 순수 텍스트 파싱이라 즉시 끝나므로, `GLView.LoadScene`에서 전체 로스터를 `Roster` 속성으로 한 번에 노출하고 `MainForm`이 이걸로 트리를 채웁니다.
- 실제 3D 모델(`ModelAsset.Load` — VPK에서 지오메트리·스켈레톤·애니메이션을 파싱하고 GPU 리소스까지 만드는 무거운 작업)은 `GLView.SelectHero(npcName)`가 처음 그 히어로를 선택할 때만 수행하고, `Dictionary<string, Character>`에 메모리 캐시해 두 번째부터는 즉시 전환됩니다. `GameArchive`(VPK 검색 경로)도 `LoadScene`에서 한 번만 열어 앱이 끝날 때까지 재사용합니다 — 클릭마다 다시 여는 건 불필요한 디스크 I/O입니다.

## 애니메이션 상태 추론과 디스크 캐시

히어로마다 어떤 클립이 Idle/Run/Attack인지는 하드코딩하지 않고 `Common.Dota2.HeroAnimationClassifier`가 클립 목록에서 추론합니다.

1. **Activity 메타데이터 우선**: `ModelAsset.ImportAnimations`가 이제 `SequenceAnimation.Activities`(`ACT_DOTA_IDLE`/`ACT_DOTA_RUN`/`ACT_DOTA_ATTACK` 같은 Source-1 스타일 activity 이름과 그 `Weight`)를 클립별로 함께 수집해 `ModelAsset.AnimationActivities: IReadOnlyDictionary<string, IReadOnlyList<HeroAnimationActivity>>`로 노출합니다. 기존 코드는 activity 이름만 파싱하고 버리고 있었습니다(가중치도 포함해 읽지 않았습니다). `ACT_DOTA_` 접두어를 뗀 뒤 `IDLE`/`RUN`/`ATTACK`/`JUMP`/`FALL` 부분 문자열이 있으면 그 상태로 분류합니다.
2. **클립명 키워드 폴백**: 실제 Dota 2 에셋은 모든 클립에 activity 데이터를 채워두지 않으므로(강력한 힌트일 뿐, 완전한 목록이 아님), activity로 분류되지 않은 클립은 클립 이름 자체에서 `idle`/`run`·`sprint`/`attack`·`swing`/`jump`·`leap`·`hop`/`fall`·`land` 키워드를 찾습니다.
3. 히어로 하나가 idle/attack 변형을 여러 개 가질 수 있어(`idle_1`, `idle_rare`, `attack_bash` 등) 분류 결과는 카테고리별 리스트이고, **acttable `Weight` 내림차순**(활동으로 분류되지 못하고 키워드로만 분류된 클립은 가중치 데이터가 없어 맨 뒤로 밀림), 그다음 알파벳순으로 정렬합니다. `PrimaryIdleClip`/`PrimaryRunClip`/`PrimaryAttackClip`/`PrimaryJumpClip`/`PrimaryFallClip`은 그 정렬의 첫 항목을 컨트롤러가 재생할 단일 클립으로 정합니다. `Weight`는 원래 게임이 같은 activity를 공유하는 여러 시퀀스(예: idle 변형 여러 개) 중 하나를 뽑을 때 쓰는 실제 선택 가중치라서, 순수 알파벳순보다 "진짜 대표 idle"을 더 신뢰성 있게 고릅니다(예: `idle`이 `idle_rare`보다 가중치가 훨씬 높다면, 이름이 우연히 알파벳순으로 앞서지 않아도 항상 `idle`이 뽑힙니다).

**Dota 2에는 점프 메커닉이 없어서** `idle`/`run`/`attack`류처럼 이름에 `jump`/`fall`이 그대로 들어간 클립은 거의 없습니다. 대신 두 가지 실제 activity/키워드를 씁니다.

- **`ACT_DOTA_FLAIL`** — Axe의 `flail`/`flail_anim` 클립을 실제로 열어보니 이 activity가 붙어 있었습니다. 이름 그대로 "공중에서 팔다리를 허우적대는" 범용 리액션(Eul의 신성한 지팡이/사이클론처럼 공중에 띄우는 효과에 쓰이는 것으로 보입니다)이라, activity 매치 단계(키워드 폴백보다 우선)에서 바로 `Jump`로 분류합니다.
- **`forcestaff`** — Force Staff(거의 모든 히어로가 반응 클립을 갖는 범용 아이템으로, 맞으면 공중으로 날아갑니다) 관련 클립들도 대부분 실제로 `ACT_DOTA_FLAIL`이 붙어 있어 위 규칙에서 이미 걸러집니다. `forcestaff`(activity가 `ACT_DOTA_FORCESTAFF_STATUE`라 activity 매치에 안 걸리는 변형)처럼 activity 매치를 피해간 나머지만 클립명 키워드 폴백에서 잡고, 그중 "end"가 붙은 착지/회복 클립(`axe_forcestaff_end`, activity `ACT_DOTA_FORCESTAFF_END`)은 `Fall`로 분류합니다.

  > 처음엔 `forcestaff_enemy`를 "상대에게 스태프를 쓰는 시전자 자신의 동작이라 땅에 서 있을 것"이라 짐작해 키워드 매칭에서 제외했었는데, 실제 activity 데이터를 찍어보니 `forcestaff_enemy`와 `forcestaff_anim`("_friendly" 변형) 둘 다 진짜 `ACT_DOTA_FLAIL`이 붙어 있었습니다 — 즉 둘 다 정상적인 공중 리액션이고, 제 첫 짐작이 틀렸던 겁니다. 지금은 activity 매치가 먼저 걸러주므로 이 키워드 폴백에서는 "enemy" 제외 로직 자체를 뺐습니다.

실제로 Axe에게 적용해보면 `JumpClips = ["flail", "flail_anim", "forcestaff_anim", "forcestaff_enemy", "forcestaff"]`(가중치순, `flail`이 대표), `FallClips = ["axe_forcestaff_end"]`가 나오고, 점프 중 화면에는 도끼를 든 채 팔다리를 허우적대며 붕 뜬 액스가 보입니다 — 진짜 점프보다 오히려 더 그럴듯합니다. 그래도 Force Staff/Flail 클립조차 없는 히어로는 여전히 있을 수 있으므로, `GLView.PlayClipForState`는 Jump/Fall 상태에서 `JumpClip ?? FallClip ?? RunClip ?? IdleClip`처럼 최종적으로 Run/Idle까지 폴백합니다 — 이건 버그가 아니라 "그 히어로의 실제 애니메이션 목록에 없는 걸 지어내지 않는다"는 이 예제 전체의 원칙을 그대로 따른 결과입니다.

`HeroAnimationClassifier.ClassifyOrLoadCached`는 이 분류 결과를 `<출력폴더>/.cache/animations/<npc이름>.json`에 저장하고, 다음에 같은 히어로를 로드할 때 그 클립 이름 집합이 라이브 데이터와 같으면(게임 업데이트로 애니메이션이 바뀌지 않았으면) 분류를 다시 하지 않고 캐시를 그대로 읽습니다. 클립 집합이 다르거나 캐시 파일이 손상되어 있으면 조용히 재계산합니다 — 캐싱은 재사용 최적화일 뿐 정합성 요건이 아니므로, 쓰기 실패도 예외를 삼키고 무시합니다. `JumpClips`/`FallClips`가 추가되기 전에 쓰인 캐시 파일은 해당 JSON 속성이 아예 없는데, `TryLoadCache`가 역직렬화 후 `?? []`로 null을 빈 리스트로 채워 넣으므로 과거 캐시 파일도 예외 없이 읽힙니다.

캐시 신선도는 **클립 이름 집합**만 보지, 분류 로직(`@` 접두어 제외, 가중치 정렬 등)의 버전은 추적하지 않습니다 — 즉 분류 알고리즘을 바꿔도 클립 목록이 그대로인 히어로는 예전 캐시를 계속 읽습니다. 개발 중 분류 로직을 바꿨다면 `.cache/animations`를 지우고 다시 실행해야 새 로직이 반영됩니다(이 저장소에서 실제로 `@` 접두어 필터와 가중치 정렬을 추가하면서 매번 이렇게 했습니다).

```json
{
  "ClipNames": ["attack1", "idle", "idle_rare", "run", ...],
  "IdleClips": ["idle", "idle_rare"],
  "RunClips": ["run"],
  "AttackClips": ["attack1"],
  "JumpClips": [],
  "FallClips": []
}
```

## 캐릭터 컨트롤러 상태 머신

`CharacterController`(이 프로젝트 로컬 클래스, `Common`에는 두지 않음 — 다른 예제와 공유할 이유가 없는 데모 전용 로직입니다)는 `Idle`/`Run`/`Attack`/`Jump`/`Fall` 다섯 상태를 오갑니다.

- 지면에 있고(`IsGrounded`) 이동 키(WASD 또는 방향키)를 누르고 있으면 `Run`, 떼면 `Idle`.
- **F 키다운 엣지**(지면에 있을 때만)에서 `Attack`으로 전환하고, 그 히어로의 `AttackClip.Duration`만큼 자체 경과 시간을 세어 이동을 잠급니다. `AnimationClip.Sample`은 항상 루프이고 "재생 종료" 신호가 없으므로(`Common.Animation`을 건드리지 않기 위해), 종료 판정은 `Common.Animation`이 아니라 이 컨트롤러 자신의 `_attackElapsedSeconds` 필드로 합니다. 시간이 다 되면 그 순간 눌려 있는 이동 키 상태를 보고 `Run`/`Idle`로 복귀합니다. 공격할 클립이 없는 히어로(`AttackClipDurationSeconds == 0`)는 F가 아무 효과도 없습니다.
- **Space 키다운 엣지**(지면에 있고 공격 중이 아닐 때만)에서 수직 속도에 `jump_speed`를 즉시 부여합니다. 공격 중에는 점프할 수 없고, 반대로 점프/낙하 중에는 F를 눌러도 공격이 시작되지 않습니다(둘 다 한 번에 하나의 전신 클립만 재생하는 구조라 동시 진행을 막았습니다).
- 매 프레임 `_verticalVelocity -= gravity * dt`로 중력을 적분하고 `Position.Y`에 반영하다가, `Y <= 0`이 되면 `Y = 0`으로 스냅하고 속도를 0으로 죽이며 `IsGrounded = true`로 되돌립니다 — 이 씬의 유일한 충돌 대상이 평면 지형 하나뿐이라, Unity `CharacterController.Move()`의 스윕 캡슐-지오메트리 충돌 판정 대신 이 정도의 "Y=0 지면 스냅"으로 충분합니다. 공중에서는(`Jump`: 수직 속도가 양수, `Fall`: 음수) 수평 이동(에어 컨트롤)은 허용하지만 공격은 막습니다.
- `GLView.UpdateCharacter`는 상태가 **바뀐 프레임에만** `Animator.Play(clip)`을 호출합니다 — 매 프레임 호출하면 같은 상태를 유지하는 동안에도 클립이 계속 처음부터 다시 재생되어 루프가 끊겨 보입니다.

## 카메라 상대 이동

W/S/A/D와 방향키는 `OrbitCamera.Yaw`를 기준으로 움직입니다 — W는 항상 카메라가 현재 보고 있는 방향(수평면에 투영)으로, S는 그 반대, D/A는 그에 수직인 좌우로 스트레이프합니다. 마우스로 카메라를 오른쪽 드래그해 돌리면 "앞"도 함께 돌아갑니다(Unity `CharacterController`를 쓰는 3인칭 데모 대부분이 이렇게 동작합니다). `CharacterController.Update`가 매 프레임 `cameraYawDegrees` 하나만 받아 `forward = (cos(yaw), 0, sin(yaw))`, `right = (-sin(yaw), 0, cos(yaw))`를 계산하는 것으로 충분합니다 — `OrbitCamera.Pan`이 이미 같은 `cross(forward, up)` 패턴으로 우측 벡터를 구하고 있어 그 관례를 그대로 따랐습니다.

히어로 모델은 `GLView.SourceToWorldUp`(Source 2의 Z-up → 이 저장소의 Y-up 변환) 적용 후 중립 자세가 월드 +X를 바라보므로, 실제 이동 벡터가 계산된 뒤 `CharacterController`는 요(yaw)를 `atan2(-moveZ, moveX)`로 계산합니다(더 흔한 `atan2(x, z)`가 아닙니다) — 이래야 이동 방향과 히어로가 실제로 도는 방향이 맞습니다. 이 변환은 카메라 상대/월드 절대 어느 쪽으로 이동 벡터를 구하든 동일하게 적용됩니다.

## 팔로우 카메라

새 캐릭터 클래스나 카메라 클래스를 만들지 않고 기존 `OrbitCamera`를 그대로 씁니다. 히어로를 선택할 때 그 히어로의 로컬 바운딩 박스 중심을 `SourceToWorldUp`으로 변환해 `_cameraFollowOffset`으로 저장해 두고, 매 프레임 `_camera.SetView(controller.Position + _cameraFollowOffset, _camera.Distance)`를 호출합니다. `Distance`(마우스 휠로 조절한 줌)는 그대로 유지한 채 `Target`만 걷는 히어로를 따라가므로, 마우스 오빗/팬/줌은 기존 예제와 똑같이 동작하면서 카메라 피벗만 캐릭터를 쫓아갑니다.

## 지형 (`Shaders/ground.vert`/`ground.frag`)

히어로용 PBR 셰이더(`shader.frag`)를 지형에도 재사용하는 대신, `Common/Geometry/PlaneGeometry.cs`(단일 쿼드, position/color/normal)를 `7-Lighting`과 같은 Key+Fill+Rim 포인트 라이트 셰이더로 그리는 별도의 작은 셰이더를 새로 만들었습니다. 지형은 텍스처도 마스크도 없는 단색 평면이라, PBR 셰이더의 텍스처 샘플링/노멀 맵 디코딩 코드를 그대로 끌어오면 오히려 의미 없는 유니폼(`uAlbedo` 등)만 늘어납니다 — 조명 자체는 히어로와 같은 태양광+Fill+Rim 위치를 공유해서 한 장면으로 보이게 했습니다.

## 리소스 수명

- `GameArchive`는 `LoadScene`에서 한 번 열어 `UnloadScene`(컨트롤 `Disposed`)까지 유지합니다.
- 히어로별 `Character`(메시·텍스처·본 팔레트 SSBO)는 `_loadedCharacters`에 누적 캐시되며, `UnloadScene`에서 한꺼번에 해제됩니다 — `14-GpuPbr`의 Reroll처럼 선택할 때마다 이전 것을 지우지 않습니다(재선택이 빨라야 하므로).
- 배치 행렬 `StorageBuffer`(`Character.InstanceBuffer`)는 `14-GpuPbr`과 달리 `BufferUsageHint.DynamicDraw`로 할당하고 매 프레임 `UpdateMatrices`로 다시 씁니다 — `14-GpuPbr`의 히어로는 한 번 배치되면 움직이지 않아 `StaticDraw`로 충분했지만, 여기서는 위치/요(yaw)가 매 프레임 바뀝니다.
- 대체 텍스처 4장(흰색 알베도/평탄 노멀/올제로 마스크)은 `LoadScene`에서 한 번만 만들어 모든 캐릭터·역할이 공유합니다(`Texture2D.Dispose`는 이미 해제된 핸들에 안전).
