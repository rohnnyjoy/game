using Godot;
using System.Collections.Generic;

#nullable enable

namespace Shared.Runtime
{
  /// <summary>
  /// Preloads frequently used assets and instantiates lightweight scene probes
  /// so the first in-game use does not hitch while the engine loads or compiles them.
  /// </summary>
  public static class AssetWarmup
  {
    private static bool _ran;
    private static readonly List<Resource> _resourceCache = new();

    private static readonly string[] ResourcePaths =
    {
      "res://assets/fonts/Born2bSportyV2.ttf",
      "res://assets/sounds/gun.wav",
      "res://assets/sounds/hit.mp3",
      "res://assets/sounds/hit.mp3",
      "res://assets/sounds/impact.wav",
      "res://assets/sprites/effects/curse_32x96.png",
      "res://assets/sprites/effects/dust/dust_28x12.png",
      "res://assets/sprites/effects/impact/impact_2_dust_48x48.png",
      "res://assets/sprites/effects/impact/impact_48x48.png",
      "res://assets/sprites/items/coin_20x20.png",
      "res://assets/sprites/items/health_potion_24x24.png",
      "res://assets/ui/3x/ninepatch.png",
      "res://assets/ui/items.png",
      "res://icons/explosive.png",
      "res://icons/shotgun.png",
      "res://reference/balatro/resources/sounds/coin1.ogg",
      "res://reference/balatro/resources/sounds/coin3.ogg",
      "res://reference/balatro/resources/sounds/coin6.ogg",
      "res://reference/balatro/resources/sounds/paper1.ogg",
      "res://shared/shaders/cursed_skull_beam.gdshader",
      "res://shared/shaders/dissolve_enemy.gdshader"
    };

    private static readonly string[] ScenePaths =
    {
      "res://shared/effects/DissolveBurst.tscn",
      "res://ui/TopLeftHud.tscn",
      "res://weapons/ol_reliable/OlReliable.tscn"
    };

    public static void Run(Node? host)
    {
      if (_ran)
        return;
      _ran = true;

      PreloadResources();
      WarmupScenes(host);
    }

    private static void PreloadResources()
    {
      foreach (var path in ResourcePaths)
      {
        if (string.IsNullOrEmpty(path))
          continue;
        var res = ResourceLoader.Load(path);
        if (res == null)
        {
          GD.PushWarning($"AssetWarmup: failed to load resource at {path}");
          continue;
        }
        if (!_resourceCache.Contains(res))
          _resourceCache.Add(res);
      }
    }

    private static void WarmupScenes(Node? host)
    {
      foreach (var path in ScenePaths)
      {
        if (ResourceLoader.Load(path) is not PackedScene scene)
        {
          GD.PushWarning($"AssetWarmup: failed to load scene at {path}");
          continue;
        }
        _resourceCache.Add(scene);
        if (host == null || !GodotObject.IsInstanceValid(host))
          continue;
        try
        {
          var instance = scene.Instantiate();
          if (instance is Node node)
          {
            node.Name = $"__warmup_{node.Name}";
            node.ProcessMode = Node.ProcessModeEnum.Disabled;
            if (node is CanvasItem canvas)
              canvas.Visible = false;
            if (node is Node3D node3D)
              node3D.Visible = false;
            host.AddChild(node);
            node.QueueFree();
          }
        }
        catch (System.Exception ex)
        {
          GD.PushWarning($"AssetWarmup: could not instantiate {path}: {ex.Message}");
        }
      }
    }
  }
}
