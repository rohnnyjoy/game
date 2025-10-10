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
    // Also force-release the current weapon so sustained fire cannot continue
    // in the background while the UI is open (e.g., Microgun flood).
    if (GlobalEvents.Instance != null && GlobalEvents.Instance.MenuOpen)
    {
      player.CurrentWeapon?.OnRelease();
      return;
    }

    if (DebugConsoleUi.IsCapturingInput)
    {
      player.CurrentWeapon?.OnRelease();
      return;
    }

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
