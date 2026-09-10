# LearnOpenTK 프로젝트 작성 기준

## 적용 범위

새로운 Chapter 예제 프로젝트를 만들거나 기존 예제를 WinForms 기반 OpenGL 예제로 전환할 때 적용한다.

## 프로젝트 구조

- 새 프로젝트는 `ChapterN/순번-이름` 폴더에 독립적으로 둔다.
- `Program`, `MainForm`, `GLView` 클래스를 사용한다.
- `Chapter3/2-GLApp`의 UI 구성을 기준으로 한다. 즉 MenuStrip(File/View/Help), 좌측 Scene TreeView, 우측 GLView, 하단 StatusStrip(FPS)을 유지한다.
- 학습 주제에 필요한 렌더링 기능만 `GLView`에 추가한다. 학습 목적과 무관한 UI 변경은 하지 않는다.
- `2-GLApp` 프로젝트를 `ProjectReference`로 참조하거나 해당 프로젝트의 소스 파일을 링크(`Compile Include="..\\2-GLApp\\..."`)하지 않는다. 각 예제는 `Program`, `MainForm`, `GLView` 구현을 프로젝트 내부에 독립적으로 둔다.
- 공용 셰이더 도우미 등 `Common` 프로젝트 참조는 허용한다.
- 새 프로젝트는 `LearnOpenTK.sln`에 추가하고 Chapter 솔루션 폴더 아래에 배치한다.

## README 작성 기준

- 각 예제 프로젝트에는 해당 프로젝트 폴더에 `README.md`를 둔다.
- README에는 실행 방법뿐 아니라 예제의 학습 목표와 실제 코드 설명을 반드시 포함한다.
- 코드 설명은 해당 예제에서 사용한 주요 클래스와 메서드, 데이터 구조, 셰이더/리소스, 핵심 OpenGL 호출의 역할과 호출 순서를 다룬다.
- 버퍼·텍스처·셰이더·uniform처럼 GPU 리소스를 사용하는 예제는 각 리소스의 의미, 생성·바인딩·사용·해제 방법 및 코드와의 대응을 설명한다.
- 화면 결과가 특정 데이터 구성이나 OpenGL 동작에 의존한다면(예: 색상 보간, 깊이 테스트, 좌표 변환) 그 원인과 확인 방법도 기록한다.

## OpenGL 구현 기준

- OpenGL 리소스(VAO/VBO/EBO, 셰이더)는 `OnLoad`에서 생성하고, 그리기는 `OnRenderFrame`에서 수행하며, `OnUnload`에서 해제한다.
- `GLView`는 GLControl 기반으로 구현하며, clear color, VSync, viewport 갱신, FPS 및 상태 이벤트를 제공한다.
- GLSL 파일을 `AppContext.BaseDirectory`에서 읽는 경우 `Shaders/**`를 출력 폴더에 복사하도록 프로젝트 파일에 설정한다. 기본 `None` 항목을 갱신하는 방식을 권장한다.

