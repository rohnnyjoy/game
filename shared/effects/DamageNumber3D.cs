using Godot;
using System;

public partial class DamageNumber3D : Node3D
{
  // Spawns a damage number above a target using AttentionText3D under the hood.
  public static void Spawn(Node context, Node3D target, float amount, Color? color = null, float yJitterMax = 1.2f)
  {
    if (context == null || !GodotObject.IsInstanceValid(context)) return;
    var tree = context.GetTree();
    if (tree?.CurrentScene == null || target == null || !GodotObject.IsInstanceValid(target)) return;

    var node = new AttentionText3D
    {
      Text = ((int)Mathf.Round(amount)).ToString(),
      DefaultTextColor = color ?? Colors.White,
      OutlineColor = Colors.Black,
      OutlineSize = 14,
      EnableShadow = false,
      Shaded = false,
      Palette = new(),
      // Tighter numeric spacing and larger size
      FontSize = 56,
      AdvanceFactor = 0.45f,
      // Scoring feel
      EnableRotate = true,
      RotateMode = 1,
      SequentialPopIn = true,
      MinCycleTime = 1f,
      PopInRate = 6.0f,
      AutoPopOut = true,
      PopDelay = 0.25f,
      PopOutRate = 4.0f,
      EnableFloat = false,
      EnableQuiver = false,
      AutoFreeAfter = false,
      HoldSeconds = 0.45f,
      FadeOutSeconds = 0.33f,
    };

    // Use global UI font for damage numbers
    var dmgFont = GD.Load<FontFile>("res://assets/fonts/Born2bSportyV2.ttf");
    if (dmgFont != null) node.Font = dmgFont;

    var rng = new RandomNumberGenerator();
    rng.Randomize();
    // Add to tree first, then set global position (safer wrt parenting transforms)
    tree.CurrentScene.AddChild(node);
    float baseHeight = 1.6f;
    float range = Mathf.Max(0.6f, yJitterMax);
    float yJitter = rng.RandfRange(0.3f, range);
    node.GlobalPosition = target.GlobalTransform.Origin + Vector3.Up * (baseHeight + yJitter);
    node.TriggerPulse(0.2f);
    }
}
