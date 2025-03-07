using Godot;

public partial class WeaponHolder : Node3D
{
  [ExportGroup("Mouse Sway")]
  [Export] public float MouseSwayAmount = 0.002f;
  [Export] public float MouseSwaySpeed = 8.0f;

  [ExportGroup("Movement Sway")]
  [Export] public float MovementSwayAmount = 0.2f;
  [Export] public float MovementSwaySpeed = 6.0f;

  // These offsets are applied on top of the weapon's default rotation.
  private Vector3 _mouseRotationOffset = Vector3.Zero;
  private Vector3 _movementRotationOffset = Vector3.Zero;

  // Reference to the player to check velocity.
  private CharacterBody3D _player;

  public override void _Ready()
  {
    // Assuming the hierarchy is: Player -> Camera3D -> WeaponHolder
    _player = GetParent()?.GetParent() as CharacterBody3D;
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    if (@event is InputEventMouseMotion mouseMotion)
    {
      // Compute a quick rotation offset from mouse movement.
      _mouseRotationOffset = new Vector3(
          Mathf.DegToRad(-mouseMotion.Relative.Y * MouseSwayAmount),
          Mathf.DegToRad(-mouseMotion.Relative.X * MouseSwayAmount),
          0
      );
    }
  }

  public override void _Process(double delta)
  {
    // Calculate movement-based rotation offset.
    if (_player != null)
    {
      // Only consider horizontal velocity.
      Vector3 horizontalVel = _player.Velocity;
      horizontalVel.Y = 0;
      float speed = horizontalVel.Length();

      if (speed > 0.1f)
      {
        // Determine a tilt based on movement direction.
        // For example:
        // - If moving forward (negative Z), pitch slightly downward.
        // - If moving sideways, add a roll.
        Vector3 direction = horizontalVel.Normalized();
        float pitchOffset = -direction.Z * MovementSwayAmount;
        float rollOffset = direction.X * MovementSwayAmount;
        _movementRotationOffset = new Vector3(pitchOffset, 0, rollOffset);
      }
      else
      {
        // When no movement, smoothly return the movement offset to zero.
        _movementRotationOffset = _movementRotationOffset.Lerp(Vector3.Zero, (float)(MovementSwaySpeed * delta));
      }
    }

    // Combine mouse and movement offsets.
    Vector3 totalOffset = _mouseRotationOffset + _movementRotationOffset;
    // Smoothly interpolate current rotation toward the offset.
    Rotation = Rotation.Lerp(totalOffset, (float)(MouseSwaySpeed * delta));
  }
}
