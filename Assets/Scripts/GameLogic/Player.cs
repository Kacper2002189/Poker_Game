using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Player
{
    public event Action<int> OnChipsChanged;

    public string Name { get; private set; }
    public ulong Id { get; set; }
    public int Chips { get; set; }
    public int BetAmount { get; set; }
    public int TotalBetAmount { get; set; }
    public int AmountWon { get; set; }
    public List<Card> HoleCards = new();
    public bool IsDealer { get; set; } = false;
    public bool HasRaised { get; set; } = false;
    public bool HasFolded { get; set; } = false;
    public bool HasChecked { get; set; } = false;
    public bool IsAllIn { get; set; } = false;
    public bool HasBetBox = false;
    public int HandRank = 0;
    public string HandName = "";

    public Player(ulong id, string name, int chips)
    {
        Id = id;
        Name = name;
        Chips = chips;
    }

    public void DealHand(Card card)
    {
        if (card == null)
        {
            Debug.LogError($"Tried to deal a null card to {Name}!");
            return;
        }
        HoleCards.Add(card);
    }

    public void Fold()
    {
        BetAmount = 0;
        HasFolded = true;
    }

    public void Bet(int amount)
    {
        if (Chips > 0)
        {
            if (amount >= Chips)
            {
                amount = Chips;
                IsAllIn = true;
            }
            BetAmount += amount;
            Chips -= amount;
        }

        OnChipsChanged?.Invoke(Chips);
    }

    public void AddChips(int amount)
    {
        AmountWon = amount;
        Chips += amount;
        OnChipsChanged?.Invoke(Chips);
    }
}
