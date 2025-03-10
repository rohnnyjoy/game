using Godot;
using System;
using System.Threading.Tasks;
using Godot.Collections;

public partial class AimbotModule : WeaponModule
{
  [Export]
  public float aim_cone_angle { get; set; } = (float)(45 * Math.PI / 180.0); // 45° in radians

  [Export]
  public float vertical_offset { get; set; } = 0.0f;

  [Export]
  public float target_line_width { get; set; } = 0.1f;

  [Export]
  public float target_line_duration { get; set; } = 0.05f;

  public AimbotModule()
  {
    CardTexture = GD.Load<Texture2D>("res://icons/aimbot.png");
    Rarity = Rarity.Epic;
    ModuleDescription = "Attacks reorient mid-flight toward the nearest enemy within 10m and a 45° cone.";
  }

  public override async Task OnCollision(Bullet.CollisionData collisionData, Bullet bullet)
  {
    if (!IsInstanceValid(bullet))
      return;
    await Aimbot(bullet);
  }

  public override async Task OnFire(Bullet bullet)
  {
    if (!IsInstanceValid(bullet))
      return;
    await Aimbot(bullet);
  }

  private async Task Aimbot(Bullet bullet)
  {
    if (!IsInstanceValid(bullet))
      return;

    GD.Print("Aimbot: Start");
    // Use bullet's current velocity as the base direction.
    Vector3 origin = bullet.GlobalTransform.Origin;
    Vector3 base_direction = bullet.Velocity.Normalized();

    GD.Print("Aimbot: Origin: " + origin);

    // Retrieve the last enemy this bullet locked onto (if any) using instance ID.
    Enemy storedEnemy = null;
    if (bullet.HasMeta("last_locked_enemy_id"))
    {
      // Cast meta value to ulong
      ulong enemyId = (ulong)bullet.GetMeta("last_locked_enemy_id");
      foreach (Node enemyNode in bullet.GetTree().GetNodesInGroup("enemies"))
      {
        if (enemyNode is Enemy enemy && enemy.GetInstanceId() == enemyId)
        {
          storedEnemy = enemy;
          break;
        }
      }
      if (storedEnemy == null)
        bullet.RemoveMeta("last_locked_enemy_id");
    }
    Node lastLockedEnemy = storedEnemy;
    GD.Print("Aimbot: Last locked enemy: " + lastLockedEnemy);

    // Choose the best enemy (prefer one not equal to last_locked_enemy).
    Enemy best_enemy = null;
    float best_angle = aim_cone_angle;

    // Also track a fallback candidate.
    Enemy fallback_enemy = null;
    float fallback_angle = aim_cone_angle;

    var spaceState = bullet.GetWorld3D().DirectSpaceState;

    // Look for an enemy within the cone.
    foreach (Node enemyNode in bullet.GetTree().GetNodesInGroup("enemies"))
    {
      if (enemyNode is Enemy enemy)
      {
        if (!IsInstanceValid(enemy))
          continue;

        Vector3 enemy_origin = enemy.GlobalTransform.Origin;
        Vector3 to_enemy = (enemy_origin - origin).Normalized();
        float angle = Mathf.Acos(base_direction.Dot(to_enemy));
        // Check if enemy falls within the allowed cone.
        if (angle < aim_cone_angle)
        {
          GD.Print("Aimbot: Enemy within cone: " + enemy.Name);
          // Determine the aim point: simply use the enemy's origin.
          Vector3 aim_point = enemy_origin;
          aim_point.Y += vertical_offset;

          // Prepare raycast parameters.
          PhysicsRayQueryParameters3D rayParams = new PhysicsRayQueryParameters3D
          {
            From = origin,
            To = aim_point
          };

          // Build an exclusion list: exclude the bullet and every enemy except the current one.
          var enemyList = bullet.GetTree().GetNodesInGroup("enemies");
          enemyList.Remove(enemy);
          var excludeArray = new Array<Rid> { bullet.GetRid() };
          foreach (Enemy e in enemyList)
          {
            if (IsInstanceValid(e))
              excludeArray.Add(e.GetRid());
          }
          rayParams.Exclude = excludeArray;

          // Raycast from bullet's origin to the aim point.
          var rayResult = spaceState.IntersectRay(rayParams);
          // Check if no collision occurred or if the collider is the enemy.
          if (rayResult.Count == 0 ||
              (rayResult.ContainsKey("collider") &&
               (Node)rayResult["collider"] == enemy))
          {
            // If enemy is not the last locked, check if it's the best candidate.
            if (enemy != lastLockedEnemy)
            {
              if (angle < best_angle)
              {
                best_angle = angle;
                best_enemy = enemy;
              }
            }
            else
            {
              GD.Print("Aimbot: Enemy is the last locked: " + enemy.Name);
            }
            // Regardless, track a fallback candidate.
            if (angle < fallback_angle)
            {
              fallback_angle = angle;
              fallback_enemy = enemy;
            }
          }
        }
      }
    }

    GD.Print($"Aimbot: Best enemy: {best_enemy?.Name}, Fallback: {fallback_enemy?.Name}");

    // Use best_enemy if found, otherwise fallback.
    if (best_enemy == null && fallback_enemy != null)
      best_enemy = fallback_enemy;

    // If an enemy was found, adjust the bullet's velocity and flash a red line.
    if (best_enemy != null && IsInstanceValid(best_enemy))
    {
      if (!IsInstanceValid(bullet))
        return;

      GD.Print("W1", best_enemy);
      Vector3 aim_point;
      if (best_enemy is CharacterBody3D bestEnemy3D)
      {
        aim_point = bestEnemy3D.GlobalTransform.Origin;
      }
      else
      {
        aim_point = best_enemy.GlobalTransform.Origin;
      }
      // Apply vertical offset.
      aim_point.Y += vertical_offset;

      // Adjust bullet velocity.
      bullet.Velocity = (aim_point - origin).Normalized() * bullet.Speed;
      // Update the bullet's last locked enemy using its instance ID.
      GD.Print("Setting meta");
      if (IsInstanceValid(bullet) && IsInstanceValid(best_enemy))
      {
        bullet.SetMeta("last_locked_enemy_id", best_enemy.GetInstanceId());
      }
      GD.Print("Set meta");
      // Flash a red line from the bullet to the target.
      await FlashLine(bullet, origin, aim_point);
    }
  }

