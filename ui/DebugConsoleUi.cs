#nullable enable

using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Lightweight in-game console for spawning enemies and running quick test commands.
/// </summary>
public partial class DebugConsoleUi : CanvasLayer
{
  private const string EnemyScenePath = "res://enemy.tscn";

  public static DebugConsoleUi? Instance { get; private set; }

  private PanelContainer _panel = default!;
  private RichTextLabel _log = default!;
  private LineEdit _input = default!;
  private ScrollContainer _scroll = default!;
  private bool _isOpen;
  private bool _resumeOnClose;
  private bool _openedMenu;
  private Input.MouseModeEnum _previousMouseMode = Input.MouseModeEnum.Visible;
  private PackedScene? _enemyScene;
  private readonly List<string> _history = new();
  private int _historyIndex = -1;
  private readonly Dictionary<string, Action<string[]>> _commands = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, string> _commandHelp = new(StringComparer.OrdinalIgnoreCase);
  private static readonly char[] CommandSeparators = { ' ', '\t' };

  public static bool IsCapturingInput => Instance != null && Instance._isOpen;

  public override void _Ready()
  {
    Instance = this;
    ProcessMode = ProcessModeEnum.Always;
    Layer = 175;
    BuildUi();
    Visible = false;
    SetProcess(true);
    SetProcessUnhandledInput(true);
    EnsureInputAction();
    RegisterDefaultCommands();
  }

  public override void _ExitTree()
  {
    if (Instance == this)
      Instance = null;
  }

  public override void _Process(double delta)
  {
    base._Process(delta);

    if (Input.IsActionJustPressed("toggle_console"))
    {
      Toggle();
      GetViewport()?.SetInputAsHandled();
    }
    else if (_isOpen && Input.IsActionJustPressed("ui_cancel"))
    {
      Close();
      GetViewport()?.SetInputAsHandled();
    }
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    if (!_isOpen)
      return;

    if (@event is InputEventKey key && key.Pressed && !key.Echo)
    {
      if (key.Keycode == Key.Escape)
      {
        Close();
        GetViewport()?.SetInputAsHandled();
      }
    }
  }

  private void EnsureInputAction()
  {
    const string action = "toggle_console";
    if (!InputMap.HasAction(action))
      InputMap.AddAction(action);

    EnsureDefaultKeyBinding(action, Key.Slash);
    EnsureDefaultKeyBinding(action, Key.F1);
  }

  private static void EnsureDefaultKeyBinding(string action, Key key)
  {
    foreach (var existing in InputMap.ActionGetEvents(action))
    {
      if (existing is InputEventKey keyEvent && keyEvent.Keycode == key)
        return;
    }

    InputMap.ActionAddEvent(action, new InputEventKey { Keycode = key });
  }

  private void BuildUi()
  {
    _panel = new PanelContainer();
    _panel.Name = "DebugConsolePanel";
    _panel.AnchorLeft = 0f;
    _panel.AnchorTop = 0f;
    _panel.AnchorRight = 1f;
    _panel.AnchorBottom = 0f;
    _panel.OffsetLeft = 16f;
    _panel.OffsetTop = 16f;
    _panel.OffsetRight = -16f;
    _panel.OffsetBottom = 280f;
    _panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    _panel.SizeFlagsVertical = 0;
    _panel.MouseFilter = Control.MouseFilterEnum.Stop;

    var margin = new MarginContainer();
    margin.AddThemeConstantOverride("margin_left", 12);
    margin.AddThemeConstantOverride("margin_right", 12);
    margin.AddThemeConstantOverride("margin_top", 12);
    margin.AddThemeConstantOverride("margin_bottom", 12);
    margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    margin.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

    var layout = new VBoxContainer();
    layout.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    layout.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
    layout.AddThemeConstantOverride("separation", 8);

    _scroll = new ScrollContainer();
    _scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    _scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
    _scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

    _log = new RichTextLabel();
    _log.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    _log.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
    _log.BbcodeEnabled = false;
    _log.ScrollFollowing = true;
    _log.AutowrapMode = TextServer.AutowrapMode.Word;
    _log.Text = "Debug console ready. Type 'help' for commands.";

    _scroll.AddChild(_log);

    _input = new LineEdit();
    _input.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    _input.PlaceholderText = "spawn_enemies 3";
    _input.TextSubmitted += OnCommandSubmitted;
    _input.GuiInput += OnInputGuiInput;

    layout.AddChild(_scroll);
    layout.AddChild(_input);
    margin.AddChild(layout);
    _panel.AddChild(margin);
    AddChild(_panel);
  }

