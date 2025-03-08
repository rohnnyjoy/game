using Godot;

public partial class ShopItem : WeaponModuleCard3D
{
  [Export]
  public int Price { get; set; } = 100; // You can set the default price or adjust via the editor.

  private Label3D priceLabel;

  public override void _Ready()
  {
    // Call the base class _Ready to ensure the card is rendered as usual.
    base._Ready();

    // Create a Label3D to display the price and configure it.
    priceLabel = new Label3D();
    priceLabel.Text = "$" + Price.ToString();
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
        // Create a new array based on the current modules and add this one.
        var newModules = new Godot.Collections.Array<WeaponModule>(Player.Instance.Inventory.WeaponModules)
                {
                    Module
                };
        Player.Instance.Inventory.WeaponModules = newModules;
        GD.Print("Purchased module: " + Module.ToString());
      }
      else
      {
        GD.Print("No module available for purchase.");
      }

      // Remove the shop item from the scene after purchase.
      QueueFree();
    }
    else
    {
      // Not enough funds; provide feedback.
      GD.Print("Insufficient funds to purchase this item. Price: $" + Price + " | Current Money: $" + Player.Instance.Inventory.Money);
      // Optionally, you could play an error sound or display a visual message here.
    }
  }
}
