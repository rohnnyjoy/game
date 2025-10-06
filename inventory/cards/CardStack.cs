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
  public float AnimDuration { get; set; } = 0.22f;
  [Export]
  public float AnimDurationOffset { get; set; } = 0.0f;
  [Export]
  public float SuctionDuration { get; set; } = 0.10f; // Duration for drop animation.
  [Export]
  public float RepositionSpeed { get; set; } = 14.0f;
  [Export]
  public bool AnimateTransitions { get; set; } = true; // Default: animate card transitions

  // Horizontal padding on both sides; also used as the top/bottom padding when auto-sizing stacks.
  [Export]
  public float Padding { get; set; } = 20.0f;

  // Debug logging
  [Export] public bool DebugLogs { get; set; } = false;
  private int _lastGapIndexLogged = int.MinValue;
  private ulong _lastDropLogMs = 0;

  private Tween tween;
  private Array<Card2D> cards = new Array<Card2D>();

  public override void _Ready()
  {
    AddToGroup("CardStacks");
    // Reflow cards when this panel resizes (e.g., theme or parent layout changes).
    Resized += OnResized;
    // Initially update the children without animation.
    UpdateCards(GetCards(), false);
  }

  private void OnResized()
  {
    UpdateCards(GetCards(), false);
  }

  public override void _Process(double delta)
  {
    Card2D dragged = Card2D.CurrentlyDragged;
    bool mouseInside = false;
    if (dragged != null)
      mouseInside = GetGlobalRect().HasPoint(dragged.GetGlobalMousePosition());
    float centerY = Size.Y * 0.5f;

    // If dragging and the mouse is inside, create a gap for the incoming card.
    bool createGap = dragged != null && mouseInside && dragged.IsDragged;

    if (createGap)
    {
      Vector2 localMouse = GetLocalMousePosition();
      int nonDraggedCount = 0;
      foreach (Card2D card in GetCards())
      {
        if (!card.IsDragged)
          nonDraggedCount++;
      }
      int gapIndex = (int)Mathf.Round((localMouse.X - Padding) / Offset);
      gapIndex = Mathf.Clamp(gapIndex, 0, nonDraggedCount);
      if (DebugLogs && gapIndex != _lastGapIndexLogged)
      {
        _lastGapIndexLogged = gapIndex;
        GD.Print($"[CardStack:{Name}] drag gapIndex={gapIndex} nonDragged={nonDraggedCount} pad={Padding} off={Offset}");
      }

      int nonDraggedIndex = 0;
      foreach (Card2D card in GetCards())
      {
        if (card.IsDragged)
          continue;

        // Ensure scale is reset in case a removal tween had set it to zero.
        card.Scale = Vector2.One;

        int slotIndex = (nonDraggedIndex >= gapIndex) ? nonDraggedIndex + 1 : nonDraggedIndex;
        float gap = Mathf.Max(0, Offset - card.CardCore.CardSize.X);
        float slotLeft = Padding + slotIndex * Offset;
        Vector2 targetPos = new Vector2(slotLeft + 0.5f * gap, centerY - card.CardCore.CardSize.Y * 0.5f);
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

        float gap = Mathf.Max(0, Offset - card.CardCore.CardSize.X);
        float slotLeft = Padding + index * Offset;
        Vector2 targetPos = new Vector2(slotLeft + 0.5f * gap, centerY - card.CardCore.CardSize.Y * 0.5f);
        card.Position = card.Position.Lerp(targetPos, (float)(RepositionSpeed * delta));
        index++;
      }
    }
  }

  public void UpdateCards(Array<Card2D> newCards, bool animated = true)
  {
    bool doAnim = animated && AnimateTransitions;
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
        if (doAnim)
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
      gapIndex = (int)Mathf.Round((localMouse.X - Padding) / Offset);
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

      float gap2 = Mathf.Max(0, Offset - card.CardCore.CardSize.X);
      float slotLeft2 = Padding + slotIndex * Offset;
      Vector2 targetPos = new Vector2(slotLeft2 + 0.5f * gap2, centerY - card.CardCore.CardSize.Y * 0.5f);
      if (doAnim)
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

  public virtual void OnCardDrop(Control card, Vector2 dropLocalPos)
  {
    if (!(card is Card2D card2d))
      return;

    int newIndex = (int)Mathf.Clamp(Mathf.Round((dropLocalPos.X - Padding) / Offset), 0, GetCardCount());
    if (DebugLogs)
    {
      ulong now = Time.GetTicksMsec();
      if (now - _lastDropLogMs > 50UL)
      {
        _lastDropLogMs = now;
        string from = card2d.GetParent()?.Name ?? "<null>";
        GD.Print($"[CardStack:{Name}] drop(defer) card={card2d.Name} from={from} -> index={newIndex} count={GetCardCount()} pad={Padding} off={Offset}");
      }
    }

    // Defer: perform scene graph mutation + data updates next frame to avoid re-entrant loops.
    CallDeferred(nameof(FinishDrop), card2d, newIndex, card2d.GetParent(), this);
  }

  private static bool _finishGuard = false;
  private void FinishDrop(Card2D card2d, int newIndex, Node oldParentNode, Node newParentNode)
  {
    if (_finishGuard)
      return;
    _finishGuard = true;
    DebugTrace.Log($"CardStack.FinishDrop card={card2d?.Name} newIndex={newIndex} old={(oldParentNode as Node)?.Name} new={(newParentNode as Node)?.Name}");

    var newParent = newParentNode as CardStack ?? this;
    var oldParent = oldParentNode as CardStack;

    // Move node across parents if needed
    if (card2d.GetParent() != newParent)
    {
      if (card2d.GetParent() != null)
        card2d.GetParent().RemoveChild(card2d);
      newParent.AddChild(card2d);
      EmitSignal(nameof(CardMovedEventHandler), card2d, oldParent, newParent);
    }

    // Position and order
    card2d.Scale = Vector2.One;
    Vector2 targetGlobalPos = card2d.GetGlobalMousePosition() + card2d.DragOffset;
    Vector2 newLocalPos = targetGlobalPos - newParent.GetGlobalRect().Position;
    card2d.Position = newLocalPos;
    newParent.MoveChild(card2d, newIndex);

    // Update source and destination lists after the move
    if (oldParent != null)
    {
      DebugTrace.Log($"CardStack.FinishDrop -> oldParent.OnCardsChanged count={oldParent.GetCards().Count}");
      oldParent.OnCardsChanged(oldParent.GetCards());
    }
    DebugTrace.Log($"CardStack.FinishDrop -> newParent.OnCardsChanged count={newParent.GetCards().Count}");
    newParent.OnCardsChanged(newParent.GetCards());

    // Snap/animate cards into their slots immediately after drop, Balatro-style
    newParent.UpdateCards(newParent.GetCards(), true);
    DebugTrace.Log($"CardStack.FinishDrop done");

    _finishGuard = false;
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
