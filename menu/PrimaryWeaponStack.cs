using Godot;
using System.Collections.Generic;

public partial class PrimaryWeaponStack : CardStack
{
  public override void _Ready()
  {
    base._Ready();

    Inventory inventory = GetTree().Root.GetNode<Inventory>("InventorySingleton");
    // inventory.InventoryChanged += OnInventoryChanged;

    PopulateCards();
  }

  private void PopulateCards()
  {
    // Remove existing cards.
    foreach (Node card in GetCards())
    {
      card.QueueFree();
    }

    Inventory inventory = GetTree().Root.GetNode<Inventory>("InventorySingleton");

    // Create cards based on the primary weapon's modules.
    if (inventory.PrimaryWeapon != null)
    {
      foreach (WeaponModule module in inventory.PrimaryWeapon.Modules)
      {
        WeaponModuleCard2D card = new WeaponModuleCard2D();
        card.Module = module;
        AddChild(card);
      }
    }

    UpdateCards(false);
  }

  private void OnInventoryChanged()
  {
    PopulateCards();
  }

  // Override to update primary_weapon.modules from the new card order.
  public override void OnCardsReordered()
  {
    GD.Print("PrimaryWeaponStack.OnCardsReordered");
    Inventory inventory = GetTree().Root.GetNode<Inventory>("InventorySingleton");
    if (inventory.PrimaryWeapon == null)
      return;

    List<WeaponModule> newModules = new();

    foreach (Node card in GetCards())
    {
      if (card is WeaponModuleCard2D moduleCard)
      {
        newModules.Add(moduleCard.Module);
      }
    }

    // Instead of assigning a new list, update the existing one.
    inventory.PrimaryWeapon.Modules.Clear();
    inventory.PrimaryWeapon.Modules.AddRange(newModules);
  }
}
