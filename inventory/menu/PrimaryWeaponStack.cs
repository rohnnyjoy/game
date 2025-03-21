using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class PrimaryWeaponStack : CardStack
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
    var newCards = new Array<Card2D>();
    foreach (WeaponModule module in Player.Instance.Inventory.PrimaryWeapon.Modules)
    {
      var existingCard = findCard(module);
      if (existingCard == null)
      {
        WeaponModuleCard2D card = new WeaponModuleCard2D();
        card.Module = module;
        newCards.Add(card);
      }
      else
      {
        newCards.Add(existingCard);
      }
    }

    UpdateCards(newCards);
  }


  private WeaponModuleCard2D findCard(WeaponModule module)
  {
    foreach (Card2D card in GetCards())
    {
      if (card is WeaponModuleCard2D moduleCard && moduleCard.Module == module)
        return moduleCard;
    }
    return null;
  }

  private void OnInventoryChanged()
  {
    PopulateCards();
  }

  public override void OnCardsChanged(Array<Card2D> newCards)
  {
    base.OnCardsChanged(newCards);
    var newModules = new Array<WeaponModule>();
    foreach (Card2D card in GetCards())
    {
      if (card is WeaponModuleCard2D moduleCard)
        newModules.Add(moduleCard.Module);
    }
    var newPrimaryWeapon = Player.Instance.Inventory.PrimaryWeapon;
    newPrimaryWeapon.Modules = newModules;
    Player.Instance.Inventory.PrimaryWeapon = newPrimaryWeapon;
  }
}
