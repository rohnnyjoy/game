using Godot;
using System;

public partial class Card3D : RigidBody3D, IInteractable
{
  [Export]
  public CardCore CardCore { get; set; }

  [Export]
  public float ScaleFactor { get; set; } = 0.01f;

  // Custom physics properties.
  [Export]
  public float CardMass { get; set; } = 1.0f;
  [Export]
  public float CardFriction { get; set; } = 1.0f;
  [Export]
  public float CardBounce { get; set; } = 0.2f;

  // Hover parameters.
  [Export]
  public float HoverDamping { get; set; } = 5.0f;
  [Export]
  public float HoverHeight { get; set; } = 1.75f;
  [Export]
  public float HoverForce { get; set; } = 20.0f;
  [Export]
  public float HoverActivationDistance { get; set; } = 2.0f;

  // Oscillation parameters.
  [Export]
  public float OscillationAmplitude { get; set; } = 0.2f;
  [Export]
  public float OscillationFrequency { get; set; } = 0.2f;

  // Billboard speed.
  [Export]
  public float BillboardSpeed { get; set; } = 10.0f;

  // Outline properties.
  [Export]
  public Color OutlineColor { get; set; } = Colors.White;
  [Export]
  public float OutlineWidth { get; set; } = 2f;  // How much the outline expands.

  // Preload the outline shader from an external file.
  private Shader outlineShader = GD.Load<Shader>("res://shaders/outline_shader.gdshader");

  private RayCast3D _raycast;

  public override void _Ready()
  {
    // Initialize CardCore if null.
    if (CardCore == null)
    {
      CardCore = new CardCore();
    }

    // Set default position if at origin.
    if (GlobalTransform.Origin == Vector3.Zero)
    {
      GlobalTransform = new Transform3D(GlobalTransform.Basis, new Vector3(0, HoverHeight, 0));
    }

    // Set physics properties.
    Mass = CardMass;
    var material = new PhysicsMaterial();
    material.Friction = CardFriction;
    material.Bounce = CardBounce;
    PhysicsMaterialOverride = material;

    // Enable custom physics integration.
    CustomIntegrator = true;

    // Disable collisions.
    CollisionLayer = 1 << 3;  // Example: Assign to layer 3.
    CollisionMask = 1 << 3;   // Allow detection on layer 3.

    // Create RayCast3D for ground detection.
    _raycast = new RayCast3D();
    _raycast.TargetPosition = Vector3.Down * 100; // Cast far downward.
    _raycast.AddException(this);
    AddChild(_raycast);
    _raycast.Enabled = true;

    SetupVisuals();

    // Optionally add this to an interactable group.
    AddToGroup("interactable");
  }

  public override void _IntegrateForces(PhysicsDirectBodyState3D state)
  {
    var transform = state.Transform;

    // Allow up to 15° tilt (in radians).
    float maxTilt = Mathf.DegToRad(15);

    // Get the camera.
    Camera3D camera = GetViewport().GetCamera3D();
    if (camera != null)
    {
      // Compute vector from card to camera.
      Vector3 toCamera = camera.GlobalTransform.Origin - transform.Origin;

      // Use -toCamera so the card's front faces the camera (adjust if needed).
      Basis targetBasis = Basis.LookingAt(-toCamera, Vector3.Up);

      // Convert the target rotation to Euler angles.
      Vector3 targetEuler = targetBasis.GetEuler();

      // Clamp the pitch (rotation around the X axis) so that the tilt doesn't exceed maxTilt.
      targetEuler.X = Mathf.Clamp(targetEuler.X, -maxTilt, maxTilt);

      // Reset roll (Z axis) to zero so that the card remains level.
      targetEuler.Z = 0;

      // Convert Euler angles to a quaternion.
      Quaternion targetQuat = Quaternion.FromEuler(targetEuler);

      // Build the target basis from the quaternion.
      targetBasis = new Basis(targetQuat);

      // Smoothly interpolate the current rotation toward the target.
      transform.Basis = transform.Basis.Slerp(targetBasis, BillboardSpeed * state.Step);

      // Zero out angular velocity.
      state.AngularVelocity = Vector3.Zero;
    }




    // Apply gravity manually.
    float gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
    state.LinearVelocity += Vector3.Down * gravity * state.Step;

    // Hover logic with oscillation.
    if (_raycast.IsColliding())
    {
      Vector3 collisionPoint = _raycast.GetCollisionPoint();
      float groundDistance = transform.Origin.Y - collisionPoint.Y;

      float timeInSec = Time.GetTicksMsec() / 1000.0f;
      float oscillationOffset = OscillationAmplitude * Mathf.Sin(2.0f * Mathf.Pi * OscillationFrequency * timeInSec);
      float desiredHoverHeight = HoverHeight + oscillationOffset;

      if (groundDistance < HoverActivationDistance)
      {
        float error = desiredHoverHeight - groundDistance;
        float dampingForce = state.LinearVelocity.Y * HoverDamping;
        float springForce = error * HoverForce;
        float netForce = springForce - dampingForce;
        state.LinearVelocity = new Vector3(0, state.LinearVelocity.Y + netForce * state.Step, 0);
      }
    }

    state.Transform = transform;
  }

