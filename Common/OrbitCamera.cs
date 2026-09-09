using System;
using OpenTK.Mathematics;

namespace LearnOpenTK.Common;

public sealed class OrbitCamera
{
    public Vector3 Target { get; private set; } = Vector3.Zero;
    public float Distance { get; private set; } = 3f;
    public float Yaw { get; private set; } = -90f;
    public float Pitch { get; private set; } = -15f;
    public float AspectRatio { private get; set; } = 16f / 9f;
    public Vector3 Position => Target - GetForward() * Distance;
    public Matrix4 GetViewMatrix() => Matrix4.LookAt(Position, Target, Vector3.UnitY);
    public Matrix4 GetProjectionMatrix() => Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45f), AspectRatio, .01f, 100f);
    public void Rotate(float yawDelta, float pitchDelta) { Yaw += yawDelta; Pitch = MathHelper.Clamp(Pitch + pitchDelta, -89f, 89f); }
    public void Pan(float rightAmount, float upAmount) { var forward = GetForward(); var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY)); Target += right * rightAmount + Vector3.UnitY * upAmount; }
    public void Zoom(float amount) => Distance = MathHelper.Clamp(Distance - amount, .5f, 30f);
    private Vector3 GetForward() { var yaw = MathHelper.DegreesToRadians(Yaw); var pitch = MathHelper.DegreesToRadians(Pitch); return Vector3.Normalize(new(MathF.Cos(pitch) * MathF.Cos(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Sin(yaw))); }
}
