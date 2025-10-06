using Godot;

public static class ImpactSound
{
  private static AudioStream _stream;
  private static RandomNumberGenerator _rng;

  public static void Prewarm()
  {
    if (_stream == null)
      _stream = GD.Load<AudioStream>("res://assets/sounds/impact.wav");
    if (_rng == null)
    {
      _rng = new RandomNumberGenerator();
      _rng.Randomize();
    }
  }

  public static void Play(Node context, Vector3 position, float volumeDb = -4.0f, float pitchJitter = 0.06f)
  {
    if (context == null || !GodotObject.IsInstanceValid(context)) return;
    var tree = context.GetTree();
    if (tree?.CurrentScene == null) return;

    if (_stream == null)
      _stream = GD.Load<AudioStream>("res://assets/sounds/impact.wav");
    if (_stream == null)
      return;
    if (_rng == null)
    {
      _rng = new RandomNumberGenerator();
      _rng.Randomize();
    }

    // If the stream is stereo, 3D players won't spatialize it properly.
    // As a fallback, play it in 2D so at least it's audible,
    // and suggest forcing mono import for true 3D positioning.
    if (_stream is AudioStreamWav wav && wav.Stereo)
    {
      var p2d = new AudioStreamPlayer
      {
        Autoplay = false,
        Bus = "Master",
        Stream = _stream,
        VolumeDb = volumeDb,
      };
      float j2 = (float)(_rng.Randf() * 2.0f - 1.0f);
      p2d.PitchScale = 1.0f + pitchJitter * j2;
      tree.CurrentScene.AddChild(p2d);
      // Fallback auto-free using stream length timer
      double len = _stream.GetLength();
      var t = tree.CreateTimer(Mathf.Max(0.05f, (float)len + 0.05f));
      t.Connect("timeout", Callable.From(() => p2d.QueueFree()));
      p2d.Play();
      return;
    }

    var p3d = new AudioStreamPlayer3D
    {
      Autoplay = false,
      Bus = "Master",
      Stream = _stream,
      VolumeDb = volumeDb,
    };
    float jitter = (float)(_rng.Randf() * 2.0f - 1.0f);
    p3d.PitchScale = 1.0f + pitchJitter * jitter;
    p3d.UnitSize = 2.0f; // narrower audible radius

    tree.CurrentScene.AddChild(p3d);
    p3d.GlobalPosition = position;
    // Auto-free using stream length timer
    double length = _stream.GetLength();
    var timer = tree.CreateTimer(Mathf.Max(0.05f, (float)length + 0.05f));
    timer.Connect("timeout", Callable.From(() => p3d.QueueFree()));
    p3d.Play();
  }
}
