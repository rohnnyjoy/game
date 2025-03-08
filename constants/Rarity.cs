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
        return new Color(0.5f, 0.5f, 0.5f);
      case Rarity.Uncommon:
        return new Color(0.2f, 0.8f, 0.2f);
      case Rarity.Rare:
        return new Color(0.2f, 0.2f, 0.8f);
      case Rarity.Epic:
        return new Color(0.8f, 0.2f, 0.8f);
      case Rarity.Legendary:
        return new Color(1.0f, 1.0f, 0.2f);
      default:
        return new Color(1.0f, 1.0f, 1.0f); // Default to white if rarity is unknown
    }
  }
}