#nullable enable

using Godot;
using System.Collections.Generic;

/// <summary>
/// Simple shop stand that spawns purchasable modules and offers a paid refresh interaction.
/// Items are positioned relative to the stand so the whole assembly can be placed as a unit.
/// </summary>
public partial class ShopStand : Area3D, IInteractable
{
  public const string RefreshActionName = "shop_refresh";
  private const string DefaultSafeZoneMaterialPath = "res://shop/materials/ShopSafeZone.material.tres";

  // Offsets for each shop slot; tweak in the inspector if you want a different layout.
  [Export] public Vector3[] ItemOffsets { get; set; } = new Vector3[]
  {
    new Vector3(-2.0f, 0.0f, 0.0f),
    new Vector3(0.0f, 0.0f, 0.0f),
    new Vector3(2.0f, 0.0f, 0.0f)
  };

  // Default prices for the offers. Adjust in the inspector as needed.
  [Export] public int[] ItemPrices { get; set; } = new[] { 75, 125, 200 };

  [Export] public int RefreshCost { get; set; } = 150;

  private float _interactionRadius = 3.5f;

  [Export(PropertyHint.Range, "0.5,25.0,0.1")] public float InteractionRadius
  {
    get => _interactionRadius;
    set
    {
      float clamped = Mathf.Max(0.5f, value);
      if (Mathf.IsEqualApprox(_interactionRadius, clamped))
        return;

      _interactionRadius = clamped;
      UpdateInteractionBounds();
      SyncSafeZone();
    }
  }

  [Export(PropertyHint.Range, "0.5,5.0,0.05")] public float SafeZoneRadiusMultiplier { get; set; } = 2.7f;
  [Export(PropertyHint.Range, "0.0,10.0,0.05")] public float SafeZoneRadiusPadding { get; set; } = 1.5f;

  [Export] public StandardMaterial3D? SafeZoneMaterial { get; set; }

  private readonly List<ShopItem> _spawnedItems = new();
  private Node3D _itemsRoot = null!;
  private CollisionShape3D _collisionShape = null!;
  private ShopSafeZone? _safeZone;

  public override void _Ready()
  {
    Monitoring = true;
    Monitorable = true;

    _itemsRoot = GetNodeOrNull<Node3D>("Items") ?? new Node3D { Name = "Items" };
    if (_itemsRoot.GetParent() != this)
      AddChild(_itemsRoot);

    _collisionShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
    if (_collisionShape == null)
    {
      _collisionShape = new CollisionShape3D { Name = "CollisionShape3D" };
      AddChild(_collisionShape);
    }

    if (_collisionShape.Shape is not SphereShape3D sphere)
    {
      sphere = new SphereShape3D();
      _collisionShape.Shape = sphere;
    }

    UpdateInteractionBounds();

    EnsureSafeZone();

    RefreshOffers();
  }

  public void OnInteract(string actionName)
  {
    if (actionName != RefreshActionName)
      return;

    Inventory? inventory = Player.Instance?.Inventory;
    if (inventory == null)
      return;

    if (inventory.Money < RefreshCost)
      return;

    int currentMoney = inventory.Money;
    int newAmount = currentMoney - RefreshCost;
    if (GlobalEvents.Instance != null)
      GlobalEvents.Instance.EmitMoneyUpdated(currentMoney, newAmount);
    inventory.Money = newAmount;

    RefreshOffers();
  }

  public string GetInteractionText()
  {
    return BuildRefreshText();
  }

  public IReadOnlyList<InteractionOption> GetInteractionOptions()
  {
    return new InteractionOption[]
    {
      new InteractionOption(RefreshActionName, BuildRefreshText())
    };
  }

  private void RefreshOffers()
  {
    ClearExistingOffers();

    Vector3[] offsets = EnsureOffsets();
    int[] prices = EnsurePrices();
    List<WeaponModule> modules = GetModules(offsets.Length);

    for (int i = 0; i < offsets.Length; i++)
    {
      WeaponModule module = modules[i % modules.Count];
      SpawnShopItem(module, prices[i % prices.Length], offsets[i]);
    }
  }

