using Godot;
using System;

public partial class GrassMultiMesh : MultiMeshInstance3D
{
  [Export]
  public Mesh GrassMesh { get; set; }

  // Dimensions of the plane area where you want to place grass.
  [Export]
  public float Width { get; set; } = 10.0f;
  [Export]
  public float Depth { get; set; } = 10.0f;

  [Export]
  public int InstanceCount { get; set; } = 1000;

  // Only place grass on surfaces that are relatively flat.
  // 1 = perfectly horizontal, 0.8 ~ 36°, etc.
  [Export]
  public float MinNormalDot { get; set; } = 0.8f;

  // Exported noise texture to modulate grass density and scale.
  [Export]
  public Texture2D NoiseTexture { get; set; }

  // Noise threshold for placement (adjust to taste).
  [Export]
  public float NoiseThreshold { get; set; } = 0.5f;

  [Export]
  public int CollisionLayer { get; set; } = 9;

  // New exported variable to affect mesh scale.
  [Export]
  public float MeshScale { get; set; } = 1.0f;

  public override void _Ready()
  {
    CallDeferred("PopulateGrass");
  }

  private void PopulateGrass()
  {
    MultiMesh multi = new MultiMesh();
    multi.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
    // Enable custom data so we can store per-instance information.
    multi.UseCustomData = true;
    multi.InstanceCount = InstanceCount;
    multi.Mesh = GrassMesh;

    Random random = new Random();
    int validCount = 0;
    int attempts = 0;
    int maxAttempts = InstanceCount * 300;

    float rayStartY = 10f;
    float rayEndY = -10f;

    // Prepare the noise image (if provided) for sampling.
    Image noiseImage = null;
    if (NoiseTexture != null)
    {
      noiseImage = NoiseTexture.GetImage();
    }

    PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;

    while (validCount < InstanceCount && attempts < maxAttempts)
    {
      attempts++;

      float posX = (float)(random.NextDouble() * Width - Width / 2);
      float posZ = (float)(random.NextDouble() * Depth - Depth / 2);

      float u = (posX + Width / 2) / Width;
      float v = (posZ + Depth / 2) / Depth;
      int pixelX = (int)(u * noiseImage.GetWidth());
      int pixelY = (int)(v * noiseImage.GetHeight());
      Color noiseColor = noiseImage.GetPixel(pixelX, pixelY);
      float noiseValue = noiseColor.R; // Assuming a grayscale texture.

      // Skip this candidate if the noise value is below the threshold.
      if (noiseValue > Math.Pow((float)random.NextDouble(), 2))
        continue;

      Vector3 start = new Vector3(posX, rayStartY, posZ);
      Vector3 end = new Vector3(posX, rayEndY, posZ);

      PhysicsRayQueryParameters3D rayParams = new PhysicsRayQueryParameters3D
      {
        From = start,
        To = end,
        CollisionMask = 1 << 8,
      };

      var result = spaceState.IntersectRay(rayParams);
      if (result.Count > 0)
      {
        Node collider = (Node)result["collider"];
        if (collider == null || !collider.IsInGroup("grass"))
          continue;
        Vector3 normal = (Vector3)result["normal"];
        if (normal.Dot(Vector3.Up) >= MinNormalDot)
        {
          Vector3 collisionPosition = (Vector3)result["position"];
          Transform3D transform = new Transform3D();
          transform.Origin = collisionPosition;

          // Create a random rotation around the up axis.
          float rotationAngle = (float)(random.NextDouble() * 2.0 * Math.PI);
          Basis basis = new Basis(Vector3.Up, rotationAngle);

          // Random base scale modified by the MeshScale variable.
          float randomScale = (noiseValue * 2.5f + 0.6f) * MeshScale;

          transform.Basis = basis.Scaled(new Vector3(randomScale, randomScale, randomScale));

          // Set the instance transform.
          multi.SetInstanceTransform(validCount, transform);
          // Communicate the grass blade's position using custom data.
          // We use a Color to store 4 floats: x, y, z, and an extra value (here 0.0f).
          multi.SetInstanceCustomData(validCount, new Color(collisionPosition.X, collisionPosition.Y, collisionPosition.Z, rotationAngle));

          validCount++;
        }
      }
    }
    GD.Print($"Placed {validCount} grass blades after {attempts} attempts.");

    multi.InstanceCount = validCount;
    Multimesh = multi;
  }
}
