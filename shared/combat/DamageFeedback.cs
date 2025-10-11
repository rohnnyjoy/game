using Godot;
using System;
#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Combat
{
  /// <summary>
  /// Shared damage feedback helper: flashes MeshInstance3D children white and plays an impact sprite.
  /// Attach programmatically to targets (Enemy, Player) and call Trigger(amount).
  /// </summary>
  public partial class DamageFeedback : Node
  {
    private readonly List<MeshInstance3D> _meshInstances = new();
    // Record the original MaterialOverride for each mesh so we can reliably
    // restore even if multiple flashes overlap.
    private readonly Dictionary<MeshInstance3D, Material?> _originalOverride = new();
    private StandardMaterial3D _flashMaterial = default!;
    private int _flashToken = 0;

    public const float DefaultFlashDuration = 0.08f;

    /// <summary>
    /// Optional visual root to search for MeshInstance3D. If null, uses the parent Node.
    /// </summary>
    public Node? VisualRoot { get; set; }

    public override void _Ready()
    {
      // Collect meshes under the chosen visual root (or parent if not set).
      Node root = VisualRoot ?? GetParent();
      if (root != null)
        CollectMeshInstances(root);

      // Simple white emissive material for flash overlay.
      _flashMaterial = new StandardMaterial3D
      {
        AlbedoColor = new Color(1, 1, 1, 1),
        EmissionEnabled = true,
        Emission = new Color(1, 1, 1, 1)
      };

      // Capture original overrides after collection so restores are stable.
      foreach (var mi in _meshInstances)
      {
        if (GodotObject.IsInstanceValid(mi))
          _originalOverride[mi] = mi.MaterialOverride;
      }
    }

    private void CollectMeshInstances(Node node)
    {
      foreach (Node child in node.GetChildren())
      {
        if (child is MeshInstance3D mi)
          _meshInstances.Add(mi);

        // Recurse through all children (meshes are frequently nested).
        CollectMeshInstances(child);
      }
    }

    /// <summary>
    /// Call when damage is applied to the owner. Plays flash + impact sprite.
    /// </summary>
    public void Trigger(float amount, float flashDuration = DefaultFlashDuration)
    {
      // Fire-and-forget flash
      _ = FlashAsync(flashDuration);
    }

    private async Task FlashAsync(float durationSeconds)
    {
      _flashToken++;
      int token = _flashToken;
      foreach (var mi in _meshInstances)
      {
        if (!GodotObject.IsInstanceValid(mi))
          continue;
        // If we don't have a baseline for this mesh yet (e.g., dynamically added),
        // or it changed externally since _Ready, capture its current non-flash state.
        if (!_originalOverride.ContainsKey(mi) || _originalOverride[mi] == _flashMaterial)
          _originalOverride[mi] = mi.MaterialOverride;
        mi.MaterialOverride = _flashMaterial;
      }

      var timer = GetTree().CreateTimer(Mathf.Max(0.01f, durationSeconds));
      await ToSignal(timer, "timeout");

      if (token == _flashToken)
      {
        foreach (var mi in _meshInstances)
        {
          if (!GodotObject.IsInstanceValid(mi))
            continue;
          // Restore to the captured original, not whatever was set mid-flash.
          Material? restore = _originalOverride.TryGetValue(mi, out var mat) ? mat : null;
          mi.MaterialOverride = restore;
        }
      }
    }
  }
}
