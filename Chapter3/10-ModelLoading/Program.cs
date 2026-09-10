using System;
using System.Windows.Forms;

namespace LearnOpenTK.ModelLoading;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
