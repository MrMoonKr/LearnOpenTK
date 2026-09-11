using System;
using System.Drawing;
using System.Windows.Forms;

namespace LearnOpenTK.CharacterController;

public sealed class MainForm : Form
{
    private const int DesignDpi = 96;
    private readonly GLView _view = new();
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly ToolStripStatusLabel _status = new("Ready");
    private readonly ToolStripStatusLabel _fps = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly Timer _timer = new() { Interval = 250 };
    private bool _treeBuilt;

    public MainForm()
    {
        Text = "LearnOpenTK - Character Controller";
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
            "Pick a hero from the tree to spawn it on a flat ground plane. W/A/S/D or the arrow keys walk " +
            "it around relative to the current camera direction (like Unity's CharacterController.Move, " +
            "orbiting the camera turns \"forward\" with it); Space jumps (gravity/ground-snap against the " +
            "flat plane), F attacks. Idle/Run/Attack/Jump/Fall clips are inferred from each hero's own " +
            "animation list (see the README) instead of being hardcoded, and cached under .cache/animations " +
            "so the inference is not redone every time a hero is selected - most Dota heroes have no real " +
            "jump/fall clip, so those states usually fall back to reusing Idle/Run. Right-drag orbits the " +
            "camera, middle-drag pans, and the wheel zooms; the camera otherwise follows the walking hero.",
            "Character Controller"));
        menu.Items.AddRange([file, view, help]);

        _tree.Nodes.Add("Loading...");
        _tree.AfterSelect += (_, e) => { if (e.Node?.Tag is string npcName) _view.SelectHero(npcName); };

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
        if (_treeBuilt) return;
        if (_view.Roster.Count == 0) return;

        _tree.Nodes.Clear();
        var root = _tree.Nodes.Add($"Heroes ({_view.Roster.Count})");
        foreach (var (npcName, displayName) in _view.Roster)
        {
            var node = root.Nodes.Add(displayName);
            node.Tag = npcName;
        }
        root.Expand();
        _treeBuilt = true;
    }

    private Size Scale(Size size) => new(Px(size.Width), Px(size.Height));
    private int Px(int value) => (int)Math.Round(value * DeviceDpi / (float)DesignDpi);
}
