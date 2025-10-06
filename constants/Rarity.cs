using Godot;

public enum Rarity
{
  Common,
  Uncommon,
  Rare,
  Epic,
  Legendary
}

public static class RarityExtensions
{
  // Get color from rarity
  public static Color GetColor(this Rarity rarity)
  {
    switch (rarity)
    {
      case Rarity.Common:
        return new Color(0.78f, 0.78f, 0.78f);
      case Rarity.Uncommon:
        return new Color(0.1f, 0.95f, 0.25f);
      case Rarity.Rare:
        return new Color(0.18f, 0.46f, 1.0f);
      case Rarity.Epic:
        return new Color(0.96f, 0.23f, 1.0f);
      case Rarity.Legendary:
        return new Color(1.0f, 0.64f, 0.0f);
      default:
        return new Color(1.0f, 1.0f, 1.0f); // Default to white if rarity is unknown
    }
  }
}
