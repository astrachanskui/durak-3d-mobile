namespace Durak.Gameplay;

public enum AttackState
{
    InProgress,
    BeatenOff,
    Successful
}

public class Attack : IAttack
{
    private readonly char _trumpSuit;
    private readonly List<Player> _attackers;
    private readonly List<AttackCard> _cards;
    private readonly Player _defender;
    private AttackState _state;
    
    // Запоминаем, сколько карт было у защищающегося на старте этого конкретного кона
    private readonly int _initialDefenderCardCount;

    public event EventHandler<AttackCardAddedEventArgs>? AttackCardAdded;
    public event EventHandler? AttackEnded;

    public Player PrincipalAttacker => _attackers[0];

    public Player Defender => _defender;

    public IReadOnlyList<Player> Attackers => _attackers.AsReadOnly();

    public IReadOnlyList<AttackCard> Cards => _cards.AsReadOnly();

    public AttackState State => _state;

    public bool IsDefending => _cards.Count % 2 != 0;

    public Attack(Player principalAttacker, Player defender, char trumpSuit)
    {
        _defender = defender;
        _trumpSuit = SuitValidator.Validate(trumpSuit)
            ? trumpSuit
            : throw new ArgumentOutOfRangeException(nameof(trumpSuit));
        _attackers = [principalAttacker];
        _cards = [];
        _state = AttackState.InProgress;
        
        // Фиксируем стартовое количество карт у защитника для контроля лимита подкидывания
        _initialDefenderCardCount = defender.Cards.Count;
    }

    public void AddAttacker(Player attacker)
    {
        if (_state != AttackState.InProgress)
        {
            throw new GameplayException("Cannot add attacker when the attack is not in progress");
        }

        if (!_attackers.Contains(attacker))
        {
            _attackers.Add(attacker);
        }
    }

    public void Play(Player player, Card card)
    {
        var result = CanPlay(player, card);
        
        if (!result)
        {
            throw new GameplayException(result.Error);
        }

        var attackCard = new AttackCard(player, card);
        _cards.Add(attackCard);
        player.Shed(card);

        AttackCardAdded?.Invoke(this, new AttackCardAddedEventArgs(attackCard));
    }

    public void End()
    {
        if (_state != AttackState.InProgress)
        {
            throw new GameplayException("Attack already ended");
        }

        if (_cards.Count % 2 == 0)
        {
            _state = AttackState.BeatenOff;
        }
        else
        {
            _defender.PickUp(_cards.Select(c => c.Card));
            _state = AttackState.Successful;
        }

        AttackEnded?.Invoke(this, new EventArgs());
    }

    public CanPlayResult CanPlay(Player player, Card card)
    {
        if (_state != AttackState.InProgress)
        {
            return new CanPlayResult(false, "Cannot play when the attack is not in progress");
        }

        if (!player.Cards.Contains(card))
        {
            return new CanPlayResult(false, "Cannot play non-player's card");
        }

        if (_cards.Select(c => c.Card).Contains(card))
        {
            return new CanPlayResult(false, "Cannot play already played card");
        }

        var isAttacking = !IsDefending;

        // Если идет атака/подкидывание
        if (isAttacking)
        {
            if (_cards.Count > 0 && _cards.TrueForAll(c => c.Card.Rank != card.Rank))
            {
                return new CanPlayResult(false, "Cannot play a card that does not match any other card's rank");
            }

            // РАСЧЕТ МОБИЛЬНОГО ЛИМИТА: количество АТАКующих карт на столе не может превышать 
            // стартовое количество карт у защитника, и общее число пар не может быть больше 6 (12 карт)
            int currentAttackPairsCount = _cards.Count / 2;
            if (currentAttackPairsCount >= _initialDefenderCardCount || _cards.Count >= 12 || _defender.Cards.Count == 0)
            {
                return new CanPlayResult(false, "Cannot have more attacking cards in this attack (Defender limit reached)");
            }
        }
        // Если идет защита
        else
        {
            if (_cards.Count > 0 && card.Rank < _cards[^1].Card.Rank && card.Suit == _cards[^1].Card.Suit)
            {
                return new CanPlayResult(false, "Cannot defend with a lower ranked card");
            }

            if (_cards.Count > 0 && card.Suit != _cards[^1].Card.Suit && card.Suit != _trumpSuit)
            {
                return new CanPlayResult(false, "Cannot defend with a different suited card that is not in trump suit");
            }
        }

        return new CanPlayResult(true, null);
    }

    public Player NextToPlay()
    {
        if (_cards.Count == 0)
        {
            return _attackers[0];
        }

        var isLastCardPlayedByAttacker = _attackers.Contains(_cards[^1].Player);
        if (isLastCardPlayedByAttacker)
        {
            return _defender;
        }

        return _cards[^2].Player;
    }
}
