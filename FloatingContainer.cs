using Godot;
using System;

public partial class FloatingContainer : Node3D
{
  [Export]
  public float FloatAmplitude = 2.0f;

  [Export]
  public float FloatSpeed = 1.0f;

  [Export]
  public float RotationSpeed = 0.5f;

  public override void _Ready()
  {
    // Store each Node3D child's initial Y position as metadata.
    foreach (Node child in GetChildren())
    {
      if (child is Node3D node)
      {
        // Save the initial Y position.
        node.SetMeta("base_y", node.Position.Y);
      }
    }
  }

  public override void _Process(double delta)
  {
    // Iterate over each Node3D child to update its position and rotation.
    foreach (Node child in GetChildren())
    {
      if (child is Node3D node)
      {
        if (!node.HasMeta("base_y"))
        {
          node.SetMeta("base_y", node.Position.Y);
        }

        // Retrieve the meta value and convert it to a float.
        object metaValue = node.GetMeta("base_y");
        float baseY = 0f;
        if (metaValue is float f)
          baseY = f;
        else if (metaValue is double d)
          baseY = (float)d;
        else
          baseY = 0f;

        // Calculate the sine wave offset for the floating effect.
        float offset = (float)Math.Sin(Time.GetTicksMsec() * FloatSpeed) * FloatAmplitude;

        Vector3 newPosition = node.Position;
        newPosition.Y = baseY + offset;
        node.Position = newPosition;

        // Update the rotation directly by modifying the rotation vector.
        Vector3 currentRotation = node.Rotation;
        currentRotation.Y += RotationSpeed * (float)delta;
        node.Rotation = currentRotation;
      }
    }
  }
}
