using Godot;

public enum WeaponStatKind
{
  Damage,
  AccuracyPct,     // 0..100
  BulletSpeed,
  ShotsPerSecond,  // 1 / fire interval
  ReloadSeconds,   // lower is better
  Magazine,
}

public static class WeaponStatColorConfig
{
  // Clamp ranges. Tweak these to fit your game balance.
  // Values outside will be clamped before sampling colours.
  public static float DamageMin = 2f;
  public static float DamageMax = 100f;

  public static float AccuracyPctMin = 50f;   // show white-ish under 50%
  public static float AccuracyPctMax = 100f;  // cap at 100%

  public static float BulletSpeedMin = 20f;
  public static float BulletSpeedMax = 200f;

  public static float ShotsPerSecondMin = 1f;
  public static float ShotsPerSecondMax = 20f;

  public static float ReloadSecondsMin = 0.5f;  // best
  public static float ReloadSecondsMax = 6.0f;  // worst

  public static int MagazineMin = 5;
  public static int MagazineMax = 100;

  // HSV gradient: red (low) -> yellow (mid) -> green (high) with full saturation/value
  public static float HueStart = 0.0f;          // red
  public static float HueEnd = 1f / 3f;         // green (120°)
  public static float Saturation = 1.0f;        // super saturated
  public static float Value = 1.0f;             // full brightness

  public static Color GetColour(WeaponStatKind kind, float value)
  {
    float t = kind switch
    {
      WeaponStatKind.Damage => Normalize(value, DamageMin, DamageMax, higherIsBetter: true),
      WeaponStatKind.AccuracyPct => Normalize(value, AccuracyPctMin, AccuracyPctMax, higherIsBetter: true),
      WeaponStatKind.BulletSpeed => Normalize(value, BulletSpeedMin, BulletSpeedMax, higherIsBetter: true),
      WeaponStatKind.ShotsPerSecond => Normalize(value, ShotsPerSecondMin, ShotsPerSecondMax, higherIsBetter: true),
      WeaponStatKind.ReloadSeconds => Normalize(value, ReloadSecondsMin, ReloadSecondsMax, higherIsBetter: false),
      WeaponStatKind.Magazine => Normalize(value, MagazineMin, MagazineMax, higherIsBetter: true),
      _ => 0.0f,
    };
    float hue = Mathf.Lerp(HueStart, HueEnd, t);
    return Color.FromHsv(hue, Mathf.Clamp(Saturation, 0f, 1f), Mathf.Clamp(Value, 0f, 1f), 1f);
  }

  private static float Normalize(float v, float min, float max, bool higherIsBetter)
  {
    if (max <= min) return 0f;
    float t = Mathf.Clamp((v - min) / (max - min), 0f, 1f);
    return higherIsBetter ? t : (1f - t);
  }

  // Kept for compatibility if needed by other callers in the future
  private static Color Lerp(in Color a, in Color b, float t) => a.Lerp(b, t);
}
