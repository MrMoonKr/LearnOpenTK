using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace LearnOpenTK;

internal static class Program
{
    private static void Main()
    {
        var nativeWindowSettings = new NativeWindowSettings
        {
            ClientSize = new Vector2i(800, 600),
            Title = "LearnOpenTK - GameWindow Lifecycle",
            Flags = ContextFlags.ForwardCompatible,
        };

        using var window = new Window(GameWindowSettings.Default, nativeWindowSettings);
        window.Run();
    }
}
