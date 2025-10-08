using Godot;

public partial class Crosshair : Control
{
  private const float CrosshairGap = 6f;
  private const float CrosshairSegmentLength = 9f;
  private const float CrosshairThickness = 2f;
  private readonly Color _crosshairBaseColor = new Color(1f, 1f, 1f);

  private const float HitMarkerDuration = 0.18f;
  private const float HitMarkerGap = 6f;
  private const float HitMarkerLength = 10f;
  private const float HitMarkerThickness = 3f;
  private const float HitMarkerOutlineThickness = 6f;
  private const float HitMarkerOutlineExtension = 2f;
  private readonly Color _hitMarkerColor = new Color(1f, 0.35f, 0.35f);
  private float _hitMarkerTimer = 0f;
  private Callable _damageDealtCallable;
  private bool _damageSignalConnected;

  public override void _Ready()
  {
    _damageDealtCallable = new Callable(this, nameof(OnDamageDealt));
    // Schedule a redraw of the control.
    SetProcess(true);
    QueueRedraw();
    TryConnectDamageSignal();
  }

  public override void _Process(double delta)
  {
    TryConnectDamageSignal();

    if (_hitMarkerTimer > 0f)
    {
      _hitMarkerTimer = Mathf.Max(0f, _hitMarkerTimer - (float)delta);
      if (_hitMarkerTimer <= 0f)
        QueueRedraw();
    }

    QueueRedraw();
  }

  public override void _Draw()
  {
    // Draw centered; when not using full-frame overlay, cancel UI offset so the crosshair remains fixed.
    bool fullFrame = GameUi.Instance != null && GameUi.Instance.UseFullFrameShake;
    Vector2 shake = (!fullFrame && GameUi.Instance != null) ? GameUi.Instance.GetScreenShakeOffset() : Vector2.Zero;
    Vector2 center = (Size / 2) - shake;

    // Define crosshair properties.
    float hitFade = HitMarkerDuration > 0f ? Mathf.Clamp(_hitMarkerTimer / HitMarkerDuration, 0f, 1f) : 0f;
    Color crosshairColor = hitFade > 0f ? new Color(1f, 0.7f, 0.7f) : _crosshairBaseColor;

    Vector2[] crosshairDirs =
    {
      new Vector2(-1f, 0f),
      new Vector2(1f, 0f),
      new Vector2(0f, -1f),
      new Vector2(0f, 1f)
    };

    foreach (var dir in crosshairDirs)
    {
      Vector2 segmentStart = center + dir * (CrosshairGap + CrosshairSegmentLength);
      Vector2 segmentEnd = center + dir * CrosshairGap;
      DrawLine(segmentStart, segmentEnd, crosshairColor, CrosshairThickness);
    }

    if (hitFade > 0f)
    {
      Color hitColor = new Color(_hitMarkerColor.R, _hitMarkerColor.G, _hitMarkerColor.B, hitFade);
      Color hitOutlineColor = new Color(1f, 1f, 1f, hitFade);
      float crosshairExtent = CrosshairGap + CrosshairSegmentLength;
      float inner = crosshairExtent + HitMarkerGap;
      float outer = inner + HitMarkerLength;
      float outlineInner = inner - HitMarkerOutlineExtension;
      float outlineOuter = outer + HitMarkerOutlineExtension;

      Vector2[] markerDirs =
      {
        new Vector2(-1f, -1f).Normalized(),
        new Vector2(1f, -1f).Normalized(),
        new Vector2(-1f, 1f).Normalized(),
        new Vector2(1f, 1f).Normalized()
      };

      foreach (var dir in markerDirs)
      {
        Vector2 outlineStart = center + dir * outlineInner;
        Vector2 outlineEnd = center + dir * outlineOuter;
        Vector2 fillStart = center + dir * inner;
        Vector2 fillEnd = center + dir * outer;

        DrawLine(outlineStart, outlineEnd, hitOutlineColor, HitMarkerOutlineThickness);
        DrawLine(fillStart, fillEnd, hitColor, HitMarkerThickness);
      }
    }
  }

  public override void _ExitTree()
  {
    DisconnectDamageSignal();
  }

  private void OnDamageDealt(Node3D target, float amount, Vector3 impulse)
  {
    if (!IsInstanceValid(target)) return;
    if (amount <= 0f) return;
    if (target is not Enemy) return;

    _hitMarkerTimer = HitMarkerDuration;
    QueueRedraw();
  }

  private void TryConnectDamageSignal()
  {
    if (_damageSignalConnected) return;
    var global = GlobalEvents.Instance;
    if (global == null) return;
    if (!global.IsConnected(nameof(GlobalEvents.DamageDealt), _damageDealtCallable))
    {
      global.Connect(nameof(GlobalEvents.DamageDealt), _damageDealtCallable);
    }
    _damageSignalConnected = true;
  }

  private void DisconnectDamageSignal()
  {
    if (!_damageSignalConnected) return;
    var global = GlobalEvents.Instance;
    if (global != null && global.IsConnected(nameof(GlobalEvents.DamageDealt), _damageDealtCallable))
    {
      global.Disconnect(nameof(GlobalEvents.DamageDealt), _damageDealtCallable);
    }
    _damageSignalConnected = false;
  }
}
