using Godot;
using System;

public partial class PlayerMovement : Node
{
  [Export] public float JumpVelocity = 24.0f;
  [Export] public float Gravity = 60.0f;
  [Export] public int MaxJumps = 2;
  [Export] public float Speed = 10.0f;
  [Export] public float InitialBoostFactor = 0.8f;
  [Export] public float GroundAccel = 80.0f;
  [Export] public float GroundDecel = 150.0f;
  [Export] public float SlideFrictionCoefficient = 15.0f;
  [Export] public float AirAcceleration = 22.0f;
  [Export] public float DashSpeed = 20.0f;
  [Export] public float WallHopMinNormalY = 0.7f;
  [Export] public float WallHopBoost = 1.2f;
  [Export] public float WallHopUpwardBoost = 18.0f;
  [Export] public NodePath PlayerPath;

  private const float AIR_FRICTION = 0.0f;


  private Player player;

  

  public override void _Ready()
  {
    player = GetNode<Player>(PlayerPath);
  }

  public override void _Input(InputEvent @event)
  {
    if (@event is InputEventKey keyEvent && !keyEvent.Echo && Input.IsActionJustPressed("jump"))
    {
      player.JumpBufferTimer = 0.1f;
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    ProcessMovement((float)delta);
  }

  public void ProcessMovement(float delta)
  {
    // Process jumping and gravity.
    ProcessJumpAndGravity(delta);

    Vector3 inputDirection = player.GetInputDirection();
    if (player.IsOnFloor())
      ProcessGroundMovement(inputDirection, delta);
    else
      ProcessAirMovement(inputDirection, delta);

    // Add knockback impulse velocity before sliding and decay it after.
    player.PreSlideHorizontalVelocity = new Vector3(player.Velocity.X, 0, player.Velocity.Z);
    player.Velocity += player.KnockbackVelocity;
    player.MoveAndSlide();
    player.StepKnockback(delta);

    

    ProcessBufferedJump(delta);
  }

  private void ProcessJumpAndGravity(float delta)
  {
    if (player.IsOnFloor())
      player.JumpsRemaining = MaxJumps;

    if (player.IsOnFloor() && player.JumpBufferTimer > 0)
    {
      player.Velocity = new Vector3(player.Velocity.X, JumpVelocity, player.Velocity.Z);
      player.JumpBufferTimer = 0;
      player.JumpsRemaining--;

      // Spawn the same sprite used for wall hops near the player's feet, oriented up (non-billboard).
      var spawnPos = player.GlobalPosition + Vector3.Down * 0.6f;
      SpriteSheetFx.Spawn(
        player,
        sheetPath: "res://assets/sprites/effects/impact/impact_2_dust_48x48.png",
        frameW: 48,
        frameH: 48,
        position: spawnPos,
        surfaceNormal: Vector3.Up,
        pixelSize: 0.06f,
        fps: 18f,
        loop: false,
        billboard: false,
        normalOffset: 0.08f,
        randomRoll: true,
        doubleSided: true,
        depthTest: true
      );
    }

    if (!player.IsOnFloor())
      player.Velocity -= new Vector3(0, Gravity * delta, 0);
  }

  private void ProcessGroundMovement(Vector3 inputDirection, float delta)
  {
    if (Input.IsActionJustPressed("ui_accept"))
    {
      player.IsSliding = false;
      ProcessStandardGroundMovement(inputDirection, delta);
    }
    else if (Input.IsActionPressed("dash"))
    {
      ProcessSlide(delta);
    }
    else
    {
      if (player.IsSliding)
        player.IsSliding = false;

      ProcessStandardGroundMovement(inputDirection, delta);
    }
  }

  private void ProcessStandardGroundMovement(Vector3 inputDirection, float delta)
  {
    if (inputDirection != Vector3.Zero)
    {
      Vector3 currentHorizontal = new Vector3(player.Velocity.X, 0, player.Velocity.Z);
      if (currentHorizontal.Length() < 0.1f)
      {
        player.Velocity = new Vector3(
            inputDirection.X * Speed * InitialBoostFactor,
            player.Velocity.Y,
            inputDirection.Z * Speed * InitialBoostFactor
        );
      }
      else
      {
        player.Velocity = new Vector3(
            Mathf.MoveToward(player.Velocity.X, inputDirection.X * Speed, GroundAccel * delta),
            player.Velocity.Y,
            Mathf.MoveToward(player.Velocity.Z, inputDirection.Z * Speed, GroundAccel * delta)
        );
      }
      // player.AnimPlayer.Play("move");
    }
    else
    {
      player.Velocity = new Vector3(
          Mathf.MoveToward(player.Velocity.X, 0, GroundDecel * delta),
          player.Velocity.Y,
          Mathf.MoveToward(player.Velocity.Z, 0, GroundDecel * delta)
      );
      // player.AnimPlayer.Play("idle");
    }

    // Robust clamp: cap world horizontal speed while grounded.
    Vector3 hv = new Vector3(player.Velocity.X, 0, player.Velocity.Z);
    float hvLen = hv.Length();
    if (hvLen > Speed && hvLen > 0.0001f)
    {
      hv = hv / hvLen * Speed;
      player.Velocity = new Vector3(hv.X, player.Velocity.Y, hv.Z);
    }
  }

  private void ProcessSlide(float delta)
  {
    if (!player.IsSliding)
    {
      player.IsSliding = true;
      // player.AnimPlayer.Play("slide");
    }

    Vector3 floorNormal = player.GetFloorNormal();
    Vector3 gravityVector = Vector3.Down;
    Vector3 naturalDownhill = (gravityVector - floorNormal * gravityVector.Dot(floorNormal)).Normalized();
    float slopeAngle = Mathf.Acos(floorNormal.Dot(Vector3.Up));
    float gravityAccel = Gravity * Mathf.Sin(slopeAngle);

    player.Velocity += naturalDownhill * gravityAccel * delta;
    player.Velocity = new Vector3(
        Mathf.MoveToward(player.Velocity.X, 0, SlideFrictionCoefficient * delta),
        player.Velocity.Y,
        Mathf.MoveToward(player.Velocity.Z, 0, SlideFrictionCoefficient * delta)
    );
  }

  

  private void ProcessAirMovement(Vector3 inputDirection, float delta)
  {
    float currentSpeed = new Vector2(player.Velocity.X, player.Velocity.Z).Length();
    Vector3 newHorizontalVel = new Vector3(player.Velocity.X, 0, player.Velocity.Z);

    // Accelerate horizontally toward the target velocity.
    if (inputDirection != Vector3.Zero)
    {
      newHorizontalVel = new Vector3(
          Mathf.MoveToward(player.Velocity.X, inputDirection.X * Math.Max(Speed, currentSpeed), AirAcceleration * delta),
          0,
          Mathf.MoveToward(player.Velocity.Z, inputDirection.Z * Math.Max(Speed, currentSpeed), AirAcceleration * delta)
      );
    }

    if (Input.IsActionJustPressed("dash") && player.GetInputDirection().Length() > 0)
    {
      var newVelocity = player.GetInputDirection() * DashSpeed;
      newVelocity.Y = player.Velocity.Y;
      player.Velocity = newVelocity;
      newHorizontalVel = new Vector3(player.Velocity.X, 0, player.Velocity.Z);

      // Spawn an air-dash dust sprite effect near the player's feet.
      // Uses the 2D sheet assets/sprites/effects/dust/dust_28x12.png via DashDustSprite.
      var spawnPos = player.GlobalPosition + Vector3.Down * 0.6f;
      // Use generic SpriteSheetFx: dust_28x12, billboarded and large.
      SpriteSheetFx.Spawn(
        player,
        sheetPath: "res://assets/sprites/effects/dust/dust_28x12.png",
        frameW: 28,
        frameH: 12,
        position: spawnPos,
        surfaceNormal: null,
        pixelSize: 0.10f,
        fps: 18f,
        loop: false,
        billboard: true,
        normalOffset: 0.06f,
        randomRoll: true,
        doubleSided: true,
        depthTest: true
      );
    }

    // Apply air friction (drag) to the horizontal velocity.
    newHorizontalVel *= 1 - AIR_FRICTION * delta;

    // Update the player's velocity while preserving the vertical component.
    player.Velocity = new Vector3(newHorizontalVel.X, player.Velocity.Y, newHorizontalVel.Z);
  }

  private void ProcessBufferedJump(float delta)
  {
    if (!player.IsOnFloor() && player.JumpBufferTimer > 0)
    {
      bool wallFound = false;
      for (int i = 0; i < player.GetSlideCollisionCount(); i++)
      {
        KinematicCollision3D collision = player.GetSlideCollision(i);
        Vector3 normal = collision.GetNormal();
        if (Mathf.Abs(normal.Dot(Vector3.Up)) < WallHopMinNormalY)
        {
          player.Velocity = player.Velocity.Bounce(normal) * WallHopBoost;
          player.Velocity += new Vector3(0, WallHopUpwardBoost, 0);
          player.JumpBufferTimer = 0;
          // Spawn a wall impact sprite at the contact point, oriented to the wall normal.
          Vector3 hitPos = collision.GetPosition();
          SpriteSheetFx.Spawn(
            player,
            sheetPath: "res://assets/sprites/effects/impact/impact_2_dust_48x48.png",
            frameW: 48,
            frameH: 48,
            position: hitPos,
            surfaceNormal: normal,
            pixelSize: 0.06f,
            fps: 18f,
            loop: false,
            billboard: false,
            normalOffset: 0.08f,
            randomRoll: true,
            doubleSided: true,
            depthTest: true
          );
          wallFound = true;
          break;
        }
      }
      if (!wallFound && player.JumpsRemaining > 0)
      {
        player.Velocity = new Vector3(player.Velocity.X, JumpVelocity, player.Velocity.Z);
        player.JumpBufferTimer = 0;
        player.JumpsRemaining--;

        // Air jump: spawn the same wall hop sprite near the player's feet, oriented up (non-billboard).
        var spawnPos = player.GlobalPosition + Vector3.Down * 0.6f;
        SpriteSheetFx.Spawn(
          player,
          sheetPath: "res://assets/sprites/effects/impact/impact_2_dust_48x48.png",
          frameW: 48,
          frameH: 48,
          position: spawnPos,
          surfaceNormal: Vector3.Up,
          pixelSize: 0.06f,
          fps: 18f,
          loop: false,
          billboard: false,
          normalOffset: 0.08f,
          randomRoll: true,
          doubleSided: true,
          depthTest: true
        );
      }
    }
    player.JumpBufferTimer = Mathf.Max(player.JumpBufferTimer - delta, 0);
  }
}
