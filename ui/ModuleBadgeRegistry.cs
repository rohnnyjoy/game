using Godot;
using System;
using System.Collections.Generic;

public partial class ModuleBadgeRegistry : Node
{
  public static ModuleBadgeRegistry Instance { get; private set; }

  // moduleId -> badge
  private readonly Dictionary<string, ModuleBadge> _badges = new();

  public event Action<string> BadgeChanged;

  public override void _EnterTree()
  {
    if (Instance != null && Instance != this)
    {
      GD.PushWarning("ModuleBadgeRegistry instance replaced");
    }
    Instance = this;
    InventoryStore.Instance?.ReplayModuleBadges();
  }

  public override void _ExitTree()
  {
    if (Instance == this)
      Instance = null;
    base._ExitTree();
  }

  public void SetBadge(string moduleId, ModuleBadge badge)
  {
    if (string.IsNullOrEmpty(moduleId)) return;
    if (string.IsNullOrEmpty(badge.Text))
    {
      ClearBadge(moduleId);
      return;
    }
    _badges[moduleId] = badge;
    BadgeChanged?.Invoke(moduleId);
  }

  public void ClearBadge(string moduleId)
  {
    if (string.IsNullOrEmpty(moduleId)) return;
    if (_badges.Remove(moduleId))
      BadgeChanged?.Invoke(moduleId);
  }

  public bool TryGetBadge(string moduleId, out ModuleBadge badge)
  {
    return _badges.TryGetValue(moduleId, out badge);
  }
}