  private void RegisterDefaultCommands()
  {
    RegisterCommand("help", args => ShowHelp(), "help - list available commands");
    RegisterCommand("clear", args => ClearOutput(), "clear - erase console output");
    RegisterCommand("spawn_enemy", SpawnEnemiesCommand, "spawn_enemy [count] [radius] - spawn enemies near the player");
    RegisterCommand("spawn_enemies", SpawnEnemiesCommand, "spawn_enemies [count] [radius] - spawn enemies near the player");
    RegisterCommand("kill_enemies", args => KillEnemies(), "kill_enemies - remove all active enemies");
  }

  private void RegisterCommand(string name, Action<string[]> handler, string help)
  {
    _commands[name] = handler;
    _commandHelp[name] = help;
  }

  private void Toggle()
  {
    if (_isOpen)
      Close();
    else
      Open();
  }

  private void Open()
  {
    if (_isOpen)
      return;

    _isOpen = true;
    _openedMenu = false;
    var tree = GetTree();
    bool menuOpen = GlobalEvents.Instance != null && GlobalEvents.Instance.MenuOpen;
    _resumeOnClose = tree != null && !tree.Paused && !menuOpen;

    if (!menuOpen && GlobalEvents.Instance != null)
    {
      GlobalEvents.Instance.SetMenuOpen(true);
      _openedMenu = true;
    }
    else if (tree != null && !tree.Paused)
    {
      tree.Paused = true;
    }

    Visible = true;
    _panel.Visible = true;
    _previousMouseMode = Input.MouseMode;
    Input.MouseMode = Input.MouseModeEnum.Visible;
    _input.Editable = true;
    _input.GrabFocus();
    _input.CaretColumn = _input.Text.Length;
  }

  private void Close()
  {
    if (!_isOpen)
      return;

    _isOpen = false;
    Visible = false;
    _panel.Visible = false;
    var tree = GetTree();
    bool menuStillOpen = GlobalEvents.Instance != null && GlobalEvents.Instance.MenuOpen;
    if (_openedMenu && GlobalEvents.Instance != null)
    {
      GlobalEvents.Instance.SetMenuOpen(false);
    }
    else if (_resumeOnClose && tree != null && tree.Paused && !menuStillOpen)
    {
      tree.Paused = false;
    }

    _resumeOnClose = false;
    _openedMenu = false;
    if (IsInstanceValid(_input))
    {
      _input.ReleaseFocus();
      _input.Text = string.Empty;
    }
    Input.MouseMode = _previousMouseMode;
  }

