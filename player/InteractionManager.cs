using Godot;
using System;
using System.Collections.Generic;

public partial class InteractionManager : Node
{
  [Export] public float InteractRadius = 2.0f;
  [Export] public int MaxResults = 32;
  [Export] public NodePath PlayerPath;
  [Export] public NodePath CameraPath;

  private Player _player;
  private Camera3D _camera;
  private SphereShape3D _interactSphere;

  private readonly Dictionary<string, OptionEntry> _activeOptions = new(StringComparer.OrdinalIgnoreCase);
  private readonly List<InteractableContext> _contextBuffer = new();
  private readonly List<OptionEntry> _sortedOptions = new();
  private readonly List<string> _currentLines = new();

  public override void _Ready()
  {
    _player = GetNode<Player>(PlayerPath);
    _camera = GetNode<Camera3D>(CameraPath);

    // Pre-create the query shape to avoid per-frame allocations and ensure it's configured.
    _interactSphere = new SphereShape3D { Radius = MathF.Max(InteractRadius, 0f) };
  }

  public override void _Input(InputEvent @event)
  {
    if (_activeOptions.Count == 0)
      return;

    foreach (OptionEntry entry in _activeOptions.Values)
    {
      string actionName = entry.Option.ActionName;
      if (string.IsNullOrEmpty(actionName))
        continue;

      if (!@event.IsActionPressed(actionName))
        continue;

      if (@event is InputEventKey keyEvent && keyEvent.Echo)
        continue;

      ExecuteOption(actionName, entry);
      GetViewport()?.SetInputAsHandled();
      break;
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    DetectInteractables();
  }

  private void DetectInteractables()
  {
    if (_player == null)
    {
      ClearInteractions();
      return;
    }

    // Guard against invalid radius which can produce a null Jolt shape.
    if (InteractRadius <= 0f)
    {
      ClearInteractions();
      return;
    }

    _contextBuffer.Clear();

    Vector3 origin = _player.GlobalPosition;
    // Keep the sphere radius in sync with the export value.
    if (!Mathf.IsEqualApprox(_interactSphere.Radius, InteractRadius))
      _interactSphere.Radius = InteractRadius;

    PhysicsShapeQueryParameters3D query = new PhysicsShapeQueryParameters3D
    {
      Transform = new Transform3D(Basis.Identity, origin),
      Shape = _interactSphere,
      CollideWithBodies = true,
      CollideWithAreas = true
    };

    World3D world3d = _player.GetWorld3D();
    if (world3d == null)
    {
      ClearInteractions();
      return;
    }

    PhysicsDirectSpaceState3D spaceState = world3d.DirectSpaceState;
    // Exclude the player from results to avoid self-hits.
    var exclude = new Godot.Collections.Array<Rid>();
    exclude.Add(_player.GetRid());
    query.Exclude = exclude;
    var results = (Godot.Collections.Array<Godot.Collections.Dictionary>)spaceState.IntersectShape(query, MaxResults);

    foreach (Godot.Collections.Dictionary result in results)
    {
      Node colliderNode = result["collider"].As<Node>();
      if (colliderNode is not Node3D collider)
        continue;

      if (collider is not IInteractable interactable)
        continue;

      Vector3 samplePosition = collider.GlobalPosition;
      if (result.TryGetValue("point", out Variant pointVariant) && pointVariant.VariantType == Variant.Type.Vector3)
        samplePosition = (Vector3)pointVariant;

      float distance = origin.DistanceTo(samplePosition);
      _contextBuffer.Add(new InteractableContext(interactable, distance));
    }

    ApplyContexts(_contextBuffer);
  }

  private void ApplyContexts(List<InteractableContext> contexts)
  {
    _activeOptions.Clear();

    foreach (InteractableContext context in contexts)
    {
      if (context.Interactable is GodotObject godotObject && !GodotObject.IsInstanceValid(godotObject))
        continue;

      IReadOnlyList<InteractionOption> options = context.Interactable.GetInteractionOptions();
      if (options == null)
        continue;

      foreach (InteractionOption option in options)
      {
        if (string.IsNullOrEmpty(option.ActionName))
          continue;

        InteractionOption resolvedOption = option;
        if (string.IsNullOrEmpty(option.Description))
        {
          string fallback = context.Interactable.GetInteractionText() ?? string.Empty;
          resolvedOption = new InteractionOption(option.ActionName, fallback);
        }

        if (_activeOptions.TryGetValue(resolvedOption.ActionName, out OptionEntry existing))
        {
          if (context.Distance < existing.Distance)
          {
            _activeOptions[resolvedOption.ActionName] = new OptionEntry(resolvedOption, context.Interactable, context.Distance);
          }
        }
        else
        {
          _activeOptions[resolvedOption.ActionName] = new OptionEntry(resolvedOption, context.Interactable, context.Distance);
        }
      }
    }

    if (_activeOptions.Count == 0)
    {
      UpdatePrompt(Array.Empty<string>());
      return;
    }

    _sortedOptions.Clear();
    _sortedOptions.AddRange(_activeOptions.Values);
    _sortedOptions.Sort((a, b) => a.Distance.CompareTo(b.Distance));

    List<string> lines = BuildPrompt(_sortedOptions);
    UpdatePrompt(lines);
  }

  private void ExecuteOption(string actionName, OptionEntry entry)
  {
    if (entry.Interactable is GodotObject godotObject && !GodotObject.IsInstanceValid(godotObject))
    {
      DetectInteractables();
      return;
    }

    entry.Interactable.OnInteract(actionName);
    DetectInteractables();
  }

  private void ClearInteractions()
  {
    if (_activeOptions.Count == 0 && _currentLines.Count == 0)
      return;

    _activeOptions.Clear();
    _sortedOptions.Clear();
    UpdatePrompt(Array.Empty<string>());
  }

  private void UpdatePrompt(IReadOnlyList<string> lines)
  {
    bool hasLines = lines != null && lines.Count > 0;
    if (hasLines && LinesEqual(_currentLines, lines))
    {
      if (GameUI.Instance != null && _currentLines.Count > 0)
        GameUI.Instance.ShowInteractionLines(_currentLines);
      return;
    }

    _currentLines.Clear();
    if (hasLines)
      _currentLines.AddRange(lines);

    if (GameUI.Instance == null)
      return;

    if (_currentLines.Count == 0)
      GameUI.Instance.HideInteractionText();
    else
      GameUI.Instance.ShowInteractionLines(_currentLines);
  }

  private static List<string> BuildPrompt(IReadOnlyList<OptionEntry> entries)
  {
    if (entries == null || entries.Count == 0)
      return new List<string>();

    List<string> lines = new(entries.Count);
    for (int i = 0; i < entries.Count; i++)
    {
      lines.Add(FormatOption(entries[i].Option));
    }

    return lines;
  }

  private static string FormatOption(InteractionOption option)
  {
    string keyLabel = GetPrimaryInputLabel(option.ActionName);
    if (!string.IsNullOrEmpty(keyLabel))
      return $"[{keyLabel}] {option.Description}";

    return option.Description;
  }

  private static string GetPrimaryInputLabel(string actionName)
  {
    if (string.IsNullOrEmpty(actionName) || !InputMap.HasAction(actionName))
      return string.Empty;

    Godot.Collections.Array<InputEvent> events = InputMap.ActionGetEvents(actionName);

    foreach (InputEvent inputEvent in events)
    {
      if (inputEvent is InputEventKey keyEvent)
      {
        Key keycode = keyEvent.PhysicalKeycode != 0
          ? (Key)keyEvent.PhysicalKeycode
          : keyEvent.Keycode;

        if (keycode != Key.None)
        {
          string label = OS.GetKeycodeString(keycode);
          if (!string.IsNullOrEmpty(label))
            return label.ToUpperInvariant();
        }
      }
      else if (inputEvent is InputEventMouseButton mouseEvent)
      {
        return mouseEvent.ButtonIndex switch
        {
          MouseButton.Left => "LMB",
          MouseButton.Right => "RMB",
          MouseButton.Middle => "MMB",
          _ => $"Mouse{(int)mouseEvent.ButtonIndex}"
        };
      }
      else if (inputEvent is InputEventJoypadButton joyEvent)
      {
        return $"Joy {joyEvent.ButtonIndex}";
      }
    }

    return string.Empty;
  }

  private readonly struct InteractableContext
  {
    public InteractableContext(IInteractable interactable, float distance)
    {
      Interactable = interactable;
      Distance = distance;
    }

    public IInteractable Interactable { get; }
    public float Distance { get; }
  }

  private readonly struct OptionEntry
  {
    public OptionEntry(InteractionOption option, IInteractable interactable, float distance)
    {
      Option = option;
      Interactable = interactable;
      Distance = distance;
    }

    public InteractionOption Option { get; }
    public IInteractable Interactable { get; }
    public float Distance { get; }
  }

  private static bool LinesEqual(List<string> current, IReadOnlyList<string> incoming)
  {
    if (current.Count != incoming.Count)
      return false;

    for (int i = 0; i < current.Count; i++)
    {
      if (!string.Equals(current[i], incoming[i], StringComparison.Ordinal))
        return false;
    }

    return true;
  }
}
