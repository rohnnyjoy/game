using Godot;

#nullable enable

public partial class ShopItem : ModulePickup
{
  private const float PriceLabelHeight = 2.0f;
  private static readonly Color SoldOutColor = new Color(0.95f, 0.35f, 0.35f);
  private static readonly Color AffordColor = Colors.White;
  private static readonly Color CannotAffordColor = new Color(0.85f, 0.4f, 0.4f);

  [Export]
  public int Price { get; set; } = 100;

  private Text3DLabel? _priceLabel;
  private bool _sold;

  public override void _Ready()
  {
    Freeze = true;
    base._Ready();

    _priceLabel = new Text3DLabel
    {
      Name = "PriceLabel",
      FontPath = "res://assets/fonts/Born2bSportyV2.ttf",
      FontSize = 40,
      PixelSize = 0.01f,
      Color = AffordColor,
      OutlineColor = new Color(0f, 0f, 0f, 1f),
      OutlineSize = 8,
      Shaded = false,
      FaceCamera = true,
      EnableShadow = true,
      ShadowColor = new Color(0f, 0f, 0f, 0.35f),
      ShadowOffset = 0.0075f,
      EnableFloat = true,
      FloatAmplitude = 0.1f,
      FloatFrequency = 1.8f,
    };
    _priceLabel.Position = new Vector3(0f, PriceLabelHeight, 0f);
    UpdatePriceLabel();
    AddChild(_priceLabel);
    UpdatePriceColors();
  }

  public override void _Process(double delta)
  {
    base._Process(delta);
    UpdatePriceColors();
  }

  public override void OnInteract(string actionName)
  {
    if (actionName != InteractionOption.DefaultAction)
      return;

    if (_sold)
      return;

    Inventory? inventory = Player.Instance?.Inventory;
    if (inventory == null)
      return;

    int currentMoney = inventory.Money;
    if (currentMoney < Price)
    {
      IndicateCannotAfford();
      return;
    }

    if (!TryTransferToInventory())
      return;

    int newAmount = currentMoney - Price;
    if (GlobalEvents.Instance != null)
      GlobalEvents.Instance.EmitMoneyUpdated(currentMoney, newAmount);
    inventory.Money = newAmount;

    _sold = true;
    UpdatePriceLabel();
    QueueFree();
  }

  public override string GetInteractionText()
  {
    if (_sold)
      return "Sold out";

    string moduleName = Module?.ModuleName ?? Module?.GetType().Name ?? "Module";
    Inventory? inventory = Player.Instance?.Inventory;
    if (inventory != null && inventory.Money >= Price)
      return $"Buy {moduleName} (${Price})";

    int missing = inventory != null ? Mathf.Max(0, Price - inventory.Money) : Price;
    return missing > 0
      ? $"Need ${missing} more for {moduleName}"
      : $"Buy {moduleName} (${Price})";
  }

  private void UpdatePriceLabel()
  {
    if (_priceLabel == null)
      return;

    if (_sold)
    {
      _priceLabel.SetText("SOLD");
      _priceLabel.Color = SoldOutColor;
      _priceLabel.EnablePulse = false;
      return;
    }

    _priceLabel.SetText($"${Price}");
    _priceLabel.Color = AffordColor;
    _priceLabel.EnablePulse = false;
  }

  private void UpdatePriceColors()
  {
    if (_priceLabel == null || _sold)
      return;

    Inventory? inventory = Player.Instance?.Inventory;
    bool canAfford = inventory != null && inventory.Money >= Price;
    _priceLabel.Color = canAfford ? AffordColor : CannotAffordColor;

    if (canAfford && _priceLabel.EnablePulse)
      _priceLabel.EnablePulse = false;
  }

  private void IndicateCannotAfford()
  {
    if (_priceLabel == null)
      return;

    _priceLabel.Color = CannotAffordColor;
    _priceLabel.EnablePulse = true;
    _priceLabel.PulseAmount = 0.25f;
    _priceLabel.PulseSpeed = 9.0f;
  }
}
