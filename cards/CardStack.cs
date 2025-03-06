using Godot;
using System;
using Godot.Collections;

public partial class CardStack : Panel
{
  // Signals.
  [Signal]
  public delegate void CardMovedEventHandler(Control card, Node from_stack, Node to_stack);
  [Signal]
  public delegate void CardsChangedEventHandler(Godot.Collections.Array<Card2D> cards);

  // Exported properties.
  [Export]
  public float Offset { get; set; } = 120.0f;
  [Export]
  public float AnimDuration { get; set; } = 0.3f;
  [Export]
  public float AnimDurationOffset { get; set; } = 0.05f;
  [Export]
  public float SuctionDuration { get; set; } = 0.15f; // Shorter duration for drop animation.
  [Export]
  public string StackType { get; set; } = "inventory"; // e.g., "inventory" or "weapon".

  private Tween tween;
  private Godot.Collections.Array<Card2D> cards = new Godot.Collections.Array<Card2D>();

  public override void _Ready()
  {
    AddToGroup("CardStacks");
    UpdateCards(false);
  }

  public void UpdateCards(bool animated = true, bool useSuction = false)
  {
    Godot.Collections.Array<Card2D> newCards = GetCards();
    if (!ArraysEqual(newCards, cards))
    {
      cards = newCards;
      EmitSignal(nameof(CardsChangedEventHandler), cards);
    }
    if (cards.Count == 0)
      return;

    float centerY = Size.Y * 0.5f;
    if (tween != null && tween.IsRunning())
      tween.Kill();
    tween = CreateTween();
    tween.SetParallel(true);

    for (int i = 0; i < cards.Count; i++)
    {
      Card2D card = cards[i];
      // Calculate target position based on the card’s index.
      Vector2 targetPos = new Vector2(20 + i * Offset, centerY - card.CardCore.CardSize.Y * 0.5f);

      if (animated)
      {
        float duration = useSuction ? SuctionDuration : (AnimDuration + i * AnimDurationOffset);
        tween.TweenProperty(card, "position", targetPos, duration)  // Corrected property name
             .SetTrans(Tween.TransitionType.Linear)
             .SetEase(Tween.EaseType.Out);

        if (card.GetGlobalRect().HasPoint(GetGlobalMousePosition()))
        {
          tween.TweenProperty(card, "scale", new Vector2(1.2f, 1.2f), duration)  // Corrected property name
               .SetTrans(Tween.TransitionType.Linear)
               .SetEase(Tween.EaseType.Out);
        }
        else
        {
          tween.TweenProperty(card, "scale", Vector2.One, duration)  // Corrected property name
               .SetTrans(Tween.TransitionType.Linear)
               .SetEase(Tween.EaseType.Out);
        }
      }
      else
      {
        card.Position = targetPos;
        card.Scale = card.GetGlobalRect().HasPoint(GetGlobalMousePosition()) ? new Vector2(1.2f, 1.2f) : Vector2.One;
      }
    }
  }

  /// <summary>
  /// Returns an array of Card2D nodes which are direct children of this stack.
  /// </summary>
  public Godot.Collections.Array<Card2D> GetCards()
  {
    Godot.Collections.Array<Card2D> cardList = new Godot.Collections.Array<Card2D>();
    foreach (Node child in GetChildren())
    {
      if (child is Card2D card)
      {
        cardList.Add(card);
      }
    }
    return cardList;
  }

  /// <summary>
  /// Called when a card is dropped onto this stack.
  /// </summary>
  public void OnCardDrop(Control card)
  {
    float localX = card.GlobalPosition.X - GlobalPosition.X;
    int newIndex = (int)Mathf.Clamp(Mathf.Round(localX / Offset), 0, GetCardCount() - 1);
    Node oldParent = card.GetParent();
    if (oldParent != this)
    {
      GD.Print("Different stack.");
      // Remove the card from its old stack and add it to this one.
      oldParent.RemoveChild(card);
      AddChild(card);
      EmitSignal(nameof(CardMovedEventHandler), card, oldParent, this);
    }
    else
    {
      GD.Print("Same stack.");
      // If it’s the same stack, simply reposition it.
      MoveChild(card, newIndex);
    }
    GD.Print("Card dropped.");
    // Update the positions of the cards (with animation).
    UpdateCards(true, true);
    OnCardsReordered();
  }

  public int GetCardCount()
  {
    return cards.Count;
  }

  /// <summary>
  /// Base implementation; subclasses (like an InventoryStack) can override to update underlying data.
  /// </summary>
  public virtual void OnCardsReordered()
  {
    // Base implementation does nothing.
  }

  /// <summary>
  /// Helper method to compare two Godot.Collections.Array<Card2D> for equality.
  /// </summary>
  private bool ArraysEqual(Godot.Collections.Array<Card2D> a, Godot.Collections.Array<Card2D> b)
  {
    if (a.Count != b.Count)
      return false;
    for (int i = 0; i < a.Count; i++)
    {
      if (a[i] != b[i])
        return false;
    }
    return true;
  }
}
