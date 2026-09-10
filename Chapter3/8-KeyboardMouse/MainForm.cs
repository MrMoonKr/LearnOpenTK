using System;
using System.Drawing;
using System.Windows.Forms;

namespace LearnOpenTK.KeyboardMouse;

public sealed class MainForm : Form
{
    private const int Dpi = 96;
    private readonly GLView _view = new();
    private readonly ToolStripStatusLabel _status = new("Ready");
    private readonly ToolStripStatusLabel _fps = new("Update: 0.0 FPS | Render: 0.0 FPS") { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly Timer _timer = new() { Interval = 250 };

    public MainForm()
    {
        Text = "LearnOpenTK - Keyboard Mouse";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = Scale(new(1280, 800));
        MinimumSize = Scale(new(800, 500));

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("Exit", null, (_, _) => Close());
        var view = new ToolStripMenuItem("View");
        var vsync = new ToolStripMenuItem("VSync") { CheckOnClick = true, Checked = _view.VSync };
        vsync.CheckedChanged += (_, _) => _view.VSync = vsync.Checked;
        view.DropDownItems.Add(vsync);
        var help = new ToolStripMenuItem("Help");
        help.DropDownItems.Add("Controls", null, (_, _) => MessageBox.Show("Right drag: orbit\nMiddle drag: pan\nWheel: zoom", "Controls"));
        menu.Items.AddRange([file, view, help]);

        var tree = new TreeView { Dock = DockStyle.Fill };
        var scene = tree.Nodes.Add("Scene");
        scene.Nodes.Add("Orbit camera");
        scene.Nodes.Add("Right drag: rotate");
        scene.Nodes.Add("Middle drag: pan");
        scene.Nodes.Add("Wheel: zoom");
        scene.Expand();

        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, Panel1MinSize = Px(180) };
        split.Panel1.Controls.Add(tree);
        split.Panel2.Controls.Add(_view);
        var status = new StatusStrip();
        status.Items.AddRange([_status, _fps]);
        Controls.AddRange([split, status, menu]);
        MainMenuStrip = menu;

        Shown += (_, _) => split.SplitterDistance = (int)Math.Round(split.ClientSize.Width * .3f);
        _view.StatusChanged += (_, s) => _status.Text = s;
        _timer.Tick += (_, _) => _fps.Text = $"Update: {_view.FramesPerSecond:F1} FPS | Render: {_view.FramesPerSecond:F1} FPS";
        _timer.Start();
        FormClosing += (_, _) => _timer.Stop();
    }

    private Size Scale(Size s) => new(Px(s.Width), Px(s.Height));
    private int Px(int p) => (int)Math.Round(p * DeviceDpi / (float)Dpi);
}
