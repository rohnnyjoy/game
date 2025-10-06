using Godot;
using System;

public static class LootSpawner
{
  public static void Spawn(LootKind kind, int quantity, Vector3 origin, Node parent)
  {
    switch (kind)
    {
      case LootKind.HealthPotion:
        SpawnHealthPotions(quantity, origin, parent);
        break;
      default:
        break;
    }
  }

  private static void SpawnHealthPotions(int quantity, Vector3 origin, Node parent)
  {
    if (parent == null || quantity <= 0) return;
    if (ItemRenderer.Instance != null)
    {
      ItemRenderer.Instance.SpawnPotionsAt(origin, quantity);
      return;
    }
    // Fallback: spawn node-based potions directly
    HealthPotion.Prewarm();
    var rng = new RandomNumberGenerator();
    rng.Randomize();
    for (int i = 0; i < quantity; i++)
    {
      Vector3 offset = new Vector3(
        rng.RandfRange(-0.5f, 0.5f),
        rng.RandfRange(0.15f, 0.4f),
        rng.RandfRange(-0.5f, 0.5f)
      );
      var potion = new HealthPotion();
      potion.GlobalTransform = new Transform3D(Basis.Identity, origin + offset);
      parent.AddChild(potion);
    }
  }
}
