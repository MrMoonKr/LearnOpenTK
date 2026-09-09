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
10. `13-CharacterController`: Idle/Walk/Run/Jump/Fall 상태 전환, 카메라, 중력·점프·기본 충돌을 포함한 월드 이동을 구현한다.

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
