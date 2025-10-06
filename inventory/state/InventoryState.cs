using Godot;
using System;
using System.Collections.Generic;

public enum StackKind
{
  Inventory = 0,
  PrimaryWeapon = 1
}

public enum ChangeOrigin
{
  Unknown = 0,
  Gameplay = 1,
  UI = 2,
  System = 3
}

public sealed class ModuleVm
{
  public string ModuleId { get; }
  public Texture2D Icon { get; }
  public string Tooltip { get; }

  public ModuleVm(string moduleId, Texture2D icon, string tooltip)
  {
    ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
    Icon = icon;
    Tooltip = tooltip ?? string.Empty;
  }
}

public sealed class ModuleData
{
  public string ModuleId { get; }
  public WeaponModule Module { get; }
  public Texture2D Icon { get; }
  public string Name { get; }
  public string Description { get; }

  public ModuleData(string moduleId, WeaponModule module)
  {
    ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
    Module = module ?? throw new ArgumentNullException(nameof(module));
    Icon = module.CardTexture;
    Name = module.ModuleName ?? module.GetType().Name;
    Description = module.ModuleDescription ?? string.Empty;
  }

  public ModuleVm ToViewModel()
  {
    string tooltip = string.IsNullOrWhiteSpace(Description) ? Name : $"{Name}\n{Description}";
    return new ModuleVm(ModuleId, Icon, tooltip);
  }
}

public sealed class InventoryState
{
  public IReadOnlyList<string> InventoryModuleIds { get; }
  public IReadOnlyList<string> PrimaryWeaponModuleIds { get; }

  public InventoryState(IReadOnlyList<string> inventoryIds, IReadOnlyList<string> primaryIds)
  {
    InventoryModuleIds = inventoryIds ?? Array.Empty<string>();
    PrimaryWeaponModuleIds = primaryIds ?? Array.Empty<string>();
  }

  public static readonly InventoryState Empty = new InventoryState(Array.Empty<string>(), Array.Empty<string>());
}
