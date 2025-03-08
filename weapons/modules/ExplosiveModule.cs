using Godot;
using System;
using System.Threading.Tasks;
using static Godot.BaseMaterial3D;

public partial class ExplosiveModule : WeaponModule
{
  [Export]
  public float ExplosionRadius { get; set; } = 2.5f;

  [Export]
  public float ExplosionDamageMultiplier { get; set; } = 0.25f;

  public ExplosiveModule()
  {
    // Set the module’s card texture and description.
    CardTexture = GD.Load<Texture2D>("res://icons/explosive.png");
    ModuleDescription = "Attacks explode on impact, dealing 25% damage in a 2-meter radius.";
    Rarity = Rarity.Uncommon;
  }

  public override async Task OnCollision(Bullet.CollisionData collision, Bullet bullet)
  {
    // Use the bullet's global position as the center of the explosion.
    Vector3 explosionCenter = bullet.GlobalPosition;
    float aoeDamage = bullet.Damage * ExplosionDamageMultiplier;

    // Spawn the explosion effect.
    SpawnExplosion(explosionCenter, bullet.GetTree());

    // Damage any enemy nodes in the "enemies" group within the explosion radius.
    var enemies = bullet.GetTree().GetNodesInGroup("enemies");
    foreach (Node enemy in enemies)
    {
      if (enemy is Enemy enemyNode && enemy.HasMethod("TakeDamage"))
      {
        // Calculate the distance between the explosion and the enemy.
        float distance = explosionCenter.DistanceTo(enemyNode.GlobalPosition);
        // Only apply damage if within the explosion radius.
        if (distance <= ExplosionRadius)
        {
          GD.Print($"Enemy {enemyNode.Name} took {aoeDamage} damage from explosion.");
          enemyNode.TakeDamage(aoeDamage);
        }
      }
    }
    await Task.CompletedTask;
  }


  public void SpawnExplosion(Vector3 position, SceneTree tree)
  {
    // Create the explosion effect area.
    Area3D explosionArea = new Area3D();
    explosionArea.Name = "ExplosionEffect";
    explosionArea.CollisionLayer = 0;
    explosionArea.CollisionMask = 0;

    // ----------------------
    // Main Explosion Visual
    // ----------------------
    MeshInstance3D explosionInstance = new MeshInstance3D();
    SphereMesh sphereMesh = new SphereMesh();

    // Set the visual radius exactly equal to the damage radius.
    float visualRadius = ExplosionRadius;
    // Use fixed values for segments and rings.
    sphereMesh.RadialSegments = 12;
    sphereMesh.Rings = 4;
    sphereMesh.Radius = visualRadius;
    sphereMesh.Height = visualRadius * 2;
    explosionInstance.Mesh = sphereMesh;

    // Set up a material for the explosion.
    StandardMaterial3D material = new StandardMaterial3D();
    // You can adjust the color as needed.
    material.AlbedoColor = new Color(1, 0.75f, 0, 0.3f);
    material.Transparency = TransparencyEnum.Alpha;
    material.ShadingMode = ShadingModeEnum.Unshaded;
    explosionInstance.MaterialOverride = material;

    // Add the explosion visual to the explosion area.
    explosionArea.AddChild(explosionInstance);
    explosionArea.Position = position;
    tree.CurrentScene.AddChild(explosionArea);

    // Apply a randomized rotation (optional).
    explosionInstance.RotationDegrees = new Vector3(
        (float)GD.Randf() * 360f,
        (float)GD.Randf() * 360f,
        (float)GD.Randf() * 360f
    );

    // ----------------------
    // Animate the Explosion
    // ----------------------
    // Set initial scale to 10% of the final size.
    float initialScaleFactor = 0.1f;
    explosionInstance.Scale = new Vector3(initialScaleFactor, initialScaleFactor, initialScaleFactor);

    // Tween the explosion to its full size (scale = 1, meaning the mesh's radius equals ExplosionRadius).
    Tween explosionTween = explosionInstance.CreateTween();
    explosionTween.TweenProperty(explosionInstance, "scale",
        new Vector3(1, 1, 1), 0.05f)
     .SetTrans(Tween.TransitionType.Linear)
     .SetEase(Tween.EaseType.Out);
    explosionTween.TweenProperty(material, "albedo_color",
        new Color(1, 0.75f, 0, 0), 0.1f)
     .SetTrans(Tween.TransitionType.Linear)
     .SetEase(Tween.EaseType.Out);

    // ----------------------
    // Smoke Cloud Visual
    // ----------------------
    MeshInstance3D smokeCloud = new MeshInstance3D();
    SphereMesh smokeMesh = new SphereMesh();
    smokeMesh.RadialSegments = 12;
    smokeMesh.Rings = 4;

    // Set smoke's radius relative to the explosion radius.
    float smokeVisualRadius = visualRadius * 1.5f;
    smokeMesh.Radius = smokeVisualRadius;
    smokeMesh.Height = smokeVisualRadius * 2;
    smokeCloud.Mesh = smokeMesh;

    // Set up the smoke material.
    StandardMaterial3D smokeMaterial = new StandardMaterial3D();
    Color smokeColor = new Color(1, 1, 1, 0.02f);
    smokeMaterial.AlbedoColor = smokeColor;
    smokeMaterial.Transparency = TransparencyEnum.Alpha;
    smokeMaterial.ShadingMode = ShadingModeEnum.Unshaded;
    smokeCloud.MaterialOverride = smokeMaterial;

    // Use the same initial scale as the explosion.
    smokeCloud.Scale = new Vector3(initialScaleFactor, initialScaleFactor, initialScaleFactor);
    explosionArea.AddChild(smokeCloud);

    // Animate the smoke cloud to its final scale.
    Tween smokeTween = smokeCloud.CreateTween();
    smokeTween.TweenProperty(smokeCloud, "scale",
        new Vector3(1, 1, 1), 0.15f)
     .SetTrans(Tween.TransitionType.Expo)
     .SetEase(Tween.EaseType.Out);
    smokeTween.TweenProperty(smokeMaterial, "albedo_color",
        new Color(1, 1, 1, 0), 0.05f)
     .SetTrans(Tween.TransitionType.Linear)
     .SetEase(Tween.EaseType.Out);

    // Remove the explosion effect after the smoke animation finishes.
    smokeTween.Finished += () => explosionArea.QueueFree();
  }
}
