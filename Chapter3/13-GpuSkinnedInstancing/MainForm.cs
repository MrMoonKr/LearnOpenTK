using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LearnOpenTK.GpuSkinnedInstancing;

public sealed class MainForm : Form
{
    private const int DesignDpi = 96;
    private readonly GLView _view = new();
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly ToolStripStatusLabel _status = new("Ready");
    private readonly ToolStripStatusLabel _fps = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly Timer _timer = new() { Interval = 250 };

    public MainForm()
    {
        Text = "LearnOpenTK - GPU Skinned Instancing";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = Scale(new Size(1280, 800));
        MinimumSize = Scale(new Size(800, 500));

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("Exit", null, (_, _) => Close());
        var view = new ToolStripMenuItem("View");
        var vsync = new ToolStripMenuItem("VSync") { CheckOnClick = true, Checked = _view.VSync };
        vsync.CheckedChanged += (_, _) => _view.VSync = vsync.Checked;
        view.DropDownItems.Add(vsync);
        var help = new ToolStripMenuItem("Help");
        help.DropDownItems.Add("About", null, (_, _) => MessageBox.Show(
            "Draws every instance of the hero body and its default wearables with one GL.DrawElementsInstanced call per part. Each instance's bone matrix palette lives in a shader storage buffer indexed by gl_InstanceID, so every instance can play a different animation clip at a different point in time on otherwise identical, shared GPU geometry (compare 12-GpuSkinning, which skins a single instance from a plain uniform array).",
            "GPU Skinned Instancing"));
        menu.Items.AddRange([file, view, help]);

        _tree.Nodes.Add("Loading...");

        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, Panel1MinSize = Px(180) };
        split.Panel1.Controls.Add(_tree);
        split.Panel2.Controls.Add(_view);
        var status = new StatusStrip();
        status.Items.AddRange([_status, _fps]);
        Controls.AddRange([split, status, menu]);
        MainMenuStrip = menu;

        Shown += (_, _) => split.SplitterDistance = (int)Math.Round(split.ClientSize.Width * .3f);
        _view.StatusChanged += (_, s) => RefreshScene(s);
        _timer.Tick += (_, _) => _fps.Text = $"Render: {_view.FramesPerSecond:F1} FPS";
        _timer.Start();
        FormClosing += (_, _) => _timer.Stop();
    }

    private void RefreshScene(string statusText)
    {
        _status.Text = statusText;

        _tree.Nodes.Clear();
        var root = _tree.Nodes.Add($"Scene ({_view.InstanceCount} instances)");
        foreach (var name in _view.RoleNames) root.Nodes.Add($"{name} (x{_view.InstanceCount})");
        root.Expand();
    }

    private Size Scale(Size size) => new(Px(size.Width), Px(size.Height));
    private int Px(int value) => (int)Math.Round(value * DeviceDpi / (float)DesignDpi);
}
