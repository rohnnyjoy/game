using Godot;
using Godot.Collections;

public partial class CameraPivot : Node3D
{
  [Export] public NodePath PlayerPath;
  [Export] public NodePath CameraRigPath;

  [Export] public float Sensitivity = 0.005f;
  [Export(PropertyHint.Range, "-1.5708,0")] public float MinPitch = -Mathf.DegToRad(75f);
  [Export(PropertyHint.Range, "0,1.5708")] public float MaxPitch = Mathf.DegToRad(75f);
  [Export(PropertyHint.Range, "-89,75")] public float InitialPitchDegrees = -20f;

  [Export] public Vector3 CameraOffset = new Vector3(0.6f, 0.8f, 4.0f);
  [Export] public Vector3 FocusOffset = new Vector3(0f, 0.8f, 0f);
  [Export(PropertyHint.Layers3DPhysics)] public uint CameraCollisionMask = 0xFFFFFFFF;
  [Export] public float CollisionMargin = 0.3f;
  [Export] public float MinDistance = 2.0f;
  [Export] public float MaxDistance = 6.0f;
  [Export] public float ZoomSpeed = 0.5f;
  [Export] public bool InvertOrbitZ = false; // If true, places camera on -Z instead of +Z

  // Player the body yaw is applied to (movement remains deterministic).
  private Player player;
  private Node3D cameraRig;

  // Target (authoritative) view angles in radians.
  private float pitch;
  private float yaw;

  private float targetDistance;
  private float baseShoulderOffset;
  private Input.MouseModeEnum _lastMouseMode;
  private bool _skipNextMouseMotion;
  private Vector3 _finalLocalCameraOffset; // computed in physics, applied in _Process

  public override void _Ready()
  {
    player = GetNodeOrNull<Player>(PlayerPath);
    cameraRig = GetNodeOrNull<Node3D>(CameraRigPath);

    // Initialize from current transforms to avoid a snap.
    yaw = NormalizeAngle(player != null ? player.Rotation.Y : 0f);

    if (MinDistance > MaxDistance)
    {
      float temp = MinDistance;
      MinDistance = MaxDistance;
      MaxDistance = temp;
    }

    baseShoulderOffset = CameraOffset.X;
    targetDistance = Mathf.Clamp(CameraOffset.Z, MinDistance, MaxDistance);
    if (targetDistance <= 0f)
      targetDistance = Mathf.Max(MinDistance, 0.1f);

    pitch = Mathf.Clamp(Mathf.DegToRad(InitialPitchDegrees), MinPitch, MaxPitch);
    SyncPlayerYaw();
    ApplyPivotRotation();

    // Initialize cached camera offset so first _Process frame has a sane position.
    _finalLocalCameraOffset = new Vector3(
      baseShoulderOffset,
      CameraOffset.Y,
      (InvertOrbitZ ? -1f : 1f) * targetDistance
    );

    // We read mouse in _Input for lowest latency, update camera visuals in _Process,
    // and keep the physics body yaw in sync via _PhysicsProcess.
    SetProcessInput(true);
    SetProcess(true);
    SetPhysicsProcess(true);

    _lastMouseMode = Input.MouseMode;
    _skipNextMouseMotion = false;

    // IMPORTANT: In the Inspector, set Physics Interpolation = Off
    // for this node (CameraPivot) and for any child that you animate
    // in _Process (e.g., CameraShake). This prevents interpolation
    // from overriding per-frame changes we make here.
  }

  public override void _Input(InputEvent @event)
  {
    switch (@event)
    {
      case InputEventMouseMotion mouseMotion:
        if (ShouldApplyLookInput())
        {
          // Discard the first motion after recapturing the mouse to avoid a large warp delta.
          if (_skipNextMouseMotion)
          {
            _skipNextMouseMotion = false;
            break;
          }
          ApplyLookDelta(mouseMotion.Relative);
        }
        break;
      case InputEventMouseButton mouseButton when mouseButton.Pressed:
        if (mouseButton.ButtonIndex == MouseButton.WheelUp)
        {
          targetDistance = Mathf.Max(MinDistance, targetDistance - ZoomSpeed);
        }
        else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
        {
          targetDistance = Mathf.Min(MaxDistance, targetDistance + ZoomSpeed);
        }
        break;
    }
  }

