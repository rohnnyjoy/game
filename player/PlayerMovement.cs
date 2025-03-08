// PlayerMovement.cs
using Godot;
using System;

public class PlayerMovement
{
  private Player player;

  public PlayerMovement(Player player)
  {
    this.player = player;
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

    // Apply extra upward boost in air.
    if (!player.IsOnFloor() && Input.IsActionPressed("upward_boost"))
    {
      player.Velocity += new Vector3(0, Player.EXTRA_UPWARD_BOOST * delta, 0);
    }

    player.PreSlideHorizontalVelocity = new Vector3(player.Velocity.X, 0, player.Velocity.Z);
    player.MoveAndSlide();

    ProcessBufferedJump(delta);
  }

  private void ProcessJumpAndGravity(float delta)
  {
    if (player.IsOnFloor())
      player.JumpsRemaining = Player.MAX_JUMPS;

    if (player.IsOnFloor() && player.JumpBufferTimer > 0)
    {
      player.Velocity = new Vector3(player.Velocity.X, Player.JUMP_VELOCITY, player.Velocity.Z);
      player.JumpBufferTimer = 0;
      player.JumpsRemaining--;
    }

    if (!player.IsOnFloor())
      player.Velocity -= new Vector3(0, Player.GRAVITY * delta, 0);
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
      {
        player.IsSliding = false;
      }
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
            inputDirection.X * Player.SPEED * Player.INITIAL_BOOST_FACTOR,
            player.Velocity.Y,
            inputDirection.Z * Player.SPEED * Player.INITIAL_BOOST_FACTOR
        );
      }
      else
      {
        player.Velocity = new Vector3(
            Mathf.MoveToward(player.Velocity.X, inputDirection.X * Player.SPEED, Player.GROUND_ACCEL * delta),
            player.Velocity.Y,
            Mathf.MoveToward(player.Velocity.Z, inputDirection.Z * Player.SPEED, Player.GROUND_ACCEL * delta)
        );
      }
      player.AnimPlayer.Play("move");
    }
    else
    {
      player.Velocity = new Vector3(
          Mathf.MoveToward(player.Velocity.X, 0, Player.GROUND_DECEL * delta),
          player.Velocity.Y,
          Mathf.MoveToward(player.Velocity.Z, 0, Player.GROUND_DECEL * delta)
      );
      player.AnimPlayer.Play("idle");
    }
  }

  private void ProcessSlide(float delta)
  {
    if (!player.IsSliding)
    {
      player.IsSliding = true;
      player.AnimPlayer.Play("slide");
    }

    Vector3 floorNormal = player.GetFloorNormal();
    Vector3 gravityVector = Vector3.Down;
    Vector3 naturalDownhill = (gravityVector - floorNormal * gravityVector.Dot(floorNormal)).Normalized();
    float slopeAngle = Mathf.Acos(floorNormal.Dot(Vector3.Up));
    float gravityAccel = Player.GRAVITY * Mathf.Sin(slopeAngle);

    player.Velocity += naturalDownhill * gravityAccel * delta;
    player.Velocity = new Vector3(
        Mathf.MoveToward(player.Velocity.X, 0, Player.SLIDE_FRICTION_COEFFICIENT * delta),
        player.Velocity.Y,
        Mathf.MoveToward(player.Velocity.Z, 0, Player.SLIDE_FRICTION_COEFFICIENT * delta)
    );
  }

  private void ProcessAirMovement(Vector3 inputDirection, float delta)
  {
    // This example assumes you have a helper manager for air movement.
    if (player.AirLurchManager == null)
      player.AirLurchManager = new AirLurchManager(new Vector2(player.Velocity.X, player.Velocity.Z));

    Vector2 currentVel = new Vector2(player.Velocity.X, player.Velocity.Z);
    Vector2 newVel = player.AirLurchManager.PerformLurch(currentVel, new Vector2(inputDirection.X, inputDirection.Z));
    player.Velocity = new Vector3(newVel.X, player.Velocity.Y, newVel.Y);
  }

  private void ProcessBufferedJump(float delta)
  {
    // Wall-hopping or mid-air jump logic.
    if (!player.IsOnFloor() && player.JumpBufferTimer > 0)
    {
      bool wallFound = false;
      for (int i = 0; i < player.GetSlideCollisionCount(); i++)
      {
        KinematicCollision3D collision = player.GetSlideCollision(i);
        Vector3 normal = collision.GetNormal();
        if (Mathf.Abs(normal.Dot(Vector3.Up)) < Player.WALL_HOP_MIN_NORMAL_Y)
        {
          player.Velocity = player.Velocity.Bounce(normal) * Player.WALL_HOP_BOOST;
          player.Velocity += new Vector3(0, Player.WALL_HOP_UPWARD_BOOST, 0);
          player.JumpBufferTimer = 0;
          wallFound = true;
          break;
        }
      }
      if (!wallFound && player.JumpsRemaining > 0)
      {
        player.Velocity = new Vector3(player.Velocity.X, Player.JUMP_VELOCITY, player.Velocity.Z);
        player.JumpBufferTimer = 0;
        player.JumpsRemaining--;
      }
    }
    player.JumpBufferTimer = Mathf.Max(player.JumpBufferTimer - delta, 0);
  }
}
