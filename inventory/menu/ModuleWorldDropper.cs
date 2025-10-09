using Godot;
using Godot.Collections;

/// <summary>
/// Centralised helper for projecting weapon modules into the world as sprite-based pickups.
/// </summary>
public static class ModuleWorldDropper
{
  private const float RayLength = 12f;
  private const float SafeSurfaceOffset = 0.25f;
  private const float DefaultForwardDistance = 2f;
  private const float TossForwardStrength = 4f;
  private const float TossUpStrength = 2f;

  public static void Drop(Node context, WeaponModule module, CardCore cardCore)
  {
    if (context == null || module == null)
      return;

    CardCore resolvedCore = PrepareCardCore(cardCore, module);
    ModulePickup pickup = new ModulePickup();
    pickup.Initialize(module, (CardCore)resolvedCore.Duplicate(true));

    SceneTree tree = context.GetTree();
    if (tree == null)
      return;

    Vector3 dropNormal = Vector3.Up;
    Vector3 dropPosition = ComputeDropPosition(context, ref dropNormal, out Vector3 forward);

    Node parent = tree.CurrentScene ?? tree.Root;
    parent.AddChild(pickup);
    pickup.GlobalPosition = dropPosition;
    pickup.LinearVelocity = forward * TossForwardStrength + dropNormal * TossUpStrength;
    pickup.Sleeping = false;
  }

  private static CardCore PrepareCardCore(CardCore core, WeaponModule module)
  {
    if (core != null)
      return core;

    var result = new CardCore();
    result.CardTexture = module.CardTexture;
    result.CardDescription = module.ModuleDescription;
    result.CardSize = new Vector2(ModuleUiConfig.IconSize, ModuleUiConfig.IconSize);
    return result;
  }

  private static Vector3 ComputeDropPosition(Node context, ref Vector3 dropNormal, out Vector3 forward)
  {
    Camera3D camera = context.GetViewport()?.GetCamera3D();
    if (camera != null)
    {
      forward = -camera.GlobalTransform.Basis.Z;
      if (forward.LengthSquared() < 0.0001f)
        forward = Vector3.Forward;
      forward = forward.Normalized();

      World3D world = camera.GetWorld3D();
      if (world != null)
      {
        PhysicsDirectSpaceState3D spaceState = world.DirectSpaceState;
        Vector3 start = camera.GlobalTransform.Origin;
        Vector3 end = start + forward * RayLength;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(start, end);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.CollisionMask = uint.MaxValue;
        query.Exclude = new Array<Rid>();

        Dictionary result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
          if (result.TryGetValue("normal", out Variant normalVariant))
            dropNormal = ((Vector3)normalVariant).Normalized();
          Vector3 surface = result.TryGetValue("position", out Variant positionVariant)
            ? (Vector3)positionVariant
            : end;
          return surface + dropNormal * SafeSurfaceOffset;
        }

        return end;
      }

      return camera.GlobalTransform.Origin + forward * DefaultForwardDistance + Vector3.Up * 0.6f;
    }

    Player player = Player.Instance;
    if (player != null)
    {
      forward = -player.GlobalTransform.Basis.Z;
      if (forward.LengthSquared() < 0.0001f)
        forward = Vector3.Forward;
      forward = forward.Normalized();
      return player.GlobalTransform.Origin + forward * DefaultForwardDistance + Vector3.Up * 0.6f;
    }

    forward = Vector3.Forward;
    return new Vector3(0f, 1f, 0f);
  }
}