  public override void _Process(double delta)
  {
    // Detect transition to Captured mode and ignore the first mouse delta after recapture
    var currentMode = Input.MouseMode;
    if (_lastMouseMode != currentMode)
    {
      if (currentMode == Input.MouseModeEnum.Captured)
        _skipNextMouseMotion = true;
      _lastMouseMode = currentMode;
    }

    ApplyPivotRotation();
    // Apply last physics-computed camera offset to the rig.
    if (cameraRig != null)
      cameraRig.Position = _finalLocalCameraOffset;
  }

  public override void _PhysicsProcess(double delta)
  {
    if (player == null)
      return;

    // Once per physics tick, snap the physics body's yaw to the latest target yaw.
    // This keeps movement/collisions deterministic and aligned with the view direction.
    SyncPlayerYaw();

    // Compute camera collision in physics and cache the local offset.
    ComputeCameraPositionPhysics();
  }

  private bool ShouldApplyLookInput()
  {
    if (GlobalEvents.Instance != null && GlobalEvents.Instance.MenuOpen)
      return false;
    return Input.MouseMode == Input.MouseModeEnum.Captured;
  }

  private void ApplyLookDelta(Vector2 delta)
  {
    if (delta == Vector2.Zero)
      return;

    yaw += -delta.X * Sensitivity;
    yaw = NormalizeAngle(yaw);
    pitch = Mathf.Clamp(pitch - delta.Y * Sensitivity, MinPitch, MaxPitch);
    SyncPlayerYaw();
    ApplyPivotRotation();
  }

  private void SyncPlayerYaw()
  {
    if (player == null)
      return;

    Vector3 pr = player.Rotation;
    float targetYaw = NormalizeAngle(yaw);
    yaw = targetYaw;
    if (!Mathf.IsEqualApprox(pr.Y, targetYaw))
    {
      pr.Y = targetYaw;
      player.Rotation = pr;
    }
  }

  private void ApplyPivotRotation()
  {
    // Keep a small safety offset in case some other system temporarily overrides the body yaw.
    float bodyYaw = NormalizeAngle(player != null ? player.Rotation.Y : 0f);
    float yawOffset = AngleDelta(bodyYaw, yaw); // normalized shortest delta in [-π, π]

    // Apply pitch and the small visual yaw offset locally on the pivot.
    // The parent (player) supplies the bulk yaw; we only add the offset here.
    Rotation = new Vector3(pitch, yawOffset, 0f);
  }

  private void ComputeCameraPositionPhysics()
  {
    if (cameraRig == null)
      return;

    float clampedDistance = Mathf.Clamp(targetDistance, MinDistance, MaxDistance);
    float signedDistance = (InvertOrbitZ ? -1f : 1f) * clampedDistance;
    Vector3 desiredLocal = new Vector3(baseShoulderOffset, CameraOffset.Y, signedDistance);

    Transform3D globalTransform = GlobalTransform;
    Vector3 focusWorld = globalTransform * FocusOffset;
    Vector3 desiredWorld = globalTransform * desiredLocal;

    Vector3 offsetDir = desiredWorld - focusWorld;
    float offsetLength = offsetDir.Length();
    Vector3 finalWorld = desiredWorld;

    if (offsetLength > Mathf.Epsilon)
    {
      offsetDir /= offsetLength;

      var spaceState = GetWorld3D()?.DirectSpaceState;
      if (spaceState != null)
      {
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(focusWorld, desiredWorld);
        query.CollisionMask = CameraCollisionMask;
        query.CollideWithAreas = false;
        Array<Rid> exclude = new Array<Rid>();
        if (player != null)
          exclude.Add(player.GetRid());
        query.Exclude = exclude;

        Godot.Collections.Dictionary hit = spaceState.IntersectRay(query);
        if (hit.Count > 0 && hit.ContainsKey("position"))
        {
          Vector3 hitPos = (Vector3)hit["position"];
          float safeDistance = Mathf.Max(0f, (hitPos - focusWorld).Length() - CollisionMargin);
          finalWorld = focusWorld + offsetDir * Mathf.Min(safeDistance, offsetLength);
        }
      }
    }

    Transform3D inv = globalTransform.AffineInverse();
    _finalLocalCameraOffset = inv * finalWorld;
  }

  // Smallest signed angle from "from" to "to", normalized to [-π, π].
  // Prevents wrap-around jitters when computing visual yaw offsets.
  private static float NormalizeAngle(float angle)
  {
    return Mathf.PosMod(angle + Mathf.Pi, Mathf.Tau) - Mathf.Pi;
  }

  private static float AngleDelta(float from, float to)
  {
    float diff = to - from;
    while (diff > Mathf.Pi) diff -= Mathf.Tau;
    while (diff < -Mathf.Pi) diff += Mathf.Tau;
    return diff;
  }
}
