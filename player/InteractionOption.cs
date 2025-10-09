public readonly record struct InteractionOption(string ActionName, string Description)
{
  public const string DefaultAction = "interact";

  public static InteractionOption ForDefault(string description)
  {
    return new InteractionOption(DefaultAction, description ?? string.Empty);
  }
}
