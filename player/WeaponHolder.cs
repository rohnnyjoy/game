using Godot;

public partial class WeaponHolder : Node3D
{
  [Export] public float AccelerationMultiplier = 0.05f;
  [Export] public float SpringStiffness = 20.0f;
  [Export] public float Damping = 12.0f;
  [Export] public float MaxOffset = 0.1f;

  [Export] public NodePath PlayerPath;
  [Export] public NodePath PivotPath;

  private CharacterBody3D _player;
  private Node3D _pivot;

  private Vector3 _basePosition;
  private Vector3 _inertiaOffset = Vector3.Zero;
  private Vector3 _inertiaVelocity = Vector3.Zero;
  private Vector3 _prevPlayerVelocity = Vector3.Zero;
  private Vector3 _prevPivotRotation = Vector3.Zero;

  public override void _Ready()
  {
    _player = GetNode<CharacterBody3D>(PlayerPath);
    _pivot = GetNode<Node3D>(PivotPath);
    _basePosition = Position;
    _prevPlayerVelocity = _pivot.GlobalTransform.Basis.Inverse() * _player.Velocity;
    _prevPivotRotation = _pivot.Rotation;
  }

  public override void _Process(double delta)
  {
    float dt = (float)delta;
    Vector3 currentPlayerVelocity = _pivot.GlobalTransform.Basis.Inverse() * _player.Velocity;
    Vector3 playerAcceleration = (currentPlayerVelocity - _prevPlayerVelocity) / dt;
    _prevPlayerVelocity = currentPlayerVelocity;

    Vector3 inertiaForce = -playerAcceleration * AccelerationMultiplier;
    Vector3 inertiaAccel = inertiaForce - (SpringStiffness * _inertiaOffset) - (Damping * _inertiaVelocity);
    _inertiaVelocity += inertiaAccel * dt;
    _inertiaOffset += _inertiaVelocity * dt;

    _inertiaOffset.X = Mathf.Clamp(_inertiaOffset.X, -MaxOffset, MaxOffset);
    _inertiaOffset.Y = Mathf.Clamp(_inertiaOffset.Y, -MaxOffset, MaxOffset);
    _inertiaOffset.Z = Mathf.Clamp(_inertiaOffset.Z, -MaxOffset, MaxOffset);

    Position = _basePosition + _inertiaOffset;
  }
}
