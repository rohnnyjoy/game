public interface IFramedCardStack
{
  // Called when a card begins a manual drag within this stack.
  void BeginCardDrag(Card2D card, SlotFrame fromFrame, Godot.Vector2 startGlobalMouse);
  // Called when the card drag ends; returns true if the stack fully handled the drop.
  bool EndCardDrag(Card2D card, Godot.Vector2 endGlobalMouse);
  // Accept an external drop when a drag started in a different framed stack.
  // Returns true if the stack handled placing and data updates.
  bool AcceptExternalDrop(Card2D card, Godot.Vector2 endGlobalMouse);
}
