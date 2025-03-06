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
    // Create a new Area3D node to represent the explosion effect.
    Area3D explosionArea = new Area3D();
    explosionArea.Name = "ExplosionEffect";
    explosionArea.CollisionLayer = 0;
    explosionArea.CollisionMask = 0;

    // Create a MeshInstance3D for the visual explosion.
    MeshInstance3D explosionInstance = new MeshInstance3D();
    SphereMesh sphereMesh = new SphereMesh();

    // Randomize sphere segments and rings.
    sphereMesh.RadialSegments = (int)(4 + (8 - 4) * (float)GD.Randf());
    sphereMesh.Rings = (int)(2 + (4 - 2) * (float)GD.Randf());

    // Instead of GD.RandfRange, calculate inline for visual radius.
    float visualRadius = ExplosionRadius * (0.8f + (1.0f - 0.8f) * (float)GD.Randf());
    sphereMesh.Radius = visualRadius;
    sphereMesh.Height = visualRadius * 2;
    explosionInstance.Mesh = sphereMesh;

    // Create and set up the material.
    StandardMaterial3D material = new StandardMaterial3D();
    float randomGreen = Mathf.Lerp(0.5f, 1.0f, (float)GD.Randf());
    Color explosionColor = new Color(1, randomGreen, 0, 0.5f);
    material.AlbedoColor = explosionColor;
    // Updated for Godot 4.3: use StandardMaterial3D.TransparencyMode enum.
    material.Transparency = TransparencyEnum.Alpha;
    material.ShadingMode = ShadingModeEnum.Unshaded;
    // material.FlagsTransparent = true;
    explosionInstance.MaterialOverride = material;

    // Add the explosion visual to the explosion area.
    explosionArea.AddChild(explosionInstance);
    explosionArea.Position = position;
    tree.CurrentScene.AddChild(explosionArea);

    // Randomize rotation.
    explosionInstance.RotationDegrees = new Vector3(
        (float)GD.Randf() * 360f,
        (float)GD.Randf() * 360f,
        (float)GD.Randf() * 360f
    );

    // Set initial scale using inline randomization.
    float initialScaleFactor = 0.1f * (0.8f + (1.2f - 0.8f) * (float)GD.Randf());
    explosionInstance.Scale = new Vector3(initialScaleFactor, initialScaleFactor, initialScaleFactor);

    // Animate the explosion using a Tween.
    Tween tween = explosionInstance.CreateTween();
    float finalScaleFactor = 3f * (0.8f + (1.2f - 0.8f) * (float)GD.Randf());
    tween.TweenProperty(explosionInstance, "scale", new Vector3(finalScaleFactor, finalScaleFactor, finalScaleFactor), 0.02f)
         .SetTrans(Tween.TransitionType.Linear)
         .SetEase(Tween.EaseType.Out);
    tween.TweenProperty(material, "albedo_color", new Color(1, randomGreen, 0, 0), 0.2f)
         .SetTrans(Tween.TransitionType.Linear)
         .SetEase(Tween.EaseType.Out);

    // Remove the explosion effect when the tween finishes.
    tween.Finished += () => explosionArea.QueueFree();
  }
}
