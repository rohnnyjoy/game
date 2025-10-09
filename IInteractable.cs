using System.Collections.Generic;

public interface IInteractable
{
  void OnInteract(string actionName);
  string GetInteractionText();

  IReadOnlyList<InteractionOption> GetInteractionOptions()
  {
    return new InteractionOption[]
    {
      InteractionOption.ForDefault(GetInteractionText())
    };
  }
}
