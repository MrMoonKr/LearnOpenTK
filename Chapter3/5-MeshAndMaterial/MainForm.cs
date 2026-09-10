using System;
using System.Drawing;
using System.Windows.Forms;

namespace LearnOpenTK.MeshAndMaterial;

public sealed class MainForm : Form
{
    private const int DesignDpi = 96;
    private readonly GLView _glView = new();
    private readonly ToolStripStatusLabel _status = new("Ready");
    private readonly ToolStripStatusLabel _fps = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly Timer _timer = new() { Interval = 250 };

    public MainForm()
    {
        Text = "LearnOpenTK - Mesh and Material";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = Scale(new Size(1280, 800));
        MinimumSize = Scale(new Size(800, 500));

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
        help.DropDownItems.Add("About", null, (_, _) => MessageBox.Show("Mesh owns geometry; Material selects a shader.", "Mesh and Material"));
        menu.Items.AddRange([file, view, help]);

        var tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
        var scene = tree.Nodes.Add("Scene");
        scene.Nodes.Add("Rotating color cube (Mesh)");
        scene.Nodes.Add("uModel rotation (Material)");
        scene.Nodes.Add("Blue Background").Tag = Color.FromArgb(31, 52, 79);
        scene.Nodes.Add("Green Background").Tag = Color.FromArgb(26, 78, 58);
        scene.Nodes.Add("Gray Background").Tag = Color.FromArgb(55, 55, 55);
        scene.Expand();
        tree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is Color c)
            {
                _glView.ClearColor = c;
                _status.Text = $"Selected: {e.Node.Text}";
            }
        };

        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, Panel1MinSize = Px(180) };
        split.Panel1.Controls.Add(tree);
        split.Panel2.Controls.Add(_glView);
        var status = new StatusStrip();
        status.Items.AddRange([_status, _fps]);
        Controls.AddRange([split, status, menu]);
        MainMenuStrip = menu;

        Shown += (_, _) => split.SplitterDistance = (int)Math.Round(split.ClientSize.Width * .30f);
        _glView.StatusChanged += (_, s) => _status.Text = s;
        _timer.Tick += (_, _) => _fps.Text = $"Render: {_glView.FramesPerSecond:F1} FPS";
        _timer.Start();
        FormClosing += (_, _) => _timer.Stop();
    }

    private Size Scale(Size size) => new(Px(size.Width), Px(size.Height));
    private int Px(int value) => (int)Math.Round(value * DeviceDpi / (float)DesignDpi);
}
