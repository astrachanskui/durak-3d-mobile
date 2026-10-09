using System.Diagnostics.CodeAnalysis;

namespace Durak.Gameplay;

public class TurnLogic : ITurnLogic
{
    private const int _maxAttackCount = 100;
    private readonly Stack<IAttack> _attacks;
    private readonly IReadOnlyList<Player> _players;
    private readonly char _trumpSuit;

    public IReadOnlyList<IAttack> Attacks => _attacks.ToList().AsReadOnly();

    public TurnLogic(IReadOnlyList<Player> players, char trumpSuit)
    {
        _players = players;
        _trumpSuit = SuitValidator.Validate(trumpSuit)
            ? trumpSuit
            : throw new ArgumentOutOfRangeException(nameof(trumpSuit));
        _attacks = new();
    }

    public void AddAttack(IAttack attack)
    {
        if (_attacks.Count > _maxAttackCount)
        {
            throw new GameplayException($"Reached more than {_maxAttackCount} attacks");
        }

        _attacks.Push(attack);
    }

    public IAttack? NextAttack()
    {
        var attack = _attacks.Count == 0
            ? FirstAttack()
            : ConsecutiveAttack();

        if (attack != null)
        {
            AddAttack(attack);
        }

        return attack;
    }

    public bool TryGetNextAttack([MaybeNullWhen(false)] out IAttack nextAttack)
    {
        nextAttack = _attacks.Count == 0
            ? FirstAttack()
            : ConsecutiveAttack();

        if (nextAttack != null)
        {
            AddAttack(nextAttack);
            return true;
        }

        return false;
    }

    private Attack FirstAttack()
    {
        var lowestTrumpCard = _players
            .SelectMany(p => p.Cards)
            .Where(c => c.Suit == _trumpSuit)
            .OrderBy(c => c.Rank)
            .FirstOrDefault();

        int attackerIndex;

        if (lowestTrumpCard == null)
        {
            attackerIndex = Random.Shared.Next(0, _players.Count);
        }
        else
        {
            attackerIndex = _players
                .Select((p, i) => new { Cards = p.Cards, Index = i })
                .Single(p => p.Cards.Contains(lowestTrumpCard))
                .Index;
        }

        return CreateAttack(attackerIndex);
    }

    private Attack? ConsecutiveAttack()
    {
        var isOneOrZeroPlayersLeft = _players.Count(p => p.Cards.Count > 0) <= 1;
        if (isOneOrZeroPlayersLeft)
        {
            return null;
        }

        var previousAttack = _attacks.Peek();
        var previousAttackerIndex = -1;
        var previousDefenderIndex = -1;

        for (var i = 0; i < _players.Count; i++)
        {
            if (_players[i] == previousAttack.PrincipalAttacker)
            {
                previousAttackerIndex = i;
            }
            else if (_players[i] == previousAttack.Defender)
            {
                previousDefenderIndex = i;
            }

            if (previousAttackerIndex != -1 && previousDefenderIndex != -1)
            {
                break;
            }
        }

        if (previousAttackerIndex == -1 || previousDefenderIndex == -1)
        {
            throw new GameplayException("Could not find latest attack's players");
        }

        int attackerIndex;

        if (previousAttack.State == AttackState.Successful)
        {
            attackerIndex = NextIndex(previousDefenderIndex, null);
        }
		else
        {
            attackerIndex = previousDefenderIndex;
            if (_players[attackerIndex].Cards.Count == 0)
            {
                attackerIndex = NextIndex(attackerIndex, null);
            }
        }

        return CreateAttack(attackerIndex);
    }

    private int NextIndex(int index, int? previousDefenderIndex)
    {
        index = (index + 1) % _players.Count;
        var originalIndex = index;

        while (_players[index].Cards.Count == 0 || index == previousDefenderIndex)
        {
            index = (index + 1) % _players.Count;
            if (originalIndex == index)
            {
                break;
            }
        }

        return index;
    }

    private Attack CreateAttack(int attackerIndex)
    {
        var defenderIndex = NextIndex(attackerIndex, null);
        
        var attack = new Attack(_players[attackerIndex], _players[defenderIndex], _trumpSuit);
        
        int nextAttackerIndex = (attackerIndex + 1) % _players.Count;
        while (nextAttackerIndex != defenderIndex)
        {
            if (_players[nextAttackerIndex].Cards.Count > 0 && nextAttackerIndex != attackerIndex)
            {
                attack.AddAttacker(_players[nextAttackerIndex]);
            }
            nextAttackerIndex = (nextAttackerIndex + 1) % _players.Count;
        }

        int backupAttackerIndex = (defenderIndex + 1) % _players.Count;
        while (backupAttackerIndex != attackerIndex)
        {
            if (_players[backupAttackerIndex].Cards.Count > 0 && backupAttackerIndex != defenderIndex)
            {
                attack.AddAttacker(_players[backupAttackerIndex]);
            }
            backupAttackerIndex = (backupAttackerIndex + 1) % _players.Count;
        }

        return attack;
    }
}
