using System;
using System.Collections.Generic;
using System.Linq;

namespace Durak.Gameplay;

public class Player
{
    private readonly List<Card> _cards;

    public event EventHandler<CardsAddedEventArgs>? CardsAdded;
    public event EventHandler<CardRemovedEventArgs>? CardRemoved;

    public string Id { get; }

    public IReadOnlyList<Card> Cards => _cards.AsReadOnly();

    public Player(string id)
    {
        Id = id;
        _cards = [];
    }

    public void PickUp(IEnumerable<Card> cards)
    {
        _cards.AddRange(cards);

        CardsAdded?.Invoke(this, new CardsAddedEventArgs(cards));
    }

    public void Shed(Card card)
    {
        _cards.Remove(card);

        CardRemoved?.Invoke(this, new CardRemovedEventArgs(card));
    }

    // ====================================================================
    // 🤖 ПРОДВИНУТЫЙ ИНТЕЛЛЕКТ ИИ (УЛУЧШЕНИЕ ИГРЫ КОМПЬЮТЕРА)
    // ====================================================================

    /// <summary>
    /// Выбирает лучшую карту для атаки или подкидывания (экономит козыри, заходит с мелочи)
    /// </summary>
    public Card? GetBestAttackCard(IAttack attack, char trumpSuit)
    {
        var validCards = _cards.Where(c => attack.CanPlay(this, c)).ToList();
        if (validCards.Count == 0) return null;

        // 1. Пытаемся найти самую мелкую НЕКОЗЫРНУЮ карту
        var nonTrumps = validCards.Where(c => c.Suit != trumpSuit).OrderBy(c => c.Rank).ToList();
        if (nonTrumps.Count > 0)
        {
            return nonTrumps[0];
        }

        // 2. Если остались только козыри, берем самый мелкий из них
        var trumps = validCards.Where(c => c.Suit == trumpSuit).OrderBy(c => c.Rank).ToList();
        if (trumps.Count > 0)
        {
            return trumps[0];
        }

        return null;
    }

    /// <summary>
    /// Выбирает лучшую карту для защиты (отбивается минимально возможным рангом, бережет сильные козыри)
    /// </summary>
    public Card? GetBestDefenseCard(IAttack attack, char trumpSuit)
    {
        var validCards = _cards.Where(c => attack.CanPlay(this, c)).ToList();
        if (validCards.Count == 0) return null;

        // Разделяем доступные для защиты карты на обычные масти и козыри
        var nonTrumps = validCards.Where(c => c.Suit != trumpSuit).OrderBy(c => c.Rank).ToList();
        var trumps = validCards.Where(c => c.Suit == trumpSuit).OrderBy(c => c.Rank).ToList();

        // 1. В первую очередь отбиваемся минимальной НЕКОЗЫРНОЙ картой
        if (nonTrumps.Count > 0)
        {
            return nonTrumps[0];
        }

        // 2. Если некозырных карт для защиты нет, отбиваемся минимальным КОЗЫРЕМ
        if (trumps.Count > 0)
        {
            return trumps[0];
        }

        return null;
    }
}

public class CardsAddedEventArgs : EventArgs
{
    public IEnumerable<Card> Cards { get; }

    public CardsAddedEventArgs(IEnumerable<Card> cards)
    {
        Cards = cards;
    }
}

public class CardRemovedEventArgs : EventArgs
{
    public Card Card { get; }

    public CardRemovedEventArgs(Card card)
    {
        Card = card;
    }
}

public class AttackCardAddedEventArgs : EventArgs
{
    public AttackCard Card { get; }

    public AttackCardAddedEventArgs(AttackCard card)
    {
        Card = card;
    }
}