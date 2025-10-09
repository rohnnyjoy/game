#nullable enable

using Godot;

namespace Shared.Runtime
{
  /// <summary>
  /// Centralizes physics layer bit management so gameplay code can opt-in to
  /// shared meanings instead of hardcoding indices in multiple places.
  /// </summary>
  public static class PhysicsLayers
  {
    public enum Layer
    {
      World = 0,
      Player = 1,
      Enemy = 2,
      Projectile = 3,
      Interactables = 4,
      SafeZone = 5
    }

    /// <summary>
    /// Builds a collision mask from the provided layer enum values.
    /// </summary>
    public static uint Mask(params Layer[] layers)
    {
      uint mask = 0;
      if (layers == null || layers.Length == 0)
        return mask;
      foreach (Layer layer in layers)
        mask |= 1u << (int)layer;
      return mask;
    }

    public static uint Add(uint mask, Layer layer)
    {
      return mask | (1u << (int)layer);
    }

    public static uint Remove(uint mask, Layer layer)
    {
      return mask & ~(1u << (int)layer);
    }

    public static bool Contains(uint mask, Layer layer)
    {
      return (mask & (1u << (int)layer)) != 0;
    }

    public static void ApplyLayers(PhysicsBody3D body, params Layer[] layers)
    {
      if (body == null)
        return;
      body.CollisionLayer = Mask(layers);
    }

    public static void ApplyMask(PhysicsBody3D body, params Layer[] layers)
    {
      if (body == null)
        return;
      body.CollisionMask = Mask(layers);
    }
  }
}
