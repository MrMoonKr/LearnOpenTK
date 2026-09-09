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
