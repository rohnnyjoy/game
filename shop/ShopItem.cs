using Godot;

public partial class ShopItem : WeaponModuleCard3D
{
  [Export]
  public int Price { get; set; } = 100; // You can set the default price or adjust via the editor.

  private Text3DLabel priceLabel;

  public override void _Ready()
  {
    // Call the base class _Ready to ensure the card is rendered as usual.
    base._Ready();

    // Create a 3D text label to display the price and configure it.
    priceLabel = new Text3DLabel
    {
      Text = "$" + Price.ToString(),
      FontPath = "res://assets/fonts/Born2bSportyV2.ttf",
      FontSize = 40,
      PixelSize = 0.01f,
      Color = Colors.White,
      OutlineColor = new Color(0,0,0,1),
      OutlineSize = 8,
      Shaded = false,
      FaceCamera = true,
      EnableShadow = true,
      ShadowColor = new Color(0,0,0,0.35f),
      ShadowOffset = 0.0075f
    };
    // Adjust the position so it appears to hover above the card.
    priceLabel.Position = new Vector3(0, 2, 0); // tweak as needed for your scene
    AddChild(priceLabel);
  }

  public override void OnInteract()
  {
    // Check if the player has enough money to purchase this item.
    if (Player.Instance.Inventory.Money >= Price)
    {
      // Deduct the price from the player's balance.
      Player.Instance.Inventory.Money -= Price;

      // "Unwrap" the module: if a module is set, add it to the player's inventory.
      if (Module != null)
      {
        var store = InventoryStore.Instance;
        if (store != null)
        {
          int insertIndex = store.State.InventoryModuleIds.Count;
          store.AddModule(Module, StackKind.Inventory, insertIndex, ChangeOrigin.Gameplay);
        }
        else
        {
          var newModules = new Godot.Collections.Array<WeaponModule>(Player.Instance.Inventory.WeaponModules)
                  {
                      Module
                  };
          Player.Instance.Inventory.WeaponModules = newModules;
        }
      }
      else { }

      // Remove the shop item from the scene after purchase.
      QueueFree();
    }
    else { }
  }
}
