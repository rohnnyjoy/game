// Card3D.cs
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
    CollisionLayer = 0;
    CollisionMask = 0;

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

    // Smooth billboarding.
    Camera3D camera = GetViewport().GetCamera3D();
    if (camera != null)
    {
      Vector3 toCamera = camera.GlobalTransform.Origin - transform.Origin;
      if (toCamera.Length() > 0.001f)
      {
        Vector3 upDir = Vector3.Up;
        if (Math.Abs(toCamera.Normalized().Dot(upDir)) > 0.99f)
        {
          upDir = Vector3.Forward;
        }
        Basis targetBasis = Basis.LookingAt(-toCamera, upDir);
        transform.Basis = transform.Basis.Slerp(targetBasis, BillboardSpeed * state.Step);
      }
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

    // Create a MeshInstance3D for visuals.
    MeshInstance3D meshInstance = new MeshInstance3D();
    Vector2 meshSize = CardCore.CardSize * ScaleFactor;

    // Create and assign a QuadMesh.
    QuadMesh quadMesh = new QuadMesh();
    quadMesh.Size = meshSize;
    meshInstance.Mesh = quadMesh;

    // Set up a material.
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

    // Create a CollisionShape3D.
    CollisionShape3D collision = new CollisionShape3D();
    BoxShape3D shape = new BoxShape3D();
    shape.Size = new Vector3(meshSize.X * 0.5f, meshSize.X * 0.5f, 0.1f);
    collision.Shape = shape;
    AddChild(collision);
  }

  // Implementation of the IInteractable interface.
  public void OnInteract()
  {
    GD.Print("Card picked up!");
    // Example: Remove from scene or add to an inventory.
    QueueFree();
  }
}
