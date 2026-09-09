using System;
using System.Drawing;
using System.Windows.Forms;

namespace LearnOpenTK.GLApp;

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
        Text = "LearnOpenTK - GL Tool App";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        // 코드에서 지정하는 크기를 96 DPI 설계 크기로 보고, 시작 모니터 DPI에 맞춰 실제 창 크기를 정한다.
        ClientSize = ScaleDesignSize(DesignClientSize);
        MinimumSize = ScaleDesignSize(DesignMinimumSize);

        var menu = CreateMenu();
        var explorer = CreateExplorer();
        var layout = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            Panel1MinSize = ScaleDesignPixels(180),
        };
        layout.Panel1.Controls.Add(explorer);
        layout.Panel2.Controls.Add(_glView);

        var status = new StatusStrip();
        status.Items.Add(_statusLabel);
        status.Items.Add(_frameLabel);

        Controls.Add(layout);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;

        // 레이아웃 완료 후 좌측 탐색기를 사용 가능한 폭의 30%로 배치한다.
        Shown += (_, _) => layout.SplitterDistance = (int)Math.Round(layout.ClientSize.Width * 0.30f);

        _glView.StatusChanged += (_, message) => _statusLabel.Text = message;
        _statusTimer.Tick += (_, _) => UpdateFrameStatus();
        _statusTimer.Start();
        FormClosing += (_, _) => _statusTimer.Stop();
    }

    private MenuStrip CreateMenu()
    {
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
        help.DropDownItems.Add("About", null, (_, _) => MessageBox.Show(
            "GameWindow-style lifecycle callbacks hosted in a WinForms GLControl.",
            "About GL Tool App", MessageBoxButtons.OK, MessageBoxIcon.Information));

        menu.Items.AddRange([file, view, help]);
        return menu;
    }

    private TreeView CreateExplorer()
    {
        var tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
        var scene = tree.Nodes.Add("Scene");
        scene.Nodes.Add("Blue Background").Tag = Color.FromArgb(31, 52, 79);
        scene.Nodes.Add("Green Background").Tag = Color.FromArgb(26, 78, 58);
        scene.Nodes.Add("Gray Background").Tag = Color.FromArgb(55, 55, 55);
        scene.Expand();

        tree.AfterSelect += (_, e) =>
        {
            var node = e.Node;
            if (node?.Tag is Color color)
            {
                _glView.ClearColor = color;
                _statusLabel.Text = $"Selected: {node.Text}";
            }
        };
        return tree;
    }

    private void UpdateFrameStatus()
    {
        _frameLabel.Text = $"Update: {_glView.UpdateFramesPerSecond:F1} FPS | Render: {_glView.RenderFramesPerSecond:F1} FPS";
    }

    private Size ScaleDesignSize(Size designSize) => new(
        ScaleDesignPixels(designSize.Width),
        ScaleDesignPixels(designSize.Height));

    private int ScaleDesignPixels(int designPixels) =>
        (int)Math.Round(designPixels * (DeviceDpi / (float)DesignDpi));
}
