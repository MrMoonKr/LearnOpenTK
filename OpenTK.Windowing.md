# OpenTK.Windowing 정리

이 문서는 이 저장소가 참조하는 **OpenTK 4.8.2** 기준이다. Windowing은 창 생성, OpenGL 컨텍스트, 프레임 루프, 키보드·마우스 입력을 담당한다. 실제 OpenGL 명령 호출은 `OpenTK.Graphics.OpenGL4.GL`에 맡긴다.

## 네임스페이스와 책임

| 네임스페이스 | 책임 | 이 저장소에서의 사용 |
|---|---|---|
| `OpenTK.Windowing.Desktop` | 데스크톱 창과 게임 루프 | `GameWindow`, 설정 객체 |
| `OpenTK.Windowing.Common` | 플랫폼 공통 이벤트·컨텍스트 설정 | 프레임/리사이즈 이벤트, `ContextFlags`, `CursorState` |
| `OpenTK.Windowing.GraphicsLibraryFramework` | GLFW 기반의 저수준 입력·창 API | `Keys`, `KeyboardState`, `MouseState` |

`Desktop` API가 일반적인 OpenTK 앱의 출발점이다. `GraphicsLibraryFramework.GLFW`를 직접 호출할 수도 있지만, 보통 `GameWindow`가 더 안전하고 간결하다.

## 핵심 클래스

### `GameWindow`

`GameWindow`는 `NativeWindow`를 확장한 렌더링용 창 클래스다. OpenGL 컨텍스트를 보유하며, `Run()`이 이벤트 처리와 업데이트·렌더 루프를 수행한다. 이 저장소의 모든 예제 `Window` 클래스는 이를 상속한다.

| 멤버/콜백 | 호출 시점 | 주된 역할 |
|---|---|---|
| `OnLoad()` | `Run()` 시작 후 한 번 | VAO/VBO, 셰이더, 텍스처 등 GPU 리소스 생성 |
| `OnUpdateFrame(FrameEventArgs e)` | 업데이트 주기마다 | 입력 처리와 게임 상태 갱신. `e.Time`은 이전 업데이트부터의 초 |
| `OnRenderFrame(FrameEventArgs e)` | 렌더 주기마다 | 화면 지우기, 상태 바인딩, draw 호출, `SwapBuffers()` |
| `OnResize(ResizeEventArgs e)` | 창 크기 변경 때 | `GL.Viewport`와 카메라 aspect ratio 갱신 |
| `OnFramebufferResize(FramebufferResizeEventArgs e)` | 실제 framebuffer 크기 변경 때 | HiDPI 환경에서 framebuffer 기준 viewport 갱신에 적합 |
| `OnMouseWheel(MouseWheelEventArgs e)` | 휠 입력 때 | 카메라 FOV/줌 등 처리 |
| `OnUnload()` | 종료/해제 시점 | 아직 사용하는 리소스만 정리할 때 사용 |

유용한 속성은 `KeyboardState`, `MouseState`, `IsFocused`, `Size`, `FramebufferSize`, `CursorState`, `VSync`이다. 창을 종료할 때는 `Close()`를 호출한다.

### `NativeWindow`

렌더 루프 자체가 필요 없는 기본 창 클래스다. 창 상태, 위치, 크기, 포커스, 파일 드롭과 같은 기능의 기반을 제공한다. 렌더링 애플리케이션은 일반적으로 `GameWindow`를 사용한다.

### 설정 객체

| 타입 | 주요 설정 |
|---|---|
| `GameWindowSettings` | `UpdateFrequency`, `RenderFrequency` 등 루프 주기 |
| `NativeWindowSettings` | `ClientSize`, `Title`, `Location`, `WindowState`, `WindowBorder`, OpenGL API/버전/프로필, 아이콘 |
| `ContextFlags` | `ForwardCompatible`, `Debug` 등 컨텍스트 플래그 |
| `ContextAPI` / `ContextProfile` | OpenGL/OpenGL ES 선택, Core/Compatibility 프로필 |
| `VSyncMode` | `On`, `Off`, `Adaptive` 동기화 모드 |