  public void SetupVisuals()
  {
    // Remove any existing MeshInstance3D or CollisionShape3D children.
    foreach (Node child in GetChildren())
    {
      if (child is MeshInstance3D || child is CollisionShape3D)
      {
        child.QueueFree();
      }
    }

    // Create a MeshInstance3D for the main card visuals.
    MeshInstance3D meshInstance = new MeshInstance3D();
    Vector2 meshSize = CardCore.CardSize * ScaleFactor;

    // Create and assign a QuadMesh.
    QuadMesh quadMesh = new QuadMesh();
    quadMesh.Size = meshSize;
    meshInstance.Mesh = quadMesh;

    // Set up the material for the main card.
    StandardMaterial3D material = new StandardMaterial3D();
    material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
    material.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
    material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
    if (CardCore.CardTexture != null)
    {
      material.AlbedoTexture = CardCore.CardTexture;
    }
    else
    {
      material.AlbedoColor = CardCore.CardColor;
    }
    meshInstance.MaterialOverride = material;
    meshInstance.Transform = new Transform3D(meshInstance.Transform.Basis, Vector3.Zero);
    AddChild(meshInstance);

    // --- Create an outline mesh instance using the external shader ---
    MeshInstance3D outlineMeshInstance = new MeshInstance3D();
    // Duplicate the main mesh to maintain consistent geometry.
    outlineMeshInstance.Mesh = quadMesh.Duplicate() as QuadMesh;

    // Create and assign a ShaderMaterial using the preloaded outline shader.
    ShaderMaterial outlineShaderMaterial = new ShaderMaterial();
    outlineShaderMaterial.Shader = outlineShader;
    outlineShaderMaterial.SetShaderParameter("outline_width", OutlineWidth);
    outlineShaderMaterial.SetShaderParameter("outline_color", OutlineColor);
    outlineMeshInstance.MaterialOverride = outlineShaderMaterial;

    // Slight Z offset to avoid z-fighting.
    outlineMeshInstance.Transform = new Transform3D(outlineMeshInstance.Transform.Basis, new Vector3(0, 0, -0.001f));
    AddChild(outlineMeshInstance);

    // Create a CollisionShape3D.
    CollisionShape3D collision = new CollisionShape3D();
    BoxShape3D shape = new BoxShape3D();
    shape.Size = new Vector3(meshSize.X * 0.5f, meshSize.X * 0.5f, 0.1f);
    collision.Shape = shape;
    AddChild(collision);
  }

  // Implementation of the IInteractable interface.
  public virtual void OnInteract()
  {
    GD.Print("Card picked up!");
    QueueFree();
  }

  public string GetInteractionText()
  {
    return "[E] Pick up card";
  }
}
