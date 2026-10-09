using Durak.Gameplay;
using Godot;
using System;
using System.Text;

namespace Durak.Godot;

public enum CardState
{
	InDeck,
	InHand,
	InAttack,
	Discarded
}

public partial class CardNode : StaticBody3D
{
	[Signal]
	public delegate void ClickedEventHandler(CardNode cardNode);

	public Card Card => _card ?? throw new GameException("Card is not initialized");

	public Vector3 TargetPosition { get; set; }

	public Vector3 TargetRotationDegrees { get; set; }

	public CardState CardState { get; set; } = CardState.InDeck;

	public bool IsAnimationEnabled { get; set; }

	public int OrderInHand { get; set; } = -1;

	// Переменная для отслеживания мобильного выделения карты в руке
	public bool IsSelectedInHand { get; private set; } = false;
	private Vector3 _originalHandPosition;

	[Export]
	private float _positionLerpWeight = 8.0f; // Увеличил дефолтный вес для более отзывчивого мобильного скольжения

	[Export]
	private float _rotationLerpWeight = 5.0f;

	[Export]
	private Texture2D? _texture;

	private Node3D DiscardPile { get => GetNode<Node3D>("/root/Main/Table/GameSurface/DiscardPile"); }

	private DateTime _physicsCooldownExpiration = DateTime.UtcNow;
	private Card? _card;

	private const string _cardSpriteBack = "Back";

	public void Initialize(Card card, CardState cardState)
	{
		_card = card;

		var texture = _texture ?? GetTexture(_card);
		GetNode<Sprite3D>(Constants.CardSpriteFront).Texture = texture;

		CardState = cardState;
		IsSelectedInHand = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (DateTime.UtcNow >= _physicsCooldownExpiration)
		{
			// Если карта выделена игроком в руке, плавно смещаем её целевую позицию чуть выше и ближе к камере
			Vector3 currentTarget = TargetPosition;
			if (CardState == CardState.InHand && IsSelectedInHand)
			{
				currentTarget += new Vector3(0, 0.05f, 0.03f);
			}

			GlobalPosition = GlobalPosition.Lerp(currentTarget, (float)delta * _positionLerpWeight);
			RotationDegrees = RotationDegrees.Lerp(TargetRotationDegrees, (float)delta * _rotationLerpWeight);
		}
	}

	public override void _InputEvent(Camera3D camera, InputEvent @event, Vector3 position, Vector3 normal, int shapeIdx)
	{
		// Обработка тач-событий на экране телефона
		if (@event is InputEventScreenTouch screenTouch && screenTouch.Pressed)
		{
			if (CardState != CardState.InHand)
				return;

			// Если карта еще не была выбрана — выделяем её и приподнимаем
			if (!IsSelectedInHand)
			{
				// Сбрасываем выделение у всех остальных карт в руке через родительский узел игрока
				var playerNode = GetParent<PlayerNode>();
				if (playerNode != null)
				{
					foreach (var child in playerNode.GetChildren())
					{
						if (child is CardNode otherCard)
						{
							otherCard.Deselect();
						}
					}
				}

				IsSelectedInHand = true;
				GD.Print($"Мобильное выделение карты: {_card}");
			}
			// Если карта УЖЕ была выделена — повторный тач совершает ход
			else
			{
				GD.Print($"Мобильный ход картой: {_card}");
				IsSelectedInHand = false;
				EmitSignal(SignalName.Clicked, this);
			}
			
			// Помечаем ввод как обработанный, чтобы не двигалась 3D-камера на фоне
			GetViewport().SetInputAsHandled();
		}
	}

	// Метод для возвращения карты на исходную позицию в руке
	public void Deselect()
	{
		if (CardState == CardState.InHand && IsSelectedInHand)
		{
			IsSelectedInHand = false;
		}
	}

	public void AddPhysicsCooldown(TimeSpan cooldown)
	{
		var cooldownStart = new DateTime(
			Math.Max(DateTime.UtcNow.Ticks, _physicsCooldownExpiration.Ticks), DateTimeKind.Utc);

		_physicsCooldownExpiration = cooldownStart + cooldown;
	}

	public void RotateDegrees(Vector3 rotationDegrees)
	{
		if (IsAnimationEnabled)
		{
			TargetRotationDegrees = rotationDegrees;
		}
		else
		{
			RotationDegrees = rotationDegrees;
		}
	}

	public void MoveGlobally(Vector3 position)
	{
		if (IsAnimationEnabled)
		{
			TargetPosition = position;
		}
		else
		{
			GlobalPosition = position;
		}
	}

	public void UpdateSortingOffsets(IAttack? attack = null)
	{
		if (attack != null)
		{
			if (attack.IsDefending)
			{
				GetNode<Sprite3D>(Constants.CardSpriteFront).SortingOffset = 1;
			}
			else
			{
				GetNode<Sprite3D>(Constants.CardSpriteFront).SortingOffset = 0;
			}
			return;
		}

		switch (CardState)
		{
			case CardState.InDeck:
				{
					GetNode<Sprite3D>(_cardSpriteBack).SortingOffset = 1;
					break;
				}
			case CardState.InHand:
				{
					if (GetParent<PlayerNode>().Player.Id == Constants.Player1Id)
					{
						GetNode<Sprite3D>(Constants.CardSpriteFront).SortingOffset = 2;
					}
					else
					{
						GetNode<Sprite3D>(Constants.CardSpriteFront).SortingOffset = 0;
					}
					break;
				}
			default:
				{
					break;
				}
		}
	}

	public void Discard()
	{
		GD.Print($"Discarding {Card}");
		IsSelectedInHand = false;
		MoveGlobally(DiscardPile.GlobalPosition);
		RotateDegrees(DiscardPile.RotationDegrees);
		CardState = CardState.Discarded;
	}

	public void PlaceCardOnTable(Node3D placement, IAttack attack)
	{
		IsSelectedInHand = false;
		MoveGlobally(placement.GlobalPosition);
		RotateDegrees(placement.RotationDegrees);
		CardState = CardState.InAttack;
		UpdateSortingOffsets(attack);
		GetNode<MeshInstance3D>("MeshInstance3D").Show();
	}

	private static Texture2D GetTexture(Card card)
	{
		var fileName = new StringBuilder("res://art/cards/fronts/");

		if (card.Suit == Suit.Clubs)
		{
			fileName.Append('c');
		}
		else if (card.Suit == Suit.Diamonds)
		{
			fileName.Append('d');
		}
		else if (card.Suit == Suit.Hearts)
		{
			fileName.Append('h');
		}
		else if (card.Suit == Suit.Spades)
		{
			fileName.Append('s');
		}
		else
		{
			throw new GameException($"Unknown card suit {card.Suit}");
		}

		var normalizedRank = card.Rank == 14 ? 1 : card.Rank;
		var formattedRank = $"{normalizedRank:00}";
		fileName.Append($"{formattedRank}.png");

		return GD.Load<Texture2D>(fileName.ToString());
	}
}
