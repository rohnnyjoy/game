using Godot;
using System;
using Godot.Collections;
using System.Collections.Generic;

public partial class CardStack : Panel
{
  // Signals for card movement and change.
  [Signal]
  public delegate void CardMovedEventHandler(Control card, Node from_stack, Node to_stack);
  [Signal]
  public delegate void CardsChangedEventHandler(Array<Card2D> cards);

  // Exported properties for animation and layout.
  [Export]
  public float Offset { get; set; } = 120.0f;
  [Export]
  public float AnimDuration { get; set; } = 0.3f;
  [Export]
  public float AnimDurationOffset { get; set; } = 0.05f;
  [Export]
  public float SuctionDuration { get; set; } = 0.15f; // Duration for drop animation.
  [Export]
  public float RepositionSpeed { get; set; } = 10.0f;

  private Tween tween;
  private Array<Card2D> cards = new Array<Card2D>();

  public override void _Ready()
  {
    AddToGroup("CardStacks");
    // Initially update the children without animation.
    UpdateCards(GetCards(), false);
  }

  public override void _Process(double delta)
  {
    Card2D dragged = Card2D.CurrentlyDragged;
    if (dragged == null)
      return;

    bool mouseInside = GetGlobalRect().HasPoint(dragged.GetGlobalMousePosition());
    float centerY = Size.Y * 0.5f;

    // If dragging and the mouse is inside, create a gap for the incoming card.
    bool createGap = mouseInside && dragged.IsDragged;

    if (createGap)
    {
      Vector2 localMouse = GetLocalMousePosition();
      int nonDraggedCount = 0;
      foreach (Card2D card in GetCards())
      {
        if (!card.IsDragged)
          nonDraggedCount++;
      }
      int gapIndex = (int)Mathf.Round((localMouse.X - 20) / Offset);
      gapIndex = Mathf.Clamp(gapIndex, 0, nonDraggedCount);

      int nonDraggedIndex = 0;
      foreach (Card2D card in GetCards())
      {
        if (card.IsDragged)
          continue;

        // Ensure scale is reset in case a removal tween had set it to zero.
        card.Scale = Vector2.One;

        int slotIndex = (nonDraggedIndex >= gapIndex) ? nonDraggedIndex + 1 : nonDraggedIndex;
        Vector2 targetPos = new Vector2(20 + slotIndex * Offset, centerY - card.CardCore.CardSize.Y * 0.5f);
        card.Position = card.Position.Lerp(targetPos, (float)(RepositionSpeed * delta));
        nonDraggedIndex++;
      }
    }
    else
    {
      int index = 0;
      foreach (Card2D card in GetCards())
      {
        if (card.IsDragged)
          continue;

        // Reset scale for safety.
        card.Scale = Vector2.One;

        Vector2 targetPos = new Vector2(20 + index * Offset, centerY - card.CardCore.CardSize.Y * 0.5f);
        card.Position = card.Position.Lerp(targetPos, (float)(RepositionSpeed * delta));
        index++;
      }
    }
  }

