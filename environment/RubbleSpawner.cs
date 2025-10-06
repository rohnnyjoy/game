using Godot;
using System;
using System.Threading.Tasks;

public partial class RubbleSpawner : Node
{
	[Export]
	public PackedScene RubbleScene;

	[Export]
	public float BaseScatterForce = 5.0f;

	// New property: how long the rubble will exist before despawning.
	[Export]
	public float RubbleLifetime = 5.0f;

	// Now marked as async to allow for timed despawning.
	public async void EmitRubbleAt(Vector3 position, Vector3 impactDirection, Vector3 colliderNormal, float impactDamage)
	{
    if (RubbleScene == null)
      return;

		float scatterForce = BaseScatterForce;
		Vector3 bounceDirection = impactDirection.Bounce(colliderNormal).Normalized();

		Node rubble = RubbleScene.Instantiate();

		// Set the global position using the computed offset.
		if (rubble is Node3D rubbleNode)
			rubbleNode.Transform = new Transform3D()
			{
				Origin = position + bounceDirection * 0.3f,
				Basis = Basis.Identity
			};
		//  = position + bounceDirection * 0.3f;

		// Add the rubble to the scene.
		AddChild(rubble);

		// If the rubble is physics-enabled, apply an impulse.
		if (rubble is RigidBody3D body)
		{
			// Calculate angle of bounce of the impact off the normal.
			body.ApplyCentralImpulse(bounceDirection * scatterForce);
		}

		// Wait for RubbleLifetime seconds before despawning the rubble.
		await ToSignal(GetTree().CreateTimer(RubbleLifetime), "timeout");

		// Safely remove the rubble if it still exists.
		if (IsInstanceValid(rubble))
		{
			rubble.QueueFree();
		}
	}
}
