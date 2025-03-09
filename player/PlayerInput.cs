// PlayerInput.cs
using Godot;
using System;

public class PlayerInput
{
  private Player player;

  public PlayerInput(Player player)
  {
    this.player = player;
    SetupInput();
  }

  private void SetupInput()
  {
    // Configure input actions if they don't already exist.
    if (!InputMap.HasAction("dash"))
    {
      InputMap.AddAction("dash");
      var ev = new InputEventKey { Keycode = Key.Shift };
      InputMap.ActionAddEvent("dash", ev);
    }
    if (!InputMap.HasAction("interact"))
    {
      InputMap.AddAction("interact");
      var ev = new InputEventKey { Keycode = Key.E };
      InputMap.ActionAddEvent("interact", ev);
    }
  }

  public void HandleInput(InputEvent @event)
  {
    // Handle camera rotation and shooting.
    if (@event is InputEventMouseMotion mouseMotion)
    {
      player.RotateY(-mouseMotion.Relative.X * 0.005f);
      player.Camera.RotateX(-mouseMotion.Relative.Y * 0.005f);
      // Clamp the camera's rotation.
      player.Camera.Rotation = new Vector3(Mathf.Clamp(player.Camera.Rotation.X, -Mathf.Pi / 2, Mathf.Pi / 2),
                                            player.Camera.Rotation.Y,
                                            player.Camera.Rotation.Z);
    }

    if (Input.IsActionJustPressed("shoot"))
    {
      GD.Print("Shooting");
      player.CurrentWeapon?.OnPress();
    }
    else if (Input.IsActionJustReleased("shoot"))
    {
      player.CurrentWeapon?.OnRelease();
    }

    if (Input.IsActionJustPressed("dash"))
    {
      var newVelocity = player.GetInputDirection() * Player.DASH_SPEED;
      newVelocity.Y = player.Velocity.Y;
      player.Velocity = newVelocity;
    }

    // Jump buffer logic.
    if (@event.IsActionPressed("ui_accept"))
    {
      player.JumpBufferTimer = Player.JUMP_BUFFER_TIME;
    }

    if (@event is InputEventKey keyEvent && !keyEvent.Echo && Input.IsActionJustPressed("interact"))
    {
      GD.Print("Interacting with: ", player.InteractionManager.DetectInteractable());
      player.InteractionManager.ProcessInteraction();
    }
  }
}
