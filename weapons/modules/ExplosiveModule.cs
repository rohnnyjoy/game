using Godot;
using System;
using System.Threading.Tasks;
using static Godot.BaseMaterial3D;

public partial class ExplosiveModule : WeaponModule
{
  [Export]
  public float ExplosionRadius { get; set; } = 1.0f;

  [Export]
  public float ExplosionDamageMultiplier { get; set; } = 0.25f;

  public ExplosiveModule()
  {
    // Set the module’s card texture and description.
    CardTexture = GD.Load<Texture2D>("res://icons/explosive.png");
    ModuleDescription = "Attacks explode on impact, dealing 25% damage in a 2-meter radius.";
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
      if (enemy.IsInGroup("enemies") && enemy.HasMethod("TakeDamage"))
      {
        enemy.Call("TakeDamage", aoeDamage);
      }
    }
    await Task.CompletedTask;
  }

  public void SpawnExplosion(Vector3 position, SceneTree tree)
  {
    // Create a new Area3D node to represent the entire explosion effect.
    Area3D explosionArea = new Area3D();
    explosionArea.Name = "ExplosionEffect";
    explosionArea.CollisionLayer = 0;
    explosionArea.CollisionMask = 0;

    // ----------------------
    // Main Explosion Visual
    // ----------------------
    MeshInstance3D explosionInstance = new MeshInstance3D();
    SphereMesh sphereMesh = new SphereMesh();

    // Randomize sphere segments and rings.
    sphereMesh.RadialSegments = (int)(4 + (8 - 4) * (float)GD.Randf());
    sphereMesh.Rings = (int)(2 + (4 - 2) * (float)GD.Randf());

    // Calculate a randomized visual radius.
    float visualRadius = ExplosionRadius * (0.8f + (1.0f - 0.8f) * (float)GD.Randf());
    sphereMesh.Radius = visualRadius;
    sphereMesh.Height = visualRadius * 2;
    explosionInstance.Mesh = sphereMesh;

    // Create and set up the explosion material (with reduced opacity).
    StandardMaterial3D material = new StandardMaterial3D();
    float randomGreen = Mathf.Lerp(0.5f, 1.0f, (float)GD.Randf());
    Color explosionColor = new Color(1, randomGreen, 0, 0.3f);
    material.AlbedoColor = explosionColor;
    material.Transparency = TransparencyEnum.Alpha;
    material.ShadingMode = ShadingModeEnum.Unshaded;
    explosionInstance.MaterialOverride = material;

    // Add explosion visual to the explosion area.
    explosionArea.AddChild(explosionInstance);
    explosionArea.Position = position;
    tree.CurrentScene.AddChild(explosionArea);

    // Randomize rotation.
    explosionInstance.RotationDegrees = new Vector3(
        (float)GD.Randf() * 360f,
        (float)GD.Randf() * 360f,
        (float)GD.Randf() * 360f
    );

    // Set initial scale.
    float initialScaleFactor = 0.1f * (0.8f + (1.2f - 0.8f) * (float)GD.Randf());
    explosionInstance.Scale = new Vector3(initialScaleFactor, initialScaleFactor, initialScaleFactor);

    // Animate the explosion:
    // Expand its scale quickly in 0.1 seconds, then fade out (alpha to 0) in 0.2 seconds.
    Tween explosionTween = explosionInstance.CreateTween();
    float explosionFinalScaleFactor = 3f * (0.8f + (1.2f - 0.8f) * (float)GD.Randf());
    explosionTween.TweenProperty(explosionInstance, "scale",
        new Vector3(explosionFinalScaleFactor, explosionFinalScaleFactor, explosionFinalScaleFactor), 0.1f)
     .SetTrans(Tween.TransitionType.Linear)
     .SetEase(Tween.EaseType.Out);
    explosionTween.TweenProperty(material, "albedo_color",
        new Color(1, randomGreen, 0, 0), 0.2f)
     .SetTrans(Tween.TransitionType.Linear)
     .SetEase(Tween.EaseType.Out);

    // ----------------------
    // Smoke Cloud Visual
    // ----------------------
    MeshInstance3D smokeCloud = new MeshInstance3D();
    SphereMesh smokeMesh = new SphereMesh();
    smokeMesh.RadialSegments = (int)(4 + (8 - 4) * (float)GD.Randf());
    smokeMesh.Rings = (int)(2 + (4 - 2) * (float)GD.Randf());

    // For the smoke, set its base radius larger than the explosion.
    float smokeVisualRadius = visualRadius * 1.5f;
    smokeMesh.Radius = smokeVisualRadius;
    smokeMesh.Height = smokeVisualRadius * 2;
    smokeCloud.Mesh = smokeMesh;

    // Create and set up a white, translucent material for the smoke.
    StandardMaterial3D smokeMaterial = new StandardMaterial3D();
    // Lower initial opacity.
    Color smokeColor = new Color(1, 1, 1, 0.02f);
    smokeMaterial.AlbedoColor = smokeColor;
    smokeMaterial.Transparency = TransparencyEnum.Alpha;
    smokeMaterial.ShadingMode = ShadingModeEnum.Unshaded;
    smokeCloud.MaterialOverride = smokeMaterial;

    // Set initial scale for the smoke (matching the explosion’s initial scale).
    float smokeInitialScaleFactor = initialScaleFactor;
    smokeCloud.Scale = new Vector3(smokeInitialScaleFactor, smokeInitialScaleFactor, smokeInitialScaleFactor);

    // Add the smoke cloud to the explosion area.
    explosionArea.AddChild(smokeCloud);

    // Random variation for final smoke size (ranging from 0.8 to 1.0).
    float randomVariation = 0.8f + (float)GD.Randf() * 0.2f;

    // Animate the smoke cloud:
    // The scale tween lasts 0.3 seconds using an exponential transition.
    // The fade tween now fades out over 0.1 seconds with no delay.
    Tween smokeTween = smokeCloud.CreateTween();
    float smokeFinalScaleFactor = explosionFinalScaleFactor * 2.0f * randomVariation;
    smokeTween.TweenProperty(smokeCloud, "scale",
        new Vector3(smokeFinalScaleFactor, smokeFinalScaleFactor, smokeFinalScaleFactor), 0.3f)
     .SetTrans(Tween.TransitionType.Expo)
     .SetEase(Tween.EaseType.Out);
    smokeTween.TweenProperty(smokeMaterial, "albedo_color",
        new Color(1, 1, 1, 0), 0.1f)
     .SetTrans(Tween.TransitionType.Linear)
     .SetEase(Tween.EaseType.Out);

    // Remove the entire explosion effect once the smoke animation finishes.
    smokeTween.Finished += () => explosionArea.QueueFree();
  }
}