  private void ClearExistingOffers()
  {
    foreach (ShopItem item in _spawnedItems)
    {
      if (GodotObject.IsInstanceValid(item))
        item.QueueFree();
    }
    _spawnedItems.Clear();
  }

  private Vector3[] EnsureOffsets()
  {
    if (ItemOffsets == null || ItemOffsets.Length == 0)
      ItemOffsets = new[]
      {
        new Vector3(-1.5f, 0f, 0f),
        new Vector3(0f, 0f, 0f),
        new Vector3(1.5f, 0f, 0f)
      };
    return ItemOffsets;
  }

  private int[] EnsurePrices()
  {
    if (ItemPrices == null || ItemPrices.Length == 0)
      ItemPrices = new[] { 75, 125, 200 };
    return ItemPrices;
  }

  private List<WeaponModule> GetModules(int count)
  {
    List<WeaponModule> modules = new List<WeaponModule>();
    if (ItemPool.Instance != null)
    {
      List<WeaponModule> sampled = ItemPool.Instance.SampleModules(count);
      foreach (WeaponModule module in sampled)
      {
        WeaponModule copy = module.Duplicate(true) as WeaponModule ?? module;
        modules.Add(copy);
      }
    }

    if (modules.Count < count)
    {
      WeaponModule[] fallbacks =
      {
        new ScatterModule(),
        new ExplosiveModule(),
        new HomingModule()
      };
      int fallbackIndex = 0;
      while (modules.Count < count)
      {
        WeaponModule fallback = fallbacks[fallbackIndex % fallbacks.Length].Duplicate(true) as WeaponModule
          ?? fallbacks[fallbackIndex % fallbacks.Length];
        modules.Add(fallback);
        fallbackIndex++;
      }
    }

    if (modules.Count > count)
      modules = modules.GetRange(0, count);

    return modules;
  }

  private void SpawnShopItem(WeaponModule module, int price, Vector3 localOffset)
  {
    ShopItem item = new ShopItem
    {
      Price = Mathf.Max(0, price)
    };
    item.Initialize(module, null);
    _itemsRoot.AddChild(item);
    item.Position = localOffset;
    _spawnedItems.Add(item);
  }

  private string BuildRefreshText()
  {
    int playerMoney = Player.Instance?.Inventory?.Money ?? 0;
    if (playerMoney >= RefreshCost)
      return $"Refresh shop (${RefreshCost})";

    int missing = Mathf.Max(0, RefreshCost - playerMoney);
    return $"Refresh shop (${RefreshCost}) - Need ${missing}";
  }

  private void EnsureSafeZone()
  {
    _safeZone = GetNodeOrNull<ShopSafeZone>(nameof(ShopSafeZone))
      ?? CreateSafeZone();

    if (_safeZone != null)
    {
      // Ensure runtime-created zones always block projectiles/damage by default.
      _safeZone.BlocksDirectProjectiles = true;
      _safeZone.BlocksIndirectDamage = true;
    }

    SyncSafeZone();
  }

  private void UpdateInteractionBounds()
  {
    if (_collisionShape?.Shape is SphereShape3D sphere)
      sphere.Radius = _interactionRadius;
  }

  private ShopSafeZone CreateSafeZone()
  {
    ShopSafeZone zone = new ShopSafeZone
    {
      Name = nameof(ShopSafeZone)
    };
    AddChild(zone);
    return zone;
  }

  private void SyncSafeZone()
  {
    if (_safeZone == null)
      return;

    float scaled = _interactionRadius * SafeZoneRadiusMultiplier + SafeZoneRadiusPadding;
    float desiredRadius = Mathf.Max(_interactionRadius, scaled);
    _safeZone.Radius = Mathf.Max(0.5f, desiredRadius);

    if (_safeZone.Material == null)
    {
      SafeZoneMaterial ??= ResourceLoader.Load<StandardMaterial3D>(DefaultSafeZoneMaterialPath);
      _safeZone.Material = SafeZoneMaterial;
    }
  }
}
