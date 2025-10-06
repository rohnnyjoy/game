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
    // Guard: When the inventory/menu is open we shouldn't process combat inputs.
    // Without this, left-click drags in the UI still trigger the global "shoot"
    // action, causing very high fire rates (e.g., Microgun) to flood updates
    // and stall frames during drag-and-drop. Blocking here keeps gameplay input
    // decoupled from UI interactions.
    if (GlobalEvents.Instance != null && GlobalEvents.Instance.MenuOpen)
      return;

    if (Input.IsActionJustPressed("shoot"))
    {
      player.CurrentWeapon?.OnPress();
    }
    else if (Input.IsActionJustReleased("shoot"))
    {
      player.CurrentWeapon?.OnRelease();
    }
  }
}
