using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LearnOpenTK.Common;
using LearnOpenTK.Common.Geometry;
using LearnOpenTK.Common.Rendering;
using LearnOpenTK.Common.Scene;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.WinForms;
namespace LearnOpenTK.SceneGraph;

public sealed class GLView : GLControl
{
    private readonly OrbitCamera _camera = new(); private readonly Stopwatch _clock = Stopwatch.StartNew(); private readonly List<(SceneNode node, List<SceneNode> children, Vector3 axis, float speed)> _groups = []; private Mesh? _mesh; private Shader? _shader; private Material? _material; private bool _loaded, _orbit, _pan; private Point _last; private int _swap, _frames; private TimeSpan _sample;
    public GLView() { Dock = DockStyle.Fill; TabStop = true; Load += (_, _) => LoadScene(); Paint += (_, _) => RenderScene(); Resize += (_, _) => ResizeViewport(); MouseDown += HandleMouseDown; MouseUp += (_, _) => { _orbit = _pan = false; Cursor = Cursors.Default; }; MouseMove += HandleMouseMove; MouseWheel += (_, e) => { _camera.Zoom(e.Delta * .003f); Invalidate(); }; Disposed += (_, _) => UnloadScene(); }
    public event EventHandler<string>? StatusChanged; public double FramesPerSecond { get; private set; }
    public bool VSync { get => _swap != 0; set { _swap = value ? 1 : 0; if (_loaded && Context is not null) Context.SwapInterval = _swap; } }
    private void LoadScene() { MakeCurrent(); GL.ClearColor(.08f, .12f, .18f, 1); GL.Enable(EnableCap.DepthTest); if (Context is not null) Context.SwapInterval = _swap; _shader = new Shader(Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.vert"), Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.frag")); _material = new Material(_shader); _mesh = new Mesh(CubeGeometry.LitVertices, CubeGeometry.Indices, 9, 0, 3, 6); var r = new Random(20260909); for (var i = 0; i < 10; i++) { var group = new SceneNode($"Group {i:00}") { Position = new Vector3((i % 5 - 2) * 3, (i / 5 - .5f) * 3, 0) }; var children = new List<SceneNode>(); for (var j = 0; j < 10; j++) children.Add(new($"Cube {i * 10 + j:00}") { Position = new Vector3((float)r.NextDouble() * 2 - 1, (float)r.NextDouble() * 2 - 1, (float)r.NextDouble() * 2 - 1), Scale = Vector3.One * (.15f + (float)r.NextDouble() * .3f) }); _groups.Add((group, children, Vector3.Normalize(new((float)r.NextDouble(), (float)r.NextDouble(), (float)r.NextDouble())), .15f + (float)r.NextDouble() * .65f)); } _loaded = true; _sample = _clock.Elapsed; ResizeViewport(); StatusChanged?.Invoke(this, "100 lit cubes | Right: orbit | Middle: pan | Wheel: zoom"); }
    private void RenderScene() { if (!_loaded) return; MakeCurrent(); GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit); _material!.Bind(); _shader!.SetMatrix4("uView", _camera.GetViewMatrix()); _shader.SetMatrix4("uProjection", _camera.GetProjectionMatrix()); _shader.SetVector3("uCameraPosition", _camera.Position); _shader.SetVector3("uSunDirection", Vector3.Normalize(new(-1, -1, -.5f))); _shader.SetVector3("uFillPosition", new(-2, 0, 2)); _shader.SetVector3("uRimPosition", new(2, 1, -1)); var t = (float)_clock.Elapsed.TotalSeconds; foreach (var g in _groups) { g.node.Rotation = Matrix4.CreateFromAxisAngle(g.axis, t * g.speed); var parent = g.node.GetWorldMatrix(Matrix4.Identity); foreach (var c in g.children) { _shader.SetMatrix4("uModel", c.GetWorldMatrix(parent)); _mesh!.Draw(); } } SwapBuffers(); _frames++; var e = _clock.Elapsed - _sample; if (e.TotalSeconds >= 1) { FramesPerSecond = _frames / e.TotalSeconds; _frames = 0; _sample = _clock.Elapsed; } Invalidate(); }
    private void HandleMouseDown(object? _, MouseEventArgs e) { Focus(); _last = e.Location; _orbit = e.Button == MouseButtons.Right; _pan = e.Button == MouseButtons.Middle; if (_orbit || _pan) Cursor = Cursors.SizeAll; }
    private void HandleMouseMove(object? _, MouseEventArgs e) { var dx = e.X - _last.X; var dy = e.Y - _last.Y; if (_orbit) _camera.Rotate(dx * .35f, -dy * .35f); if (_pan) _camera.Pan(-dx * .01f, dy * .01f); _last = e.Location; }
    private void ResizeViewport() { if (!_loaded || ClientSize.Height == 0) return; MakeCurrent(); GL.Viewport(0, 0, ClientSize.Width, ClientSize.Height); _camera.AspectRatio = ClientSize.Width / (float)ClientSize.Height; }
    private void UnloadScene() { if (!_loaded) return; MakeCurrent(); _mesh?.Dispose(); if (_shader is not null) GL.DeleteProgram(_shader.Handle); _loaded = false; }
}
