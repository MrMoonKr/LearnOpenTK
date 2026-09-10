using System;
using System.Drawing;
using System.Windows.Forms;

namespace LearnOpenTK.GpuPbr;

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
        Text = "LearnOpenTK - GPU PBR";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = Scale(new Size(1280, 800));
        MinimumSize = Scale(new Size(800, 500));

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("Reroll Heroes", null, (_, _) => _view.Reroll());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Exit", null, (_, _) => Close());
        var view = new ToolStripMenuItem("View");
        var vsync = new ToolStripMenuItem("VSync") { CheckOnClick = true, Checked = _view.VSync };
        vsync.CheckedChanged += (_, _) => _view.VSync = vsync.Checked;
        view.DropDownItems.Add(vsync);
        var help = new ToolStripMenuItem("Help");
        help.DropDownItems.Add("About", null, (_, _) => MessageBox.Show(
            "Places a random cast of Dota 2 heroes (Axe always included) side by side, each with its own default wearables, rendered with Dota 2's real material textures (albedo, normal, and the two mask maps) plus a flat ambient term so shadowed sides aren't pure black. File > Reroll Heroes picks a fresh cast.",
            "GPU PBR"));
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
        var root = _tree.Nodes.Add($"Scene ({_view.Characters.Count} characters)");
        foreach (var (heroName, partNames) in _view.Characters)
        {
            var heroNode = root.Nodes.Add(heroName);
            foreach (var partName in partNames) heroNode.Nodes.Add(partName);
        }
        root.ExpandAll();
    }

    private Size Scale(Size size) => new(Px(size.Width), Px(size.Height));
    private int Px(int value) => (int)Math.Round(value * DeviceDpi / (float)DesignDpi);
}