  private void OnCommandSubmitted(string text)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      _input.Text = string.Empty;
      _historyIndex = -1;
      return;
    }

    AppendLine($"> {text}");
    _history.Add(text);
    _historyIndex = -1;
    _input.Text = string.Empty;

    var tokens = SplitArguments(text);
    if (tokens.Length == 0)
      return;

    var command = tokens[0];
    var args = tokens.Skip(1).ToArray();

    if (_commands.TryGetValue(command, out var handler))
    {
      try
      {
        handler(args);
      }
      catch (Exception ex)
      {
        AppendLine($"Command '{command}' failed: {ex.Message}");
        GD.PrintErr($"DebugConsole command '{command}' threw: {ex}");
      }
    }
    else
    {
      AppendLine($"Unknown command '{command}'. Type 'help' for a list.");
    }
  }

  private void OnInputGuiInput(InputEvent @event)
  {
    if (@event is not InputEventKey key || !key.Pressed || key.Echo)
      return;

    switch (key.Keycode)
    {
      case Key.Tab:
        HandleTabCompletion();
        GetViewport()?.SetInputAsHandled();
        return;
      case Key.Up:
        if (_history.Count == 0)
          return;
        if (_historyIndex == -1)
          _historyIndex = _history.Count;
        _historyIndex = Math.Max(0, _historyIndex - 1);
        ApplyHistoryEntry();
        GetViewport()?.SetInputAsHandled();
        break;
      case Key.Down:
        if (_history.Count == 0 || _historyIndex == -1)
          return;
        _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
        ApplyHistoryEntry();
        GetViewport()?.SetInputAsHandled();
        break;
    }
  }

  private void ApplyHistoryEntry()
  {
    if (_historyIndex >= 0 && _historyIndex < _history.Count)
      _input.Text = _history[_historyIndex];
    else
      _input.Text = string.Empty;

    _input.CaretColumn = _input.Text.Length;
  }

  private void HandleTabCompletion()
  {
    string text = _input.Text ?? string.Empty;
    int caret = _input.CaretColumn;
    if (caret < 0)
      caret = 0;
    if (caret > text.Length)
      caret = text.Length;

    int commandEnd = text.IndexOfAny(CommandSeparators);
    if (commandEnd == -1)
      commandEnd = text.Length;

    if (caret > commandEnd)
      return;

    string prefix = text.Substring(0, caret);
    if (prefix.Length > 0 && prefix.IndexOfAny(CommandSeparators) >= 0)
      return;

    var matches = _commands.Keys
      .Where(cmd => cmd.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
      .OrderBy(cmd => cmd, StringComparer.OrdinalIgnoreCase)
      .ToList();

    if (matches.Count == 0)
      return;

    string remainder = text.Substring(commandEnd);

    string completion = GetCompletion(prefix, matches);

    if (completion.Length <= prefix.Length)
    {
      if (matches.Count == 1)
      {
        string command = matches[0];
        if (remainder.Length == 0)
        {
          _input.Text = command + " ";
          _input.CaretColumn = _input.Text.Length;
        }
        else
        {
          _input.Text = command + remainder;
          _input.CaretColumn = command.Length;
        }
      }
      else
      {
        AppendLine("Matches: " + string.Join(", ", matches));
      }
      return;
    }

    if (matches.Count == 1 && remainder.Length == 0 && completion == matches[0])
    {
      _input.Text = completion + " ";
      _input.CaretColumn = _input.Text.Length;
    }
    else
    {
      _input.Text = completion + remainder;
      _input.CaretColumn = completion.Length;
    }
  }

  private static string GetCompletion(string prefix, List<string> matches)
  {
    if (matches.Count == 0)
      return prefix;
    if (matches.Count == 1)
      return matches[0];

    string candidate = matches[0];
    for (int i = 1; i < matches.Count; i++)
    {
      candidate = GetSharedPrefix(candidate, matches[i]);
      if (candidate.Length <= prefix.Length)
        break;
    }
    return candidate;
  }

  private static string GetSharedPrefix(string a, string b)
  {
    int limit = Math.Min(a.Length, b.Length);
    int i = 0;
    for (; i < limit; i++)
    {
      if (char.ToLowerInvariant(a[i]) != char.ToLowerInvariant(b[i]))
        break;
    }
    return a.Substring(0, i);
  }

  private void ShowHelp()
  {
    AppendLine("Commands:");
    foreach (var pair in _commandHelp.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
    {
      AppendLine($"  {pair.Value}");
    }
  }

  private void ClearOutput()
  {
    _log.Clear();
  }

  private void SpawnEnemiesCommand(string[] args)
  {
    int count = 1;
    if (args.Length >= 1 && !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
    {
      AppendLine($"Invalid enemy count '{args[0]}'.");
      return;
    }
    count = Math.Max(1, count);

    float radius = 6f;
    if (args.Length >= 2 && !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out radius))
    {
      AppendLine($"Invalid radius '{args[1]}'.");
      return;
    }
    radius = MathF.Max(0f, radius);

    var player = Player.Instance;
    if (player == null || !IsInstanceValid(player))
    {
      AppendLine("Cannot spawn: player not found.");
      return;
    }

    var parent = player.GetParent();
    if (parent == null || !IsInstanceValid(parent))
    {
      AppendLine("Cannot spawn: player parent scene unavailable.");
      return;
    }

    _enemyScene ??= GD.Load<PackedScene>(EnemyScenePath);
    if (_enemyScene == null)
    {
      AppendLine($"Cannot load enemy scene at {EnemyScenePath}.");
      return;
    }

    var rng = new RandomNumberGenerator();
    rng.Randomize();

    int spawned = 0;
    for (int i = 0; i < count; i++)
    {
      var instance = _enemyScene.Instantiate<Node3D>();
      if (instance == null)
        continue;

      Vector3 spawnOrigin = player.GlobalTransform.Origin;
      if (radius > 0.01f)
      {
        float angle = rng.RandfRange(0f, Mathf.Tau);
        float distance = radius * rng.Randf();
        spawnOrigin += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
      }
      spawnOrigin += new Vector3(0f, 0.5f, 0f);

      parent.AddChild(instance);
      instance.GlobalTransform = new Transform3D(Basis.Identity, spawnOrigin);
      spawned++;
    }

    AppendLine($"Spawned {spawned} enemy{(spawned == 1 ? string.Empty : "ies")} near the player.");
  }

  private void KillEnemies()
  {
    if (Enemy.ActiveEnemies.Count == 0)
    {
      AppendLine("No active enemies to remove.");
      return;
    }

    var enemies = Enemy.ActiveEnemies.Where(e => e != null && IsInstanceValid(e)).ToArray();
    foreach (var enemy in enemies)
      enemy.QueueFree();

    AppendLine($"Queued {enemies.Length} enemy{(enemies.Length == 1 ? string.Empty : "ies")} for removal.");
  }

  private static string[] SplitArguments(string text)
  {
    return text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
  }

  private void AppendLine(string text)
  {
    _log.AppendText(text + "\n");
  }
}
