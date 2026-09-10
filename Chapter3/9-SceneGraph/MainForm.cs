using System;
using System.Drawing;
using System.Windows.Forms;

namespace LearnOpenTK.SceneGraph;

public sealed class MainForm : Form
{
    private readonly GLView _view = new();
    private readonly ToolStripStatusLabel _status = new("Ready");
    private readonly ToolStripStatusLabel _fps = new("Update: 0.0 FPS | Render: 0.0 FPS") { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly Timer _timer = new() { Interval = 250 };

    public MainForm()
    {
        Text = "LearnOpenTK - Scene Graph";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(800, 500);

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("Exit", null, (_, _) => Close());
        var view = new ToolStripMenuItem("View");
        var vsync = new ToolStripMenuItem("VSync") { CheckOnClick = true, Checked = _view.VSync };
        vsync.CheckedChanged += (_, _) => _view.VSync = vsync.Checked;
        view.DropDownItems.Add(vsync);
        var help = new ToolStripMenuItem("Help");
        help.DropDownItems.Add("About", null, (_, _) => MessageBox.Show("10 rotating groups, each with 10 cube nodes.", "Scene Graph"));
        menu.Items.AddRange([file, view, help]);

        var tree = new TreeView { Dock = DockStyle.Fill };
        var root = tree.Nodes.Add("Scene (100 cubes)");
        for (var g = 0; g < 10; g++)
        {
            var group = root.Nodes.Add($"Group {g:00}");
            for (var c = 0; c < 10; c++) group.Nodes.Add($"Cube {g * 10 + c:00}");
        }
        root.Nodes.Add("Lights: Key / Fill / Rim");
        root.Expand();

        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, Panel1MinSize = 180, SplitterDistance = 384 };
        split.Panel1.Controls.Add(tree);
        split.Panel2.Controls.Add(_view);
        var status = new StatusStrip();
        status.Items.AddRange([_status, _fps]);
        Controls.AddRange([split, status, menu]);
        MainMenuStrip = menu;

        _view.StatusChanged += (_, s) => _status.Text = s;
        _timer.Tick += (_, _) => _fps.Text = $"Update: {_view.FramesPerSecond:F1} FPS | Render: {_view.FramesPerSecond:F1} FPS";
        _timer.Start();
        FormClosing += (_, _) => _timer.Stop();
    }
}
