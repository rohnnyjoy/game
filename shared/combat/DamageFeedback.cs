using Godot;
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
    private StandardMaterial3D _flashMaterial = default!;
    private int _flashToken = 0;

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
    public void Trigger(float amount, float flashDuration = 0.08f)
    {
      // Fire-and-forget flash
      _ = FlashAsync(flashDuration);
    }

    private async Task FlashAsync(float durationSeconds)
    {
      _flashToken++;
      int token = _flashToken;

      foreach (var mi in _meshInstances)
        mi.MaterialOverride = _flashMaterial;

      var timer = GetTree().CreateTimer(Mathf.Max(0.01f, durationSeconds));
      await ToSignal(timer, "timeout");

      if (token == _flashToken)
      {
        foreach (var mi in _meshInstances)
          mi.MaterialOverride = null;
      }
    }
  }
}
