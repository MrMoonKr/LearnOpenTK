# 1-GameWindow: GameWindow 라이프사이클과 GLFW 콜백

이 예제는 OpenTK `GameWindow`의 주요 콜백이 언제 호출되는지 확인하기 위한 프로젝트다. 창 제목에는 update/render 프레임 수가 나타나며, 창 크기·포커스·종료 관련 이벤트는 콘솔에 기록된다.

## 실행

```powershell
dotnet run --project Chapter3/1-GameWindow/1-GameWindow.csproj
```

- `Esc`: 창 닫기
- 창 가장자리 드래그: `OnResize`, `OnFramebufferResize` 확인
- 다른 창을 선택하거나 다시 선택: `OnFocusedChanged` 확인
- 닫기 버튼: `OnClosing`, `OnUnload` 확인

이 프로젝트는 `OutputType`이 `Exe`이므로 PowerShell이나 명령 프롬프트에서 실행하면 콜백 기록을 볼 수 있다.

## 계층 구조

```text
애플리케이션 코드
  Window : GameWindow
    NativeWindow
      OpenTK.Windowing.GraphicsLibraryFramework.GLFW
        GLFW 네이티브 라이브러리
          운영체제의 창·입력 이벤트
```

- `GameWindow`: OpenGL 컨텍스트와 update/render 루프를 제공한다.
- `NativeWindow`: 창 크기, 위치, 포커스 및 입력 이벤트의 기반을 제공한다.
- `GLFW`: OpenTK가 창 생성, OS 이벤트 수신, 컨텍스트 생성에 사용하는 저수준 바인딩이다.

일반적인 OpenTK 앱은 `GLFW.SetKeyCallback` 같은 GLFW 함수를 직접 등록할 필요가 없다. `GameWindow`를 상속하고 `OnKeyDown`을 오버라이드하거나 `KeyboardState`를 조회하면 OpenTK가 GLFW 이벤트를 처리해 준다.

## 두 종류의 콜백

### 1. GLFW 이벤트에서 전달되는 콜백

운영체제에서 창이나 입력 이벤트가 발생하면 GLFW가 먼저 받는다. OpenTK는 등록해 둔 `GLFWCallbacks.*Callback` 델리게이트를 통해 해당 이벤트를 받고, `NativeWindow` 상태를 갱신한 뒤 대응되는 `On...` 메서드를 호출한다.

```text
마우스 이동
  -> GLFWCallbacks.CursorPosCallback
  -> OpenTK의 MouseState 갱신
  -> Window.OnMouseMove(MouseMoveEventArgs)
```

### 2. `GameWindow.Run()`이 관리하는 프레임 콜백

`OnLoad`, `OnUpdateFrame`, `OnRenderFrame`, `OnUnload`는 특정 GLFW 이벤트의 단순한 래퍼가 아니다. `Run()`이 시간과 이벤트 처리를 관리하면서 호출하는 고수준 게임 루프 콜백이다.

```text
Program.Main
  -> new Window(...)
  -> window.Run()
       -> OnLoad()                         // 한 번
       -> 이벤트 폴링 및 GLFW 콜백 처리
       -> OnUpdateFrame(e)                 // 반복
       -> OnRenderFrame(e) -> SwapBuffers  // 반복
       -> OnUnload()                       // 종료 시 한 번
```

`UpdateFrequency`와 `RenderFrequency`는 `GameWindowSettings`에서 조절할 수 있다. 둘은 별도의 주기로 동작할 수 있으므로, 게임 상태 변경은 대체로 `OnUpdateFrame`, OpenGL 그리기는 `OnRenderFrame`에 둔다.

## 현재 `Window.cs`의 콜백

| 메서드 | 호출 성격 | 이 예제에서 하는 일 |
|---|---|---|
| `OnLoad()` | `Run()` 시작 뒤 한 번 | OpenGL clear color를 설정하고 초기화 메시지 출력 |
| `OnUpdateFrame(FrameEventArgs)` | 매 update tick | 프레임 수 증가, Esc 종료 처리, 창 제목 갱신 |
| `OnRenderFrame(FrameEventArgs)` | 매 render tick | color buffer clear 후 `SwapBuffers()` |
| `OnResize(ResizeEventArgs)` | 창의 논리 크기 변경 | 새 크기를 콘솔에 기록 |
| `OnFramebufferResize(FramebufferResizeEventArgs)` | 실제 framebuffer 크기 변경 | `GL.Viewport` 갱신 및 새 크기 기록 |
| `OnFocusedChanged(FocusedChangedEventArgs)` | 포커스 변경 | 현재 포커스 상태 기록 |
| `OnClosing(CancelEventArgs)` | 종료 직전 | 종료 요청을 기록. `e.Cancel = true`면 취소 가능 |
| `OnUnload()` | 루프 종료 뒤 한 번 | 마지막 정리 메시지 출력 |

