using Godot;

public partial class WeaponHolder : Node3D
{
  // Controls how strongly the gun reacts to changes in player velocity.
  [Export] public float AccelerationMultiplier = 0.05f;

  // Spring parameters for the inertia simulation.
  [Export] public float SpringStiffness = 20.0f; // Higher = stronger pull to center.
  [Export] public float Damping = 12.0f;         // Higher = less overshoot (more damped).

  // Limits to avoid extreme offsets.
  [Export] public float MaxOffset = 0.1f;

  private CharacterBody3D _player;
  private Node3D _pivot; // The camera pivot (parent)

  // Base position of the weapon (its rest position).
  private Vector3 _basePosition;

  // Inertia simulation state.
  private Vector3 _inertiaOffset = Vector3.Zero;
  private Vector3 _inertiaVelocity = Vector3.Zero;
  private Vector3 _prevPlayerVelocity = Vector3.Zero;

  // For look sway.
  private Vector3 _prevPivotRotation = Vector3.Zero;

  public override void _Ready()
  {
    // Assuming the player's CharacterBody3D is the grandparent.
    _player = GetParent().GetParent<CharacterBody3D>();
    _pivot = (Node3D)GetParent();

    _basePosition = Position;
    // Convert player's velocity into the pivot's local space.
    _prevPlayerVelocity = _pivot.GlobalTransform.Basis.Inverse() * _player.Velocity;
    _prevPivotRotation = _pivot.Rotation;
  }

  public override void _Process(double delta)
  {
    float dt = (float)delta;

    // === Inertia from Acceleration ===
    // Convert player's velocity to the pivot's local space.
    Vector3 currentPlayerVelocity = _pivot.GlobalTransform.Basis.Inverse() * _player.Velocity;
    Vector3 playerAcceleration = (currentPlayerVelocity - _prevPlayerVelocity) / dt;
    _prevPlayerVelocity = currentPlayerVelocity;

    // Compute an inertia "force" that is opposite to the player's acceleration.
    Vector3 inertiaForce = -playerAcceleration * AccelerationMultiplier;

    // Update the inertia simulation using a mass-spring-damper model:
    //   acceleration = force - (spring * offset) - (damping * velocity)
    Vector3 inertiaAccel = inertiaForce - (SpringStiffness * _inertiaOffset) - (Damping * _inertiaVelocity);
    _inertiaVelocity += inertiaAccel * dt;
    _inertiaOffset += _inertiaVelocity * dt;

    // Clamp the inertia offset to avoid excessive movement.
    _inertiaOffset.X = Mathf.Clamp(_inertiaOffset.X, -MaxOffset, MaxOffset);
    _inertiaOffset.Y = Mathf.Clamp(_inertiaOffset.Y, -MaxOffset, MaxOffset);
    _inertiaOffset.Z = Mathf.Clamp(_inertiaOffset.Z, -MaxOffset, MaxOffset);

    // === Combine Offsets and Apply ===
    // Both _inertiaOffset and lookSwayOffset are now in the pivot's local space.
    Vector3 totalOffset = _inertiaOffset;
    Position = _basePosition + totalOffset;
  }
}
