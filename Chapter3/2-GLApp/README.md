# 2-GLApp: WinForms 기반 OpenGL 툴 앱

`GameWindow`의 렌더링 개념을 WinForms 데스크톱 도구 UI 안에서 사용하는 예제다. 독립 GLFW 창인 `GameWindow` 대신, WinForms 컨트롤인 `OpenTK.WinForms.GLControl`을 사용한다. (`OpenTK.GLControl` 패키지 4.0.0의 네임스페이스다.)

## 실행

```powershell
dotnet run --project Chapter3/2-GLApp/2-GLApp.csproj
```

Windows 전용 프로젝트이며 `.NET 8`과 `OpenTK 4.8.2`, `OpenTK.GLControl 4.0.0`을 사용한다.

## 화면 구성

```text
MainForm
├─ MenuStrip
│  ├─ File / Exit
│  ├─ View / 배경색 선택
│  └─ Help / About
├─ SplitContainer
│  ├─ 왼쪽: TreeView (Scene 탐색기)
│  └─ 오른쪽: GLView : GLControl (OpenGL 렌더 영역)
└─ StatusStrip (선택 항목, update/render FPS)
```

TreeView의 배경 항목 또는 `View` 메뉴를 선택하면 OpenGL clear color가 즉시 바뀐다. 이는 UI 명령이 렌더러 상태를 바꾸는 가장 작은 예시다.

`View > VSync`는 swap interval을 제어한다. 기본값은 Off(`Context.SwapInterval = 0`)이며, 프레임 속도는 UI 타이머, GPU, 드라이버에 의해 제한된다. VSync를 켜면 swap interval 1을 사용하므로 보통 디스플레이 주사율에 맞춰지고 화면 찢김을 줄일 수 있다.

## 핵심 설계

`GLView`는 `GLControl`을 상속한다. GLControl은 내부의 OpenTK `NativeWindow`를 WinForms 자식 창으로 호스팅한다. 따라서 `GameWindow`를 WinForms `SplitContainer`에 강제로 넣지 않아도, 메뉴·트리·상태바가 있는 일반 도구 앱 안에 OpenGL 화면을 안전하게 배치할 수 있다.

WinForms 이벤트를 그대로 애플리케이션에 노출하지 않고, `GameWindow`와 비슷한 protected virtual 메서드로 한 번 변환한다. 이후 씬 뷰를 확장할 때는 `GLView`를 상속해 다음 메서드를 재정의하면 된다.

| `GLView` 메서드 | GameWindow 대응 | 실제 WinForms 트리거 |
|---|---|---|
| `OnLoad()` | `GameWindow.OnLoad()` | `GLControl.Load` |
| `OnUpdateFrame(FrameEventArgs)` | `GameWindow.OnUpdateFrame(...)` | `System.Windows.Forms.Timer.Tick` |
| `OnRenderFrame(FrameEventArgs)` | `GameWindow.OnRenderFrame(...)` | `GLControl.Paint` |
| `OnResize(ResizeEventArgs)` | `GameWindow.OnResize(...)` | `GLControl.Resize` |
| `OnFramebufferResize(FramebufferResizeEventArgs)` | `GameWindow.OnFramebufferResize(...)` | `GLControl.Resize` |
| `OnFocusedChanged(bool)` | `GameWindow.OnFocusedChanged(...)` | `GotFocus` / `LostFocus` |
| `OnUnload()` | `GameWindow.OnUnload()` | `GLControl.Disposed` |

`OnUpdateFrame`은 GLControl이 자동으로 제공하지 않는다. 이 예제는 UI 메시지가 없을 때 `Application.Idle`에서 update와 render를 연속 호출한다. 따라서 WinForms `Timer`나 `Paint` 빈도에 묶이지 않고, VSync·GPU·드라이버가 허용하는 최대 프레임률로 실행된다.

```text
Application.Idle (메시지 큐가 비어 있는 동안 반복)
  -> GLView.OnUpdateFrame(deltaTime)
  -> GLView.OnRenderFrame(deltaTime)
  -> SwapBuffers()
```

## OpenGL 리소스 규칙

- 셰이더, VAO/VBO, 텍스처 생성: `OnLoad()`
- 카메라·애니메이션·씬 상태 변경: `OnUpdateFrame()`
- `GL.Clear`, uniform 설정, `GL.Draw*`: `OnRenderFrame()`
- viewport 및 투영 행렬 갱신: `OnResize()` 또는 `OnFramebufferResize()`
- `GL.Delete*` 자원 해제: `OnUnload()`

`GLView`는 렌더 메서드 호출 후 `SwapBuffers()`를 담당한다. 파생 클래스의 `OnRenderFrame()`에서는 중복 호출하지 않는다.

최대 프레임률 모드는 유휴 CPU 코어와 GPU를 계속 사용한다. 일반적인 편집기 화면처럼 정지된 장면이 대부분인 도구는 필요할 때만 `Invalidate()`하는 event-driven 렌더링이 전력 효율적이다.

## 확장 방향

1. `Renderer` 클래스를 분리해 셰이더·메시·텍스처·카메라를 보관한다.
2. TreeView 노드에 씬 오브젝트를 연결하고, 선택 시 Inspector 패널을 추가한다.
3. `StatusChanged` 이벤트에 FPS, OpenGL vendor/version, 선택된 오브젝트 정보를 표시한다.
4. 파일 열기 메뉴와 `OnFileDrop` 성격의 WinForms drag-and-drop을 연결한다.
5. 지속 입력이 필요하면 GLView에서 WinForms `KeyDown`/`KeyUp` 상태를 저장하고 `OnUpdateFrame`에서 처리한다.

## 주의 사항

- WinForms 컨트롤은 UI 스레드에서만 접근한다. OpenGL 호출도 이 예제에서는 같은 UI 스레드에서 수행한다.
- `OnLoad` 이전과 `OnUnload` 이후에는 OpenGL 리소스를 만들거나 호출하지 않는다.
- 프로젝트는 `ApplicationHighDpiMode=SystemAware`와 `AutoScaleMode.Dpi`를 사용한다. OpenTK.GLControl 4.0.0은 내부 GLFW 창을 Win32 자식 창으로 재부모화하고 GLControl의 `Width`/`Height`로 크기를 설정한다. 따라서 이 버전에서는 부모 WinForms와 GLControl의 좌표계를 일치시키는 `SystemAware` 모드가 안정적이다. 앱 시작 시 주 모니터 DPI에 맞춰 UI와 렌더 영역이 함께 스케일된다.
- `MainForm`의 `1280 × 800`은 96 DPI 설계 크기다. 생성 시 현재 `DeviceDpi / 96` 배율을 적용해 `ClientSize`, 최소 크기, splitter 폭을 실제 창 크기로 설정한다. 예를 들어 150% (144 DPI) 모니터에서는 초기 client 영역이 `1920 × 1200`이 된다.
- DPI가 서로 다른 모니터를 오가며 동적으로 재배율하는 `PerMonitorV2`는 이 GLControl 버전과 섞지 않는다. 그렇게 하면 WinForms 논리 좌표와 내부 GLFW 자식 창의 좌표가 달라져 렌더 영역 위치·크기가 어긋날 수 있다.
