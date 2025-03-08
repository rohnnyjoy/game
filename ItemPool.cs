using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ItemPool : Node
{
  public static ItemPool Instance { get; private set; }

  // List of all available modules.
  public List<WeaponModule> Modules { get; set; } = new List<WeaponModule>();

  // A shared random generator.
  private static readonly Random random = new Random();

  public override void _Ready()
  {
    Modules = new List<WeaponModule>{
      new PenetratingModule(),
      new HomingModule(),
      new ExplosiveModule(),
      new BouncingModule(),
    };
    Instance = this;
  }

  // Returns a weight based on the module's rarity.
  // Lower weights for rarer items (adjust these values as necessary).
  private float GetWeight(Rarity rarity)
  {
    switch (rarity)
    {
      case Rarity.Common: return 1f;
      case Rarity.Uncommon: return 0.5f;
      case Rarity.Rare: return 0.25f;
      case Rarity.Epic: return 0.1f;
      case Rarity.Legendary: return 0.05f;
      default: return 1f;
    }
  }

  // Samples 'n' modules without replacement using weighted probabilities based on rarity.
  public List<WeaponModule> SampleModules(int n)
  {
    // Make a copy of the list to remove selected modules without affecting the original list.
    List<WeaponModule> availableModules = new List<WeaponModule>(Modules);
    List<WeaponModule> sampledModules = new List<WeaponModule>();

    for (int i = 0; i < n && availableModules.Count > 0; i++)
    {
      // Calculate the total weight of the remaining modules.
      float totalWeight = availableModules.Sum(module => GetWeight(module.Rarity));

      // Pick a random value between 0 and totalWeight.
      float randomValue = (float)(random.NextDouble() * totalWeight);
      float cumulative = 0f;

      // Iterate through the modules to find which one corresponds to the random value.
      foreach (var module in availableModules)
      {
        cumulative += GetWeight(module.Rarity);
        if (randomValue <= cumulative)
        {
          sampledModules.Add(module);
          availableModules.Remove(module);
          break;
        }
      }
    }
    return sampledModules;
  }
}