  private async Task FlashLine(Node bullet, Vector3 start, Vector3 end)
  {
    if (!IsInstanceValid(bullet))
      return;

    SceneTree tree = bullet.GetTree();
    if (tree == null)
      return;

    // Get a valid parent node from the current scene.
    Node parent_node = tree.CurrentScene;
    if (parent_node == null)
      parent_node = tree.Root.GetChild(0);

    // Create a MeshInstance3D with a BoxMesh.
    MeshInstance3D line_instance = new MeshInstance3D();
    BoxMesh box = new BoxMesh();
    float thickness = target_line_width;
    float distance = start.DistanceTo(end);
    box.Size = new Vector3(thickness, thickness, distance);
    line_instance.Mesh = box;

    // Position the box at the midpoint.
    Vector3 mid_point = (start + end) * 0.5f;
    Transform3D transform = Transform3D.Identity;
    transform.Origin = mid_point;

    // Align the box so its local Z axis points from start to end.
    Vector3 direction = (end - start).Normalized();
    transform.Basis = Basis.LookingAt(direction, Vector3.Up);
    line_instance.Transform = transform;

    // Use an unshaded material and set depth draw mode to always draw.
    StandardMaterial3D material = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always,
      AlbedoColor = new Color(1, 0, 0),
    };
    line_instance.MaterialOverride = material;

    parent_node.AddChild(line_instance);

    // Keep the flash visible for target_line_duration seconds, then remove.
    var timer = tree.CreateTimer(target_line_duration);
    await ToSignal(timer, "timeout");
    if (IsInstanceValid(line_instance))
      line_instance.QueueFree();
  }
}
