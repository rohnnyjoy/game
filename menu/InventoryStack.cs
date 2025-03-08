using Godot;
using System.Collections.Generic;

public partial class InventoryStack : CardStack
{
  public override void _Ready()
  {
    base._Ready();

    Inventory inventory = Player.Instance.Inventory;
    inventory.InventoryChanged += OnInventoryChanged;

    PopulateCards();
  }

  private void PopulateCards()
  {
    // Remove existing cards.
    foreach (Node card in GetCards())
    {
      card.QueueFree();
    }

    // Create a card for each module in weapon_modules.
    Inventory inventory = Player.Instance.Inventory;
    foreach (WeaponModule module in inventory.WeaponModules)
    {
      WeaponModuleCard2D card = new WeaponModuleCard2D();
      card.Module = module;
      AddChild(card);
    }

    UpdateCards(false);
  }

  private void OnInventoryChanged()
  {
    GD.Print("INVENTORY CHANGED");
    PopulateCards();
  }

  // Override to update Inventory weapon_modules.
  public override void OnCardsReordered()
  {
    List<WeaponModule> newModules = new();

    foreach (Node card in GetCards())
    {
      if (card is WeaponModuleCard2D moduleCard)
      {
        newModules.Add(moduleCard.Module);
      }
    }

    Inventory inventory = Player.Instance.Inventory;
    inventory.WeaponModules.Clear();
    inventory.WeaponModules.AddRange(newModules);
  }
}