```xml
<ItemGroup>
  <None Update="Shaders\**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- 여러 삼각형으로 하나의 연속적인 색상 그라데이션을 만들 때, 공유 변을 기준으로 색상 불연속이 생기지 않도록 정점 색상을 선택한다. 사각형의 경우 두 삼각형의 대각선이 보이지 않는지 확인한다.

## 완료 전 확인

- `dotnet build ChapterN/순번-이름/프로젝트.csproj`가 경고와 오류 없이 성공해야 한다.
- 외부 셰이더/텍스처 리소스가 출력 폴더에 복사되는지 확인한다.
- 실행 화면에서 메뉴, Scene TreeView, 배경색 선택, VSync, 상태/FPS 표시와 새 렌더링 기능이 모두 동작하는지 확인한다.

## Chapter 3 렌더링·애니메이션 확장 로드맵

Chapter 3의 4번 예제부터는 단순 OpenGL 호출 예제를 재사용 가능한 렌더링 구성 요소로 점진적으로 확장한다. 최종 목표는 스키닝된 모델의 애니메이션 상태 전환과 간단한 월드 내 캐릭터 이동이다.

1. `4-GraphicsResources`: `GlBuffer`, `VertexArray`, `Texture2D`, `ShaderProgram`의 생성·바인딩·해제를 분리한다.
2. `5-MeshAndMaterial`: 정점/인덱스 데이터와 GPU 자원을 소유하는 `Mesh`, 셰이더·텍스처·uniform을 묶는 `Material`을 구현한다.
3. `6-Camera`: `uModel`, `uView`, `uProjection` 및 perspective projection으로 물체 변환과 카메라 변환을 분리한다.
4. `7-Lighting`: normal, Blinn-Phong 재질, 태양 방향광(Key)·Fill·Rim의 3점 조명을 구현한다.
5. `8-KeyboardMouse`: orbit camera의 오른쪽 드래그 회전, 중간 드래그 패닝, 휠 줌 입력 콜백을 구현한다.
6. `9-SceneGraph`: `Scene`, `SceneNode`, `Transform`, `Camera`, `Light`를 통해 계층 장면을 구성한다.
7. `10-ModelLoading`: 여러 메시와 재질을 가진 모델을 로드하고 렌더링한다.
8. `11-SkeletalAnimation`: `Skeleton`, `Bone`, `AnimationClip`, `Animator` 및 CPU linear-blend skinning을 구현한다.
9. `12-GpuSkinning`: 본 행렬 팔레트를 GPU로 전달하여 정점 셰이더에서 skinning을 수행한다.
10. `13-GpuSkinnedInstancing`: 본 행렬 팔레트를 인스턴스별 SSBO에 담아 `GL.DrawElementsInstanced` 한 번으로 여러 인스턴스를 그리되, 각 인스턴스가 서로 다른 애니메이션 클립/재생 시점을 갖게 한다.
11. `14-GpuPbr`: 알베도 외에 노멀 맵과 Dota 2 히어로 마스크 텍스처(마스크1/마스크2)를 읽어 physically based 조명 모델로, 실제 게임 로스터에서 무작위로 고른 여러 히어로(Axe 포함)를 나란히 렌더링한다.
12. `15-CharacterController`: Idle/Walk/Run/Jump/Fall 상태 전환, 카메라, 중력·점프·기본 충돌을 포함한 월드 이동을 구현한다.

### 공용 계층의 책임

- `Common.Graphics`: OpenGL 핸들과 GPU 리소스의 생성·바인딩·해제를 담당한다. `IDisposable`을 구현하며 OpenGL 컨텍스트가 유효한 `GLView.OnUnload`에서 해제한다.
- `Common.Rendering`: `Mesh`, `Material`, `RenderState`, `Renderer`를 통해 무엇을 어떤 상태로 그릴지 담당한다.
- `Common.Scene`: Transform과 부모-자식 관계, 카메라, 광원을 담당한다. OpenGL 호출을 직접 포함하지 않는다.
- `Common.Animation`: skeleton, animation clip, pose 계산 및 skinning palette를 담당한다.
- `GLView`: 컨텍스트, 프레임 루프, viewport, 입력과 UI 이벤트만 담당하며 GPU 리소스의 생성은 `OnLoad`, 렌더링은 `OnRenderFrame`, 해제는 `OnUnload`에서 수행한다.

초기 Chapter 1~2 예제는 학습을 위해 직접 `GL.*` 호출을 유지한다. 공용 래퍼는 Chapter 3/4 이후 예제부터 사용하며, 래퍼가 OpenGL 호출의 의미를 숨기지 않도록 README에 내부 호출과 리소스 수명을 설명한다.

## Current Chapter 3 implementation rules

- Chapter 3 projects 4 through 9 are independent WinForms projects and must be registered under the `Chapter3` solution folder in `LearnOpenTK.sln` as part of project creation.
- Copy the UI behavior of `3-RenderQuad`: DPI-scaled design client/minimum sizes, File/View/Help menus, 30% Scene TreeView split, clear-color choices, VSync, status text, and `Update: ... FPS | Render: ... FPS` status display.
- Write every `.csproj` in the multi-line, indented style used by `2-GLApp`. Do not create one-line XML project files.
- Write C# in readable blocks. Keep using directives, fields, constructors, properties, event registration, resource lifecycle methods, rendering methods, and input handlers on separate lines and methods. Do not compress a project into one-line declarations or methods.
- Common graphics resource names are `Buffer`, `VertexBuffer`, `IndexBuffer`, and `VertexArray`. `Mesh` owns VAO/VBO/EBO and `Material` selects a shader.
- This repository's `Shader.SetMatrix4` uploads transposed OpenTK matrices. GLSL therefore follows the existing row-vector convention: `vec4(position, 1.0) * uModel * uView * uProjection`.
- Lighting examples use a white albedo, per-face normals (the shared `CubeGeometry.LitVertices` format is position/color/normal), and Directional Key + Point Fill + Point Rim lighting. Light gizmos use an unlit material when added.
- `8-KeyboardMouse` uses an orbit camera: right-button drag rotates around Target, middle-button drag pans Target and camera together, and wheel zoom changes Distance. Keep `GLView.TabStop` and call `Focus()` when input begins.
- `9-SceneGraph` uses 10 deterministic random groups with 10 child cubes each. Parent world transforms are propagated to children; group rotation axes/speeds use a fixed random seed for reproducible output.

## Chapter 3 10-14: real Dota 2 assets via ValveResourceFormat

`10-ModelLoading`, `11-SkeletalAnimation`, `12-GpuSkinning`, `13-GpuSkinnedInstancing`, and `14-GpuPbr` load a real Dota 2 hero body and its default wearables from the player's own game installation, using the sibling `ValveResourceFormat` (VRF) repository purely as a parsing library.

- These five projects, and the `Common.Dota2` project they reference, target `net10.0`/`net10.0-windows` because `ValveResourceFormat.csproj` targets `net10.0`. Chapter 3 projects 1-9 and `Common` stay on `net8.0`(-windows); a newer-TFM project can reference an older-TFM library, so `Common.csproj` did not need to change target.
- `Common.Dota2/Common.Dota2.csproj` project-references `..\..\ValveResourceFormat\ValveResourceFormat\ValveResourceFormat.csproj` by relative path (a sibling checkout, not a NuGet package). It is intentionally not added as a "must exist" dependency of the rest of the repo: only these five example projects need it, and building them requires that sibling repository to be present on disk.
- `Common.Dota2` is parsing-only: it returns plain CPU data (`SubMesh` with an interleaved 20-float vertex layout: position/normal/uv/tangent+handedness/boneindex/boneweight, see its constants; a decoded texture is a `DecodedTexture` record) and populates `Common.Animation` types (`Skeleton`, `Bone`, `AnimationClip`). It never calls OpenGL and never references `Common.Rendering`/`Common.Graphics`. Do not route Dota 2 parsing through VRF's own `Renderer/` project; that is a separate full engine with its own scene graph and shader pipeline, not a fit for this repository's minimal wrapper style.
- `GameArchive` opens `<game_root>\game\dota\gameinfo.gi` via `ValveResourceFormat.IO.GameFileLoader.FindAndLoadSearchPaths` and loads compiled resources (`.vmdl_c` etc.) through it. Non-compiled game scripts (`scripts/items/items_game.txt`) are read as raw bytes via `GameFileLoader.FindFile` + `Package.ReadEntry`, not `LoadFileCompiled`, since they are not resource-container files.
- Wearable placement uses **bone-name matching**, not VRF's `Attachment`/attachment-socket data: `HeroLoadout` resolves a hero's default (non-persona) loadout from `items_game.txt` (KeyValues1, parsed with the `ValveKeyValue` package VRF already depends on), and `Common.Animation.SkeletonMerge` places each wearable's skeleton onto the body's current pose by matching bone names case-insensitively, falling back to the wearable's own bind pose for unmatched bones. This mirrors how the game itself attaches cosmetic pieces; do not add attachment-socket-based placement for this use case.
- Each project reads its own `config.ini` (gitignored; `config.sample.ini` is committed and copied to the output directory) for `[paths] game_root`, `[model] path`, and optionally `[animation] clip` (`13-GpuSkinnedInstancing` instead reads `[instancing] rows`/`columns`/`spacing`, since every instance auto-cycles through the model's own clip list; `14-GpuPbr` reads `[characters] count`/`spacing`/`seed` instead, and its `[model] path` is unused, kept only so the file shares a shape with the others). Do not hardcode a machine-specific game path in source.
- `14-GpuPbr` places several *different* heroes (not repeats of one hero) side by side, so it cannot batch them into one instanced draw call the way `13-GpuSkinnedInstancing` does (different heroes have different vertex/index buffers and bone counts). Its `Character` owns one `ModelRole` per body/wearable part, each with its own single-instance bone-palette `StorageBuffer`; `Mesh.DrawInstanced(1)` is still used (not `Draw`) purely to reuse the same SSBO-indexed shader code as `13-GpuSkinnedInstancing`, not because there is real instancing happening. `Common.Dota2.HeroRoster` reads `scripts/npc/npc_heroes.txt` (KeyValues1) for the real, playable hero list (name -> model path) instead of a hardcoded name list: several heroes' internal model folder names do not match their public name (`bloodseeker` -> `blood_seeker`, `drow_ranger` -> `drow`, `nevermore` -> `shadow_fiend`), so guessing paths from hero names is unreliable. Because the roster already gives the exact npc name, `HeroLoadout.LoadDefaultLoadout` no longer needs `HeroNpcNameFromModelPath`'s inference for this project.
- `14-GpuPbr`'s fragment shader adds a flat two-color hemisphere ambient term (sky/ground blended by world-space normal.y) so faces none of the Key/Fill/Rim lights reach are not pure black. The constants (`(.18,.21,.28)` sky, `(.09,.08,.07)` ground) and the technique match the exact fallback `dota2-projects`' own hero viewport uses when it has no environment cubemap loaded; that project's primary path is a real prefiltered-cubemap IBL pass (sourced from `ValveResourceFormat`'s own bundled default studio cubemap, `industrial_sunset_puresky.vtex_c`, not a Dota 2 game asset) which is a possible future upgrade but was not implemented here (needs a new cube-texture class and a decision about depending on/copying that asset).
- `OrbitCamera` gained `MinDistance`/`MaxDistance`/`FarPlane` (defaulting to the old hardcoded 0.5/30/100) and `SetView(target, distance)` so these life-sized-model viewers can widen the zoom/clip range and auto-frame a loaded model's bounds without changing the small unit-cube chapters' behavior. **Both `MaxDistance` and `FarPlane` must be widened together** when auto-framing content larger than the default cube examples: widening only `MaxDistance` lets the camera back away from the model far enough that the model itself ends up beyond the (still-default) far clip plane, rendering nothing.
- Source 2 meshes are authored right-handed Z-up (+X forward, +Y left, +Z up); this repository's cameras/lighting assume Y-up. Rather than converting every vertex, bone, and animation sample at import time, each of these five `GLView`s applies one fixed rotation (`SourceToWorldUp`, -90 degrees about X) as the outermost transform (folded into `uModel`, or into each instance's transform in `13-GpuSkinnedInstancing`/`14-GpuPbr`) after CPU or GPU skinning has already happened in the untouched source space. Any camera-framing math (e.g. an auto-fit target) computed from raw mesh bounds must also be run through `SourceToWorldUp`, or the camera ends up centered on the wrong point.
- `Common.Rendering.Mesh` gained a second constructor taking an explicit `VertexAttribute[]` layout (plus an optional `BufferUsageHint`) for vertex shapes that do not fit the fixed position/color/normal constructor used by chapters 4-9, and `DrawInstanced` (`GL.DrawElementsInstanced`) alongside `Draw`. `Common.Graphics.Buffer` gained `Update<T>` (`GL.BufferSubData`) for CPU-skinned vertex buffers created with `BufferUsageHint.DynamicDraw`. `Common.Graphics.StorageBuffer` wraps a Shader Storage Buffer Object (`Allocate`/`BindBase`) for per-instance data too large for a plain uniform array, plus `AllocateMatrices`/`UpdateMatrices` (see the transpose note below). `Common.Shader` gained `SetMatrix4Array` for uploading a GPU skinning bone palette (`uniform mat4 uBones[N]`) for the single-instance case (`12-GpuSkinning`); `13-GpuSkinnedInstancing`/`14-GpuPbr` instead upload one `StorageBuffer` per role holding every instance's bone palette back to back, indexed in the shader by `gl_InstanceID * boneCountPerInstance`, since a plain uniform array has no room for a per-instance dimension.
- `13-GpuSkinnedInstancing` draws one `GL.DrawElementsInstanced` call per role (the body, then each wearable) across the whole instance grid, not per hero. Each role owns its own bone `StorageBuffer` (sized `instanceCount * thatRole'sBoneCount`) and a reused `Matrix4[]` scratch array refilled every frame from each instance's own `Animator`; a shared instance-transform `StorageBuffer` (grid placement, `StaticDraw`, built once) is bound at a second SSBO binding point. Instances desync visually by each playing a different clip from the model's own animation list, time-offset by a fixed per-instance amount.
- **`Matrix4` uploaded to a raw buffer (SSBO/VBO via `Buffer.SetData`/`Update`) is not automatically transposed the way `Shader.SetMatrix4`/`SetMatrix4Array` transpose it for `GL.UniformMatrix4`.** `Matrix4` is stored row-major to match this repo's row-vector convention, but `GL.BufferData`/`BufferSubData` have no transpose flag, so a raw upload leaves GLSL's column-major `mat4` reading the transpose of every matrix — for a skinning palette this does not look like a uniformly wrong pose, it looks like the bones are composed in the wrong order (limbs twisted/scrambled). Always upload `Matrix4[]` through `StorageBuffer.AllocateMatrices`/`UpdateMatrices` (which transpose first), never through the plain `Allocate`/`Update` inherited from `Buffer`.
- `14-GpuPbr` reads a hero material's other three texture params beyond `g_tColor` (already used since `10-ModelLoading`): `g_tNormal`, `g_tMasks1`, `g_tMasks2`. These names and the per-channel semantics below come straight from `ValveResourceFormat/Renderer/Shaders/dota_hero.frag.slang` (the actual shader hero materials use) and `ValveResourceFormat/IO/ShaderDataProvider.cs`; do not invent different channel meanings. `g_tMasks1`: B = metalness (a diffuse-darkening factor only, not a metallic/dielectric PBR split), A = self-illumination (additive). `g_tMasks2`: R = specular intensity, G = a static per-material Fresnel rim mask (distinct from this repo's own Key/Fill/Rim point-light "Rim"), B = specular tint toward albedo, A = specular exponent scale. Hero materials have no true roughness/AO texture. `g_tNormal` is DXT5nm-packed: X in the **alpha** channel, Y in **green** (not R/G, that is the more common BC5 convention and produces a lighting seam at mirrored UV islands such as a character's face), Y stored inverted, Z reconstructed via `sqrt(1-x²-y²)`.
- Tangent-space normal mapping needs per-vertex tangents, which `VBIB.GetNormalTangentArray` already returns alongside normals but earlier chapters discarded (`(normals, _) = ...`). `SubMesh` gained `TangentOffset` (xyz + bitangent handedness `w`) and `FloatsPerVertex` grew from 16 to 20 accordingly (bone index/weight offsets shifted from 8/12 to 12/16). Chapters 10-13 did not need code changes for this: they reference `SubMesh.BoneIndexOffset`/`BoneWeightOffset`/`FloatsPerVertex` symbolically rather than hardcoding the old numbers, so they picked up the new stride on rebuild automatically.
- A material missing one of its four maps (some wearables have no normal/mask texture) gets a 1x1 fallback texture (white albedo, flat normal, all-zero masks) built once per `GLView` and shared by every part that needs it, so the fragment shader can sample all four maps unconditionally instead of branching on "has this map" uniforms.
