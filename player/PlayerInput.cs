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
    if (Input.IsActionJustPressed("shoot"))
    {
      GD.Print("Shooting");
      player.CurrentWeapon?.OnPress();
    }
    else if (Input.IsActionJustReleased("shoot"))
    {
      player.CurrentWeapon?.OnRelease();
    }
  }
}