  public void UpdateCards(Array<Card2D> newCards, bool animated = true)
  {
    // Record current positions for cards already in this stack.
    System.Collections.Generic.Dictionary<Card2D, Vector2> originalPositions =
        new System.Collections.Generic.Dictionary<Card2D, Vector2>();
    foreach (Card2D card in newCards)
    {
      if (card.GetParent() == this)
        originalPositions[card] = card.Position;
    }

    // Animate removal for children that are no longer in newCards.
    foreach (Node child in GetChildren())
    {
      if (child is Card2D card && !newCards.Contains(card))
      {
        if (animated)
        {
          // Capture card in a local variable to avoid closure issues.
          Card2D cardToRemove = card;
          Tween removalTween = cardToRemove.CreateTween();
          removalTween.TweenProperty(cardToRemove, "scale", Vector2.Zero, SuctionDuration)
              .SetTrans(Tween.TransitionType.Linear)
              .SetEase(Tween.EaseType.In);
          removalTween.Finished += () => _OnRemovalTweenFinished(cardToRemove);
        }
        else
        {
          RemoveChild(card);
        }
      }
    }

    // Set our internal reference.
    cards = newCards;

    // Ensure every card in newCards is a child of this node.
    foreach (Card2D card in newCards)
    {
      if (card.GetParent() != this)
      {
        // Use the persistent ID to find the existing instance.
        Vector2 previousPos = card.Position;
        if (originalPositions.ContainsKey(card))
          previousPos = originalPositions[card];

        Node currentParent = card.GetParent();
        if (currentParent != null)
          currentParent.RemoveChild(card);
        AddChild(card);
        // Instead of resetting the card position completely, use the stored position.
        card.Position = previousPos;
      }
    }

    GD.Print("CardStack.UpdateCards: newCards.Count =", newCards.Count, "Children.Count =", GetChildCount());

    if (cards.Count == 0)
      return;

    float centerY = Size.Y * 0.5f;
    Card2D draggedCard = null;
    foreach (Card2D card in cards)
    {
      if (card.IsDragged)
      {
        draggedCard = card;
        break;
      }
    }

    int gapIndex = -1;
    if (draggedCard != null)
    {
      Vector2 localMouse = GetLocalMousePosition();
      gapIndex = (int)Mathf.Round((localMouse.X - 20) / Offset);
      int nonDraggedCount = cards.Count - 1;
      gapIndex = Mathf.Clamp(gapIndex, 0, nonDraggedCount);
    }

    int nonDraggedIndex = 0;
    foreach (Card2D card in cards)
    {
      if (card.IsDragged)
        continue;

      // Ensure scale is reset.
      card.Scale = Vector2.One;

      int slotIndex = nonDraggedIndex;
      if (gapIndex != -1 && nonDraggedIndex >= gapIndex)
        slotIndex = nonDraggedIndex + 1;

      Vector2 targetPos = new Vector2(20 + slotIndex * Offset, centerY - card.CardCore.CardSize.Y * 0.5f);
      if (animated)
      {
        Tween cardTween = card.CreateTween();
        float delay = nonDraggedIndex * AnimDurationOffset;
        cardTween.TweenProperty(card, "position", targetPos, SuctionDuration)
            .SetDelay(delay)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        cardTween.TweenProperty(card, "scale", Vector2.One, SuctionDuration)
            .SetDelay(delay)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
      }
      else
      {
        card.Position = targetPos;
        card.Scale = Vector2.One;
      }
      nonDraggedIndex++;
    }
  }

  private void _OnRemovalTweenFinished(Card2D card)
  {
    if (card.GetParent() == this)
      RemoveChild(card);
  }

  public Array<Card2D> GetCards()
  {
    Array<Card2D> cardList = new Array<Card2D>();
    foreach (Node child in GetChildren())
    {
      if (child is Card2D card)
        cardList.Add(card);
    }
    return cardList;
  }

  public void OnCardDrop(Control card, Vector2 dropLocalPos)
  {
    if (!(card is Card2D card2d))
    {
      GD.PrintErr("Dropped card is not a Card2D!");
      return;
    }

    int newIndex = (int)Mathf.Clamp(Mathf.Round(dropLocalPos.X / Offset), 0, GetCardCount());
    Node oldParent = card2d.GetParent();
    if (oldParent != this)
    {
      if (oldParent != null && oldParent is CardStack oldParentStack)
      {
        oldParentStack.RemoveChild(card2d);
        oldParentStack.OnCardsChanged(oldParentStack.GetCards());
      }
      AddChild(card2d);
      EmitSignal(nameof(CardMovedEventHandler), card2d, oldParent, this);
    }

    // Reset scale.
    card2d.Scale = Vector2.One;

    // Snap the card to a new position.
    Vector2 targetGlobalPos = card2d.GetGlobalMousePosition() + card2d.DragOffset;
    Vector2 newLocalPos = targetGlobalPos - GetGlobalRect().Position;
    card2d.Position = newLocalPos;

    MoveChild(card2d, newIndex);

    // Notify the client that cards have changed.
    Array<Card2D> currentCards = GetCards();
    OnCardsChanged(currentCards);
  }

  /// <summary>
  /// Called when the card order has changed (for example, via a drop).
  /// Clients should override this to update their underlying data structure and then (if needed) call UpdateCards().
  /// </summary>
  public virtual void OnCardsChanged(Array<Card2D> newCards)
  {
    // Base implementation does nothing.
  }

  public int GetCardCount()
  {
    return cards.Count;
  }

  private bool ArraysEqual(Array<Card2D> a, Array<Card2D> b)
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
