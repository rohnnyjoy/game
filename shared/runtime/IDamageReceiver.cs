#nullable enable

namespace Shared.Runtime
{
  public interface IDamageReceiver
  {
    DamageResult ReceiveDamage(in DamageRequest request);
  }
}
