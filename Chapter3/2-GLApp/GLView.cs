using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenTK.WinForms;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;

namespace LearnOpenTK.GLApp;

/// <summary>
/// WinForms GLControl에 GameWindow와 유사한 라이프사이클 콜백을 제공한다.
/// WinForms 이벤트는 이 클래스가 처리하고, 파생 클래스는 OnLoad/OnUpdateFrame/
/// OnRenderFrame 등의 protected virtual 메서드만 재정의하면 된다.
/// </summary>
public class GLView : GLControl
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _previousUpdateTime;
    private TimeSpan _previousRenderTime;
    private TimeSpan _fpsSampleStartTime;
    private int _updatesSinceLastFpsSample;
    private int _rendersSinceLastFpsSample;
    private bool _isLoaded;
    private bool _isUnloaded;
    private bool _isRendering;
    private int _swapInterval;
    private Color _clearColor = Color.FromArgb(31, 52, 79);

    public GLView()
    {
        Dock = DockStyle.Fill;
        TabStop = true;

        Load += HandleLoad;
        Paint += HandlePaint;
        Resize += HandleResize;
        GotFocus += (_, _) => OnFocusedChanged(true);
        LostFocus += (_, _) => OnFocusedChanged(false);
        Disposed += HandleDisposed;
    }

    public event EventHandler<string>? StatusChanged;

    public int UpdateFrameCount { get; private set; }

    public int RenderFrameCount { get; private set; }

    public double UpdateFramesPerSecond { get; private set; }

    public double RenderFramesPerSecond { get; private set; }

    /// <summary>Swap interval 1(VSync) 또는 0(VSync 해제)을 설정한다.</summary>
    public bool VSync
    {
        get => _swapInterval != 0;
        set
        {
            _swapInterval = value ? 1 : 0;
            if (_isLoaded)
            {
                ApplySwapInterval();
            }
        }
    }

    public Color ClearColor
    {
        get => _clearColor;
        set
        {
            _clearColor = value;
            if (_isLoaded)
            {
                MakeCurrent();
                ApplyClearColor();
                Invalidate();
            }
        }
    }

    /// <summary>GameWindow.OnLoad 대응. OpenGL 리소스 생성은 여기서 한다.</summary>
    protected virtual void OnLoad()
    {
    }

    /// <summary>GameWindow.OnUpdateFrame 대응. UI 메시지가 없는 동안 가능한 한 자주 호출한다.</summary>
    protected virtual void OnUpdateFrame(FrameEventArgs e)
    {
    }

    /// <summary>GameWindow.OnRenderFrame 대응. 호출 뒤 GLView가 SwapBuffers를 수행한다.</summary>
    protected virtual void OnRenderFrame(FrameEventArgs e)
    {
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    /// <summary>GameWindow.OnResize 대응. 논리 컨트롤 크기가 바뀔 때 호출한다.</summary>
    protected virtual void OnResize(ResizeEventArgs e)
    {
    }

    /// <summary>GameWindow.OnFramebufferResize 대응. GLControl이 관리하는 렌더 영역 크기로 호출한다.</summary>
    protected virtual void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        GL.Viewport(0, 0, e.Width, e.Height);
    }

    /// <summary>GameWindow.OnFocusedChanged 대응.</summary>
    protected virtual void OnFocusedChanged(bool isFocused)
    {
        ReportStatus(isFocused ? "GLView focused" : "GLView focus lost");
    }

    /// <summary>GameWindow.OnUnload 대응. GPU 리소스 삭제는 컨텍스트가 유효한 이 시점에 한다.</summary>
    protected virtual void OnUnload()
    {
    }

    protected void ReportStatus(string message)
    {
        StatusChanged?.Invoke(this, message);
    }

    private void HandleLoad(object? sender, EventArgs e)
    {
        MakeCurrent();
        ApplyClearColor();
        ApplySwapInterval();
        GL.Enable(EnableCap.DepthTest);

        _previousUpdateTime = _clock.Elapsed;
        _previousRenderTime = _clock.Elapsed;
        _fpsSampleStartTime = _clock.Elapsed;
        _isLoaded = true;
        OnLoad();
        Application.Idle += HandleApplicationIdle;
        ReportStatus($"GLView loaded - VSync: {(VSync ? "On" : "Off")}");
    }

    private void HandleApplicationIdle(object? sender, EventArgs e)
    {
        // 메시지가 도착하기 전까지만 렌더링한다. 이 방식은 WinForms Paint 빈도에 제한되지 않는다.
        while (_isLoaded && !IsDisposed && IsApplicationIdle())
        {
            RunFrame();
        }
    }

    private void RunFrame()
    {
        if (_isRendering)
        {
            return;
        }

        _isRendering = true;
        try
        {
        var now = _clock.Elapsed;
        var updateElapsed = now - _previousUpdateTime;
        _previousUpdateTime = now;
        UpdateFrameCount++;
        _updatesSinceLastFpsSample++;
        OnUpdateFrame(new FrameEventArgs(updateElapsed.TotalSeconds));

        MakeCurrent();
        var renderElapsed = now - _previousRenderTime;
        _previousRenderTime = now;
        RenderFrameCount++;
        _rendersSinceLastFpsSample++;
        OnRenderFrame(new FrameEventArgs(renderElapsed.TotalSeconds));
        SwapBuffers();
        UpdateFrameRates(now);
        }
        finally
        {
            _isRendering = false;
        }
    }

    private void HandlePaint(object? sender, PaintEventArgs e)
    {
        if (!_isLoaded || IsDisposed)
        {
            return;
        }

        RunFrame();
    }

    private void HandleResize(object? sender, EventArgs e)
    {
        if (!_isLoaded || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        OnResize(new ResizeEventArgs(ClientSize.Width, ClientSize.Height));
        UpdateViewport();
    }

    private void HandleDisposed(object? sender, EventArgs e)
    {
        if (_isUnloaded)
        {
            return;
        }

        _isUnloaded = true;
        Application.Idle -= HandleApplicationIdle;

        if (_isLoaded)
        {
            MakeCurrent();
            OnUnload();
        }
    }

    private void ApplyClearColor()
    {
        GL.ClearColor(_clearColor.R / 255f, _clearColor.G / 255f, _clearColor.B / 255f, 1f);
    }

    private void ApplySwapInterval()
    {
        if (Context is not null)
        {
            Context.SwapInterval = _swapInterval;
        }
    }

    private void UpdateFrameRates(TimeSpan now)
    {
        var elapsed = now - _fpsSampleStartTime;
        if (elapsed.TotalSeconds < 1)
        {
            return;
        }

        UpdateFramesPerSecond = _updatesSinceLastFpsSample / elapsed.TotalSeconds;
        RenderFramesPerSecond = _rendersSinceLastFpsSample / elapsed.TotalSeconds;
        _updatesSinceLastFpsSample = 0;
        _rendersSinceLastFpsSample = 0;
        _fpsSampleStartTime = now;
    }

    private void UpdateViewport()
    {
        if (!_isLoaded || IsDisposed || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        MakeCurrent();
        // OpenTK.GLControl 4.0.0은 내부 GLFW 자식 창을 Width/Height로 리사이즈한다.
        // SystemAware 모드에서는 GLControl과 내부 창이 같은 좌표계를 사용한다.
        OnFramebufferResize(new FramebufferResizeEventArgs(ClientSize.Width, ClientSize.Height));
        ReportStatus($"Viewport: {ClientSize.Width} x {ClientSize.Height}");
    }

    private static bool IsApplicationIdle()
    {
        return !PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
    }

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out NativeMessage message, IntPtr windowHandle, uint minimumMessage, uint maximumMessage, uint removeMessage);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Handle;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Point;
    }
}