## GLFW 콜백과 OpenTK 메서드 대응

| GLFW의 저수준 콜백 | OpenTK 오버라이드 | 용도 |
|---|---|---|
| `WindowPosCallback` | `OnMove(WindowPositionEventArgs)` | 창 위치 변경 |
| `WindowSizeCallback` | `OnResize(ResizeEventArgs)` | 논리 창 크기 변경 |
| `FramebufferSizeCallback` | `OnFramebufferResize(FramebufferResizeEventArgs)` | 실제 픽셀 렌더 영역 크기 변경 |
| `WindowFocusCallback` | `OnFocusedChanged(FocusedChangedEventArgs)` | 포커스 변경 |
| `WindowCloseCallback` | `OnClosing(CancelEventArgs)` | 닫기 요청 |
| `WindowRefreshCallback` | `OnRefresh()` | OS의 다시 그리기 요청 |
| `WindowIconifyCallback` | `OnMinimized(MinimizedEventArgs)` | 최소화/복원 |
| `WindowMaximizeCallback` | `OnMaximized(MaximizedEventArgs)` | 최대화 상태 변경 |
| `KeyCallback` | `OnKeyDown`, `OnKeyUp` | 키 눌림/해제 |
| `CharCallback` | `OnTextInput(TextInputEventArgs)` | 문자 입력. 텍스트 입력 UI에 사용 |
| `MouseButtonCallback` | `OnMouseDown`, `OnMouseUp` | 마우스 버튼 |
| `CursorPosCallback` | `OnMouseMove(MouseMoveEventArgs)` | 포인터 이동 |
| `ScrollCallback` | `OnMouseWheel(MouseWheelEventArgs)` | 휠 이동 |
| `CursorEnterCallback` | `OnMouseEnter`, `OnMouseLeave` | 커서 진입/이탈 |
| `DropCallback` | `OnFileDrop(FileDropEventArgs)` | 파일 드롭 |
| `JoystickCallback` | `OnJoystickConnected(JoystickEventArgs)` | 조이스틱 연결 변경 |

입력 상태를 매 프레임 확인하고 싶으면 `OnUpdateFrame`에서 `KeyboardState`와 `MouseState`를 사용한다. 한 번 발생한 입력 자체를 처리하고 싶으면 `OnKeyDown`, `OnMouseDown`, `OnMouseWheel` 같은 이벤트 콜백을 오버라이드한다.

```csharp
protected override void OnUpdateFrame(FrameEventArgs e)
{
    base.OnUpdateFrame(e);

    if (KeyboardState.IsKeyDown(Keys.Escape))
    {
        Close();
    }
}
```

## `OnResize`와 `OnFramebufferResize`의 차이

`OnResize`의 `e.Width`, `e.Height`는 창의 논리 크기다. 고해상도(HiDPI) 모니터에서는 화면 배율 때문에 실제 framebuffer의 픽셀 수가 논리 크기와 다를 수 있다.

OpenGL viewport는 실제 픽셀 크기를 사용해야 하므로 이 예제는 `OnFramebufferResize`에서 갱신한다.

```csharp
protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
{
    base.OnFramebufferResize(e);
    GL.Viewport(0, 0, e.Width, e.Height);
}
```

카메라 투영 행렬의 화면 비율도 framebuffer 기준으로 계산하면 HiDPI 환경에서 더 일관된 결과를 얻을 수 있다.

## 렌더링의 최소 단위

이 예제는 화면을 지우는 작업만 한다.

```csharp
protected override void OnRenderFrame(FrameEventArgs e)
{
    base.OnRenderFrame(e);

    GL.Clear(ClearBufferMask.ColorBufferBit);
    SwapBuffers();
}
```

OpenTK 창은 이중 버퍼를 사용한다. `GL.Clear`와 draw 명령은 back buffer에 적용되고, `SwapBuffers()`가 그 결과를 화면에 보이는 front buffer와 교체한다. 이 호출이 없으면 그린 결과가 보이지 않는다.

## 주의 사항

- OpenGL 함수 호출은 `OnLoad` 이후, 유효한 컨텍스트가 있을 때 수행한다.
- `OnRenderFrame`에서는 매 프레임 리소스를 생성하지 말고, 생성은 `OnLoad`에 둔다.
- 이동처럼 지속되는 동작은 `IsKeyDown`과 `e.Time`을 조합한다. 그래야 프레임 속도에 따라 속도가 달라지지 않는다.
- `OnFocusedChanged`에서 포커스를 잃었을 때 입력이나 오디오를 일시 정지하면 의도치 않은 조작을 줄일 수 있다.
- GLFW API를 직접 사용하면서 `GameWindow`의 같은 입력 처리를 중복 구현하면 상태나 콜백이 중복될 수 있다. 특별한 이유가 없다면 `GameWindow` API를 우선한다.
