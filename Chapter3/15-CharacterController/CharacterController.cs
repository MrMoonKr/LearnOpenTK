using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenTK.Mathematics;

namespace LearnOpenTK.CharacterController;

public enum CharacterMotionState { Idle, Run, Attack, Jump, Fall }

/// <summary>
/// Movement/state machine for the one currently-selected hero, styled after Unity's
/// <c>CharacterController</c>: camera-relative WASD/arrow-key movement (W always runs toward where the
/// camera is currently looking, not a fixed world axis), gravity/jump/ground-snap against the flat
/// ground plane (Y = 0; there is no other collision geometry in this example, so this stands in for
/// Unity's swept-capsule <c>Move()</c> collision response), and an F-key-down-edge attack that locks
/// movement for the attack clip's own duration. Space is Jump rather than Attack so it matches the key
/// most character-controller demos use.
/// </summary>
public sealed class CharacterController
{
    private readonly float _moveSpeed;
    private readonly float _jumpSpeed;
    private readonly float _gravity;
    private bool _attackKeyWasDown;
    private bool _jumpKeyWasDown;
    private float _attackElapsedSeconds;
    private float _verticalVelocity;

    public CharacterController(float moveSpeed, float jumpSpeed, float gravity)
    {
        _moveSpeed = moveSpeed;
        _jumpSpeed = jumpSpeed;
        _gravity = gravity;
    }

    public Vector3 Position { get; private set; }
    public float YawDegrees { get; private set; }
    public CharacterMotionState State { get; private set; } = CharacterMotionState.Idle;
    public bool IsGrounded { get; private set; } = true;

    /// <summary>Places the character at a spawn point (e.g. when a new hero is selected) and clears any in-progress attack/jump.</summary>
    public void Reset(Vector3 position, float yawDegrees)
    {
        Position = position;
        YawDegrees = yawDegrees;
        State = CharacterMotionState.Idle;
        _attackElapsedSeconds = 0f;
        _attackKeyWasDown = false;
        _jumpKeyWasDown = false;
        _verticalVelocity = 0f;
        IsGrounded = true;
    }

    /// <summary>
    /// Advances one frame. <paramref name="cameraYawDegrees"/> is <see cref="OrbitCamera.Yaw"/> - W/S
    /// move along the camera's own forward/back (projected onto the ground plane) and A/D strafe
    /// perpendicular to it, so orbiting the camera turns "forward" with it. <paramref
    /// name="attackClipDurationSeconds"/> is the active hero's primary attack clip length in seconds;
    /// F is a no-op if it is 0 (the hero has no classified attack clip). <see cref="AnimationClip"/>/
    /// <see cref="Animator"/> always loop and have no "finished" signal, so the attack lock is timed
    /// against this controller's own elapsed-time field instead. Jump/attack cannot be triggered
    /// while the other is active; horizontal air control while jumping/falling is allowed.
    /// </summary>
    public void Update(float deltaSeconds, IReadOnlySet<Keys> heldKeys, bool attackKeyDown, bool jumpKeyDown,
        float cameraYawDegrees, float attackClipDurationSeconds, float groundHalfSize)
    {
        var attackKeyPressedThisFrame = attackKeyDown && !_attackKeyWasDown;
        _attackKeyWasDown = attackKeyDown;
        var jumpKeyPressedThisFrame = jumpKeyDown && !_jumpKeyWasDown;
        _jumpKeyWasDown = jumpKeyDown;

        var attacking = State == CharacterMotionState.Attack;
        if (attacking)
        {
            _attackElapsedSeconds += deltaSeconds;
            attacking = _attackElapsedSeconds < attackClipDurationSeconds;
        }

        if (!attacking && IsGrounded && attackKeyPressedThisFrame && attackClipDurationSeconds > 0f)
        {
            attacking = true;
            _attackElapsedSeconds = 0f;
        }

        if (!attacking && IsGrounded && jumpKeyPressedThisFrame)
        {
            _verticalVelocity = _jumpSpeed;
        }

        // Horizontal movement is locked while attacking, but allowed (with air control) while jumping/falling.
        var moving = false;
        if (!attacking)
        {
            var yawRad = MathHelper.DegreesToRadians(cameraYawDegrees);
            var cameraForward = new Vector3(MathF.Cos(yawRad), 0f, MathF.Sin(yawRad));
            var cameraRight = new Vector3(-MathF.Sin(yawRad), 0f, MathF.Cos(yawRad));

            var move = Vector3.Zero;
            if (heldKeys.Contains(Keys.W) || heldKeys.Contains(Keys.Up)) move += cameraForward;
            if (heldKeys.Contains(Keys.S) || heldKeys.Contains(Keys.Down)) move -= cameraForward;
            if (heldKeys.Contains(Keys.D) || heldKeys.Contains(Keys.Right)) move += cameraRight;
            if (heldKeys.Contains(Keys.A) || heldKeys.Contains(Keys.Left)) move -= cameraRight;

            if (move.LengthSquared > 0f)
            {
                move = Vector3.Normalize(move);
                var moved = Position + move * _moveSpeed * deltaSeconds;
                Position = new Vector3(
                    MathHelper.Clamp(moved.X, -groundHalfSize, groundHalfSize),
                    Position.Y,
                    MathHelper.Clamp(moved.Z, -groundHalfSize, groundHalfSize));

                // The hero model's neutral (unrotated) facing after GLView's SourceToWorldUp conversion is
                // world +X, not +Z, so yaw is measured from +X rather than the more common atan2(x, z).
                YawDegrees = MathHelper.RadiansToDegrees(MathF.Atan2(-move.Z, move.X));
                moving = true;
            }
        }

        // Gravity/ground-snap: the only collision surface in this example is the flat plane at Y = 0,
        // so this stands in for Unity CharacterController's swept-capsule Move() against real geometry.
        _verticalVelocity -= _gravity * deltaSeconds;
        var newY = Position.Y + _verticalVelocity * deltaSeconds;
        if (newY <= 0f)
        {
            newY = 0f;
            _verticalVelocity = 0f;
            IsGrounded = true;
        }
        else
        {
            IsGrounded = false;
        }
        Position = new Vector3(Position.X, newY, Position.Z);

        State = attacking ? CharacterMotionState.Attack
            : !IsGrounded ? (_verticalVelocity > 0f ? CharacterMotionState.Jump : CharacterMotionState.Fall)
            : moving ? CharacterMotionState.Run
            : CharacterMotionState.Idle;
    }
}
