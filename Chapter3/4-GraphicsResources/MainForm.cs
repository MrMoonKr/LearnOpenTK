using System;
using System.Drawing;
using System.Windows.Forms;

namespace LearnOpenTK.GraphicsResources;

public sealed class MainForm : Form
{
    private const int DesignDpi = 96;
    private static readonly Size DesignClientSize = new(1280, 800);
    private static readonly Size DesignMinimumSize = new(800, 500);
    private readonly GLView _glView = new();
    private readonly ToolStripStatusLabel _statusLabel = new("Ready");
    private readonly ToolStripStatusLabel _frameLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly Timer _statusTimer = new() { Interval = 250 };

    public MainForm()
    {
        Text = "LearnOpenTK - Graphics Resources";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = ScaleDesignSize(DesignClientSize);
        MinimumSize = ScaleDesignSize(DesignMinimumSize);

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("Exit", null, (_, _) => Close());
        var view = new ToolStripMenuItem("View");
        var vsync = new ToolStripMenuItem("VSync") { CheckOnClick = true, Checked = _glView.VSync };
        vsync.CheckedChanged += (_, _) => _glView.VSync = vsync.Checked;
        view.DropDownItems.Add(vsync);
        view.DropDownItems.Add(new ToolStripSeparator());
        view.DropDownItems.Add("Blue clear color", null, (_, _) => _glView.ClearColor = Color.FromArgb(31, 52, 79));
        view.DropDownItems.Add("Green clear color", null, (_, _) => _glView.ClearColor = Color.FromArgb(26, 78, 58));
        view.DropDownItems.Add("Gray clear color", null, (_, _) => _glView.ClearColor = Color.FromArgb(55, 55, 55));
        var help = new ToolStripMenuItem("Help");
        help.DropDownItems.Add("About", null, (_, _) => MessageBox.Show("OpenGL resource ownership with GlBuffer and VertexArray.", "Graphics Resources"));
        menu.Items.AddRange([file, view, help]);

        var tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
        var scene = tree.Nodes.Add("Scene");
        scene.Nodes.Add("Blue Background").Tag = Color.FromArgb(31, 52, 79);
        scene.Nodes.Add("Green Background").Tag = Color.FromArgb(26, 78, 58);
        scene.Nodes.Add("Gray Background").Tag = Color.FromArgb(55, 55, 55);
        scene.Expand();
        tree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is Color color)
            {
                _glView.ClearColor = color;
                _statusLabel.Text = $"Selected: {e.Node.Text}";
            }
        };

        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, Panel1MinSize = ScaleDesignPixels(180) };
        split.Panel1.Controls.Add(tree);
        split.Panel2.Controls.Add(_glView);
        var status = new StatusStrip();
        status.Items.AddRange([_statusLabel, _frameLabel]);
        Controls.AddRange([split, status, menu]);
        MainMenuStrip = menu;

        Shown += (_, _) => split.SplitterDistance = (int)Math.Round(split.ClientSize.Width * 0.30f);
        _glView.StatusChanged += (_, text) => _statusLabel.Text = text;
        _statusTimer.Tick += (_, _) => _frameLabel.Text = $"Render: {_glView.FramesPerSecond:F1} FPS";
        _statusTimer.Start();
        FormClosing += (_, _) => _statusTimer.Stop();
    }

    private Size ScaleDesignSize(Size designSize) => new(ScaleDesignPixels(designSize.Width), ScaleDesignPixels(designSize.Height));
    private int ScaleDesignPixels(int designPixels) => (int)Math.Round(designPixels * (DeviceDpi / (float)DesignDpi));
}
