using System;
using System.ComponentModel;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace LearnOpenTK;

/// <summary>
/// GameWindow의 주요 라이프사이클 콜백이 어느 시점에 호출되는지 보여 주는 예제다.
/// Esc를 누르거나 창을 닫으면 종료된다.
/// </summary>
public class Window : GameWindow
{
    private int _updateFrameCount;
    private int _renderFrameCount;

    public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings)
    {
    }

    // OpenGL 컨텍스트가 준비된 뒤, Run()당 한 번 호출된다.
    protected override void OnLoad()
    {
        base.OnLoad();

        GL.ClearColor(0.12f, 0.18f, 0.28f, 1.0f);
        Console.WriteLine("OnLoad: OpenGL 초기화가 가능합니다.");
    }

    // 업데이트 주기마다 호출된다. e.Time은 직전 업데이트 이후 경과 시간(초)이다.
    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        _updateFrameCount++;
        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
            return;
        }

        UpdateTitle();
    }

    // 렌더 주기마다 호출된다. SwapBuffers()로 back buffer를 화면에 표시한다.
    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        _renderFrameCount++;
        GL.Clear(ClearBufferMask.ColorBufferBit);
        SwapBuffers();
    }

    // 논리 창 크기가 바뀔 때 호출된다.
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        Console.WriteLine($"OnResize: {e.Width} x {e.Height}");
    }

    // 실제 렌더 대상(framebuffer) 크기가 바뀔 때 호출된다. HiDPI 화면에서는 Size와 다를 수 있다.
    protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        base.OnFramebufferResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
        Console.WriteLine($"OnFramebufferResize: {e.Width} x {e.Height}");
    }

    // 창의 포커스 획득/상실 때 호출된다.
    protected override void OnFocusedChanged(FocusedChangedEventArgs e)
    {
        base.OnFocusedChanged(e);
        Console.WriteLine($"OnFocusedChanged: IsFocused = {IsFocused}");
    }

    // 닫기 요청 직전에 호출된다. e.Cancel을 true로 설정하면 종료를 취소할 수 있다.
    protected override void OnClosing(CancelEventArgs e)
    {
        Console.WriteLine("OnClosing: 창 닫기 요청을 처리합니다.");
        base.OnClosing(e);
    }

    // Run 루프가 끝난 뒤 한 번 호출된다. GPU 리소스를 직접 관리했다면 여기서 해제한다.
    protected override void OnUnload()
    {
        Console.WriteLine("OnUnload: GameWindow 라이프사이클이 종료되었습니다.");
        base.OnUnload();
    }

    private void UpdateTitle()
    {
        Title = $"GameWindow Lifecycle | Update: {_updateFrameCount} | Render: {_renderFrameCount} | Esc: Close";
    }
}