macOS에서 Core OpenGL 컨텍스트를 만들 때 이 저장소처럼 `Flags = ContextFlags.ForwardCompatible`가 필요할 수 있다.

```csharp
var nativeSettings = new NativeWindowSettings
{
    ClientSize = new Vector2i(800, 600),
    Title = "LearnOpenTK",
    Flags = ContextFlags.ForwardCompatible,
};

using var window = new Window(GameWindowSettings.Default, nativeSettings);
window.Run();
```

## 입력 API

### 키보드

`KeyboardState`와 `Keys`를 함께 사용한다.

```csharp
if (KeyboardState.IsKeyDown(Keys.Escape)) Close();
if (KeyboardState.IsKeyPressed(Keys.Space)) { /* 이번 프레임에 눌림 */ }
```

- `IsKeyDown`: 누르고 있는 동안 참이다. 이동처럼 연속 동작에 적합하다.
- `IsKeyPressed`: 직전 프레임에는 눌리지 않았고 이번 프레임에 눌린 경우다. 토글에 적합하다.
- `Keys`: Escape, W/A/S/D, Space, LeftShift 등 키 코드 enum이다.

### 마우스

`MouseState`는 `X`, `Y`, `Position`, 버튼 상태를 제공하며, `MouseWheelEventArgs.Offset`/`OffsetY`로 휠 변위를 받는다. FPS 카메라는 첫 프레임에 기준 좌표를 저장하고 이후 좌표 차이(delta)로 yaw/pitch를 갱신한다.

```csharp
CursorState = CursorState.Grabbed; // 커서를 숨기고 창에 고정
var mouse = MouseState;
var deltaX = mouse.X - lastPos.X;
var deltaY = mouse.Y - lastPos.Y;
```

`CursorState`의 대표 값은 `Normal`, `Hidden`, `Grabbed`다. 입력은 포커스를 잃었을 때 의도치 않게 적용되지 않도록 `IsFocused`를 먼저 확인하는 것이 좋다.

## 실행 흐름

```text
Program.Main
  -> NativeWindowSettings / GameWindowSettings 생성
  -> GameWindow 파생 Window 생성
  -> Run()
       -> OnLoad() 한 번
       -> [OnUpdateFrame() -> OnRenderFrame() -> SwapBuffers()] 반복
       -> OnUnload()
```

`OnLoad` 이후에는 현재 OpenGL 컨텍스트가 활성화되어 있으므로 GL 리소스를 생성할 수 있다. `OnRenderFrame` 끝에서 `SwapBuffers()`를 빼먹으면 back buffer에 그린 결과가 화면에 나타나지 않는다.

## 저수준 GLFW 계층

`OpenTK.Windowing.GraphicsLibraryFramework`는 OpenTK가 사용하는 GLFW 바인딩이다. 주요 타입은 다음과 같다.

- `GLFW`: 초기화, 창/모니터, 입력을 직접 다루는 정적 API
- `KeyboardState`, `MouseState`, `GamepadState`, `JoystickState`: 장치 상태
- `Keys`, `MouseButton`, `InputAction`, `KeyModifiers`: 입력 코드와 상태
- `Monitor`, `VideoMode`, `GammaRamp`: 모니터 및 디스플레이 정보
- `Cursor`, `CursorShape`, `Window`, `WindowHint*`, `WindowAttribute*`: 저수준 창·커서 설정
- `GLFWCallbacks.*Callback`: GLFW 콜백 델리게이트

학습 예제와 대부분의 일반 앱은 `GLFW`를 직접 호출하지 않고 `GameWindow`의 상태와 오버라이드 콜백을 사용하면 된다.

## 이 저장소에서 확인할 위치

- 기본 창·종료 처리: `Chapter1/1-CreatingAWindow/Window.cs`
- 렌더 루프, 리사이즈, 리소스 해제: `Chapter1/2-HelloTriangle/Window.cs`
- 키보드·마우스 FPS 카메라 및 휠 FOV: `Chapter1/9-Camera/Window.cs`
- 다중 조명 예제의 완성된 입력 처리: `Chapter2/6-MultipleLights/Window.cs`
