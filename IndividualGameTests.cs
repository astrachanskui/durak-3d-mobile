using Xunit.Abstractions;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Durak.Gameplay.FunctionalTests;

public class IndividualGameTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public IndividualGameTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData(3)] // Конфигурация: Вы + 2 ИИ
    [InlineData(4)] // Конфигурация: Вы + 3 ИИ
    public void TurnLogic_NextAttackIsNull_ShouldHaveOnePlayerWithCards(int playerCount)
    {
        var players = new List<Player>();
        for (var i = 0 ; i < playerCount; i++)
        {
            players.Add(new Player($"P{i + 1}"));
        }

        var deck = new Deck(new FrenchSuited36CardProvider(), new DefaultCardShuffler());
        var dealer = new Dealer(6, players, deck);
        var turnLogic = new TurnLogic(players, deck.TrumpSuit);
        
        // Передаем deck.TrumpSuit в обновленный мобильный симулятор для корректной тактики ИИ
        var gameSimulator = new GameSimulator(_testOutputHelper, dealer, turnLogic, deck.TrumpSuit);

        _testOutputHelper.WriteLine($"Trump: {deck.TrumpSuit}");

        try
        {
            gameSimulator.Simulate();
        }
        catch (Exception ex)
        {
            _testOutputHelper.WriteLine(ex.Message);
            _testOutputHelper.WriteLine(ex.StackTrace);
            _testOutputHelper.WriteLine(string.Join(',', players.Select(p => p.Cards.Count)));
        }

        var lastAttack = turnLogic.Attacks[^1];
        var isDraw = lastAttack.PrincipalAttacker.Cards.Count == 0 
            && lastAttack.Defender.Cards.Count == 0;

        if (!isDraw)
        {
            players.Should().ContainSingle(p => p.Cards.Count > 0);
        }
    }
}
