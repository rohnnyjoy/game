using Godot;
using Godot.Collections;
using System;
using System.Linq;

public partial class WeaponModuleCard2D : Card2D
{
  [Export]
  public WeaponModule Module { get; set; }

  public override void _Ready()
  {
    // Initialize CardCore using the module's properties.
    CardCore = new CardCore();
    CardCore.CardTexture = Module.CardTexture;
    CardCore.CardDescription = Module.ModuleDescription;
    GD.Print(Module.CardTexture);
    base._Ready();
  }

  protected override void OnDroppedOutsideStacks()
  {
    ConvertTo3D();
    var parent = GetParent();
    if (parent is InventoryStack)
    {
      var newModules = new Array<WeaponModule>(Player.Instance.Inventory.WeaponModules);
      newModules.Remove(Module);
      Player.Instance.Inventory.WeaponModules = newModules;
    }
    else if (parent is PrimaryWeaponStack)
    {
      var newModules = new Array<WeaponModule>(Player.Instance.Inventory.PrimaryWeapon.Modules);
      newModules.Remove(Module);
      var newPrimaryWeapon = Player.Instance.Inventory.PrimaryWeapon;
      newPrimaryWeapon.Modules = newModules;
      Player.Instance.Inventory.PrimaryWeapon = newPrimaryWeapon;
    }
    else
    {
      GD.PrintErr("Dropped outside of stacks, but parent is not a stack, it is: " + parent);
    }
  }

  private void ConvertTo3D()
  {
    // Convert the 2D card to its 3D counterpart.
    WeaponModuleCard3D card3d = new WeaponModuleCard3D();
    card3d.Initialize(Module);
    card3d.CardCore = CardCore;

    Camera3D camera = GetViewport().GetCamera3D();
    if (camera != null)
    {
      float dropDistance = 2.0f;
      Vector3 forward = -camera.GlobalTransform.Basis.Z.Normalized();
      Vector3 dropPosition = camera.GlobalTransform.Origin + forward * dropDistance;

      var spaceState = camera.GetWorld3D().DirectSpaceState;
      PhysicsRayQueryParameters3D query = new PhysicsRayQueryParameters3D
      {
        From = camera.GlobalTransform.Origin,
        To = dropPosition,
        Exclude = new Godot.Collections.Array<Rid>(),
        CollisionMask = 0xFFFFFFFF
      };
      var collision = spaceState.IntersectRay(query);
      if (collision.Count() > 0)
      {
        float safeOffset = 0.5f;
        if (collision.ContainsKey("position") && collision.ContainsKey("normal"))
        {
          Vector3 collisionPos = (Vector3)collision["position"];
          Vector3 collisionNormal = (Vector3)collision["normal"];
          dropPosition = collisionPos + collisionNormal * safeOffset;
        }
      }

      Transform3D transform = card3d.GlobalTransform;
      transform.Origin = dropPosition;
      card3d.GlobalTransform = transform;

      Node ground = GetNodeOrNull("/root/World/Ground");
      if (ground != null)
        ground.AddChild(card3d);
      else
        GetTree().Root.AddChild(card3d);

      Vector3 toCamera = camera.GlobalTransform.Origin - dropPosition;
      if (toCamera.Length() > 0.001f)
      {
        Vector3 upDir = Vector3.Up;
        if (Math.Abs(toCamera.Normalized().Dot(upDir)) > 0.99f)
          upDir = Vector3.Forward;
        transform = card3d.GlobalTransform;
        transform.Basis = Basis.LookingAt(-toCamera, upDir);
        card3d.GlobalTransform = transform;
      }

      float tossStrength = 3.0f;
      float tossUpStrength = 1.0f;
      card3d.LinearVelocity = forward * tossStrength + Vector3.Up * tossUpStrength;
    }
    else
    {
      Transform3D transform = card3d.GlobalTransform;
      transform.Origin = new Vector3(GlobalPosition.X, GlobalPosition.Y, 0);
      card3d.GlobalTransform = transform;
      Node ground = GetNodeOrNull("/root/World/Ground");
      if (ground != null)
        ground.AddChild(card3d);
      else
        GetTree().Root.AddChild(card3d);
    }

    QueueFree();
  }
}
