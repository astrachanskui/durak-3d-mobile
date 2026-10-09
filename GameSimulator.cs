using System.Text;
using Xunit.Abstractions;

namespace Durak.Gameplay.FunctionalTests;

public class GameSimulator
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly IDealer _dealer;
    private readonly ITurnLogic _turnLogic;
    private char _trumpSuit;

    public GameSimulator(ITestOutputHelper testOutputHelper, IDealer dealer, ITurnLogic turnLogic, char trumpSuit)
    {
        _testOutputHelper = testOutputHelper;
        _dealer = dealer;
        _turnLogic = turnLogic;
        _trumpSuit = trumpSuit;
	}

    public void Simulate()
    {
        IAttack? attack = null;

        while (_dealer.Deal(attack))
		{
            attack = Attack();
            if (attack == null)
            {
                break;
            }
        }

        while (Attack() != null)
        {
            // Do nothing
        }
    }

    private IAttack? Attack()
    {
        var attack = _turnLogic.NextAttack();

        if (attack == null)
        {
            return attack;
        }

        foreach (var attacker in attack.Attackers)
        {
            WritePlayerCards(attacker);
        }

        WritePlayerCards(attack.Defender);

        while (attack.State == AttackState.InProgress)
        {
            var isAttacking = PlaySmartOrPass(attack, attack.PrincipalAttacker, false);
            if (isAttacking)
            {
                var isDefending = PlaySmartOrPass(attack, attack.Defender, true);
                if (!isDefending)
                {
                    attack.End();
                }
            }
            else
            {
                attack.End();
            }
        }

        _testOutputHelper.WriteLine(ToString(attack));
        return attack;
    }

    private bool PlaySmartOrPass(IAttack attack, Player player, bool isDefending)
    {
        try
        {
            Card? targetCard = isDefending 
                ? player.GetBestDefenseCard(attack, _trumpSuit) 
                : player.GetBestAttackCard(attack, _trumpSuit);

            if (targetCard != null && attack.CanPlay(player, targetCard))
            {
                attack.Play(player, targetCard);
                return true;
            }
        }
        catch (GameplayException ex)
        {
            if (ex.Message.Contains("Cannot have more attacking cards") 
                || ex.Message.Contains("Reached more than"))
            {
                return false;
            }
        }

        return false;
    }

    private void WritePlayerCards(Player player)
    {
        var cards = string.Join(", ", player.Cards.Select(CardConverter.ToString));
        _testOutputHelper.WriteLine($"{player.Id}: [{cards}]");
    }

    private static string ToString(IAttack? attack)
    {
        if (attack == null)
        {
            return string.Empty;
        }

        var stringBuilder = new StringBuilder();

        var cardGroups = attack.Cards
            .Select(c => string.Format("{0} {1,3}", c.Player.Id, CardConverter.ToString(c.Card)))
            .Chunk(2);

        foreach (var cardGroup in cardGroups)
        {
            stringBuilder.AppendJoin("  ", cardGroup);
            stringBuilder.AppendLine();
        }

        return stringBuilder.ToString();
    }
}
