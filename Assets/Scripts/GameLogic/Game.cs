#nullable enable
using UnityEngine;
using Random = System.Random;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEditor;
using System.Collections;

public class Game
{
    private const int MAX_PLAYERS = 5;
    public List<Card> CommCards = new();
    public Player?[] Players = new Player?[MAX_PLAYERS];
    public List<Pot> Pots = new();
    private Deck deck;
    private HandEvaluation evaluation;
    private Random random = new();

    public enum ActionType { Call, Check, Rise, Fold }
    public event Action<Player>? OnPlayerActionRequest;
    private bool waitingForPlayerAction = false;
    public bool roundSkipped = false;
    public int bigBlindAmount;
    private int activePlayers;
    public int highestBet = 0;
    public int lastBetAmount = 0;
    private int roundCount;
    public int dealerIndex;
    private int lastRaiserIndex = -1;
    public Player? currentPlayer = null;

    public Game()
    {
        deck = new Deck();
        deck.AddCards();
        deck.Shuffle();
        evaluation = new HandEvaluation();
        bigBlindAmount = 20;
        dealerIndex = random.Next(Players.Length);
    }

    public void DealingCards()
    {
        int startIndex = (dealerIndex + 1) % Players.Length;
        for (int i = 0; i < Players.Length; i++)
        {
            int index = (startIndex + i) % Players.Length;
            if (Players[index] != null)
            {
                DealHand(Players[index]!);
                // update player's hand rank and name after dealing
                Players[index]!.HandRank = GetPlayerHandRank(Players[index]!);
                Players[index]!.HandName = GetPlayerHandName(Players[index]!.HandRank);
            }
        }
        activePlayers = GetActionablePlayersCount();
    }

    public void DealHand(Player player)
    {
        for (int round = 0; round < 2; round++)
        {
            var card = deck.Draw();
            if (card != null) player.DealHand(card);
        }
    }

    public void ClearRound()
    {
        roundCount = 0;
        deck.AddCards();
        deck.Shuffle();
        Pots.Clear();
        CommCards.Clear();
        //dealer rotation
        SetDealerId();
        highestBet = 0;
        Pots.Add(new Pot { Chips = 0, EligiblePlayers = Players.Where(p => p != null).Cast<Player>().ToList() });

        foreach (var p in Players.Where(p => p != null))
        {
            p!.HasFolded = false;
            p.IsAllIn = false;
            p.HasChecked = false;
            p.TotalBetAmount = 0;
            p.BetAmount = 0;
            p.AmountWon = 0;
            p.HoleCards = new List<Card>();
        }

        activePlayers = Players.Count(p => p != null && !p!.HasFolded && !p!.IsAllIn);
    }

    public void ResetGame()
    {
        Players = new Player[MAX_PLAYERS];
        Pots.Clear();
        CommCards.Clear();
        dealerIndex = 0;
        highestBet = 0;
        lastBetAmount = 0;
        roundSkipped = false;
        waitingForPlayerAction = false;
        roundCount = 0;
        lastRaiserIndex = -1;
    }

    public void SetDealerId()
    {
        do
        {
            dealerIndex = (dealerIndex + 1) % Players.Length;
        } while (Players[dealerIndex] == null);
    }

    public void DealFlop()
    {
        if (deck.Count != 0)
            while (CommCards.Count < 3) CommCards.Add(deck.Draw());

        UpdatePlayersHandRank();
    }

    public void DealTurn()
    {
        if (CommCards.Count == 3) CommCards.Add(deck.Draw());
        UpdatePlayersHandRank();
    }

    public void DealRiver()
    {
        if (CommCards.Count == 4) CommCards.Add(deck.Draw());
        UpdatePlayersHandRank();
    }

    public List<Player> GetBlindPlayers()
    {
        List<Player> BlindPlayers = new();
        //small blind
        for (int i = 1; i <= Players.Length; i++)
        {
            int index = (dealerIndex + i) % Players.Length;
            if (Players[index] != null)
            {
                BlindPlayers.Add(Players[index]!);
                break;
            }
        }
        //big blind
        for (int i = 1; i <= Players.Length; i++)
        {
            int index = (Array.IndexOf(Players, BlindPlayers[0]) + i) % Players.Length;
            if (Players[index] != null)
            {
                BlindPlayers.Add(Players[index]!);
                break;
            }
        }
        return BlindPlayers;
    }

    public void BlindBets(int smallBlind, int bigBlind)
    {
        List<Player> BlindPlayers = GetBlindPlayers();
        int smallBlindPlayerIndex = Array.IndexOf(Players, BlindPlayers[0]);
        int bigBlindPlayerIndex = Array.IndexOf(Players, BlindPlayers[1]);

        if (Pots.Count() == 0)
            Pots.Add(new Pot { Chips = 0, EligiblePlayers = Players.Where(p => p != null).Cast<Player>().ToList() });

        if (smallBlindPlayerIndex != -1 && Players[smallBlindPlayerIndex] != null)
        {
            var smallBlindPlayer = Players[smallBlindPlayerIndex]!;
            smallBlindPlayer.Bet(smallBlind);
            smallBlindPlayer.TotalBetAmount += smallBlind;
            Pots[0].AddToPot(smallBlind);
        }

        if (bigBlindPlayerIndex != -1 && Players[bigBlindPlayerIndex] != null)
        {
            var bigBlindPlayer = Players[bigBlindPlayerIndex]!;
            bigBlindPlayer.Bet(bigBlind);
            bigBlindPlayer.TotalBetAmount += bigBlind;
            Pots[0].AddToPot(bigBlind);
            lastBetAmount = bigBlind;
            highestBet = lastBetAmount;
        }
    }

    public int GetPlayerHandRank(Player player)
    {
        var playerHoleCards = player.HoleCards;
        var playerHand = new List<Card>(playerHoleCards);
        playerHand.AddRange(CommCards);
        return evaluation.GetBestHandValue(playerHand.ToArray());
    }

    public string GetPlayerHandName(int handRank)
    {
        return evaluation.GetHandName(handRank);
    }

    public void UpdatePlayersHandRank()
    {
        foreach (var player in Players)
        {
            if (player != null && !player.HasFolded)
            {
                player.HandRank = GetPlayerHandRank(player);
                player.HandName = GetPlayerHandName(player.HandRank);
            }
        }
    }

    public void PlayerAction(Player currentPlayer, ActionType action, int raiseAmount = 0)
    {
        int betDiff = highestBet - currentPlayer.BetAmount;

        switch (action)
        {
            case ActionType.Call:
                if (betDiff != 0)
                {
                    currentPlayer.Bet(betDiff);
                    if (!currentPlayer.IsAllIn)
                    {
                        Pots[0].AddToPot(betDiff);
                        currentPlayer.TotalBetAmount += betDiff;
                    }
                    else
                    {
                        Pots[0].AddToPot(currentPlayer.BetAmount);
                        currentPlayer.TotalBetAmount += currentPlayer.BetAmount;
                    }
                    currentPlayer.HasChecked = false;
                }
                break;
            case ActionType.Check:
                if (betDiff == 0)
                {
                    currentPlayer.HasChecked = true;
                }
                break;
            case ActionType.Rise:
                currentPlayer.Bet(raiseAmount);
                currentPlayer.TotalBetAmount += raiseAmount;
                highestBet = currentPlayer.BetAmount;
                Pots[0].AddToPot(raiseAmount);
                currentPlayer.HasRaised = true;
                currentPlayer.HasChecked = false;
                lastRaiserIndex = Array.IndexOf(Players, currentPlayer);
                break;
            case ActionType.Fold:
                currentPlayer.Fold();
                currentPlayer.HasChecked = false;
                break;
        }

        waitingForPlayerAction = false;
    }

    private int GetActionablePlayersCount()
    {
        return Players.Count(p => p != null && !p!.HasFolded && !p!.IsAllIn);
    }

    public bool LastPlayerStanding()
    {
        return Players.Count(p => p != null && !p!.HasFolded) == 1;
    }

    public IEnumerator PlacingBets()
    {
        if (Players.All(p => p == null)) yield break;

        roundCount++;
        lastRaiserIndex = -1;
        roundSkipped = false;

        if ((GetActionablePlayersCount() == 0 || activePlayers <= 1) && (LastPlayerStanding() || activePlayers <= 1))
        {
            roundSkipped = true;
            highestBet = 0;
            foreach (var p in Players.Where(p => p != null))
            {
                p!.HasBetBox = false;
                p!.BetAmount = 0;
            }
            yield break;
        }

        int bigBlindPlayerIndex = Array.IndexOf(Players, GetBlindPlayers()[1]);
        int currentPlayerIndex = (roundCount == 1) ? (bigBlindPlayerIndex + 1) % Players.Length : (dealerIndex + 1) % Players.Length;

        bool bettingRoundOver = false;
        int playersToAct = GetActionablePlayersCount();
        int playersActedSinceRaise = 0;

        while (!bettingRoundOver)
        {
            playersToAct = GetActionablePlayersCount();

            if (playersToAct == 0 || LastPlayerStanding())
            {
                roundSkipped = true;
                break;
            }

            currentPlayer = Players[currentPlayerIndex];

            if (currentPlayer != null && !currentPlayer.HasFolded && !currentPlayer.IsAllIn)
            {
                if (highestBet > lastBetAmount)
                {
                    lastBetAmount = highestBet;
                }

                waitingForPlayerAction = true;
                OnPlayerActionRequest?.Invoke(currentPlayer);
                while (waitingForPlayerAction) yield return null;

                if (currentPlayer.HasRaised)
                {
                    currentPlayer.HasRaised = false;
                    if (currentPlayer.IsAllIn) playersActedSinceRaise = 0;
                    else playersActedSinceRaise = 1;
                    playersToAct = GetActionablePlayersCount();
                }
                else if (!currentPlayer.HasFolded)
                {
                    playersActedSinceRaise++;
                }
            }

            activePlayers = Players.Count(p => p != null && !p!.HasFolded && !p!.IsAllIn);

            if (playersActedSinceRaise >= playersToAct && playersToAct > 0)
            {
                bettingRoundOver = true;
                highestBet = 0;
                foreach (var player in Players.Where(p => p != null))
                {
                    player!.HasBetBox = false;
                    player!.BetAmount = 0;
                }
            }

            currentPlayerIndex = (currentPlayerIndex + 1) % Players.Length;
        }
    }

    public List<Pot> CreatePots()
    {
        bool sidePotNeeded = Players.Any(p => p != null && p.IsAllIn) &&
                             Players.Max(p => p?.TotalBetAmount) > Players.Where(p => p != null && p.IsAllIn).Min(p => p?.TotalBetAmount);

        if (!sidePotNeeded) return Pots;

        var totalBetAmounts = Players.Where(p => p != null).Select(p => p!.TotalBetAmount).OrderBy(x => x).ToList();
        List<Pot> newPots = new();
        int previousAmount = 0;

        for (int i = 0; i < totalBetAmounts.Count; i++)
        {
            var eligible = Players.Where(p => p != null && p.TotalBetAmount >= totalBetAmounts[i]).Select(p => p).ToList();
            int amountDiff = totalBetAmounts[i] - previousAmount;
            int potSize = amountDiff * eligible.Count;
            if (potSize != 0)
                newPots.Add(new Pot { Chips = potSize, EligiblePlayers = eligible });
            previousAmount = totalBetAmounts[i];
        }

        newPots.Reverse();
        Pots = newPots;
        return Pots;
    }

    public int GetShowdownPlayerIndex()
    {
        int startShowdownPlayerIndex = -1;
        if (lastRaiserIndex == -1)
        {
            for (int i = 1; i <= Players.Length; i++)
            {
                int index = (dealerIndex + i) % Players.Length;
                if (Players[index] != null && !Players[index]!.HasFolded)
                {
                    startShowdownPlayerIndex = index;
                    return startShowdownPlayerIndex;
                }
            }
        }
        else startShowdownPlayerIndex = lastRaiserIndex;

        return startShowdownPlayerIndex;
    }

    public Dictionary<Pot, List<Player?>> DetermineWinners()
    {
        Dictionary<Pot, List<Player?>> WinnersInPots = new();
        var playersLeft = Players.Where(p => p != null && !p!.HasFolded).Select(p => p).ToList();

        if (playersLeft.Count == 1)
        {
            var lastPlayer = playersLeft.FirstOrDefault();
            if (lastPlayer != null)
            {
                lastPlayer.AddChips(Pots.Count > 0 ? Pots[0].Chips : 0);
                WinnersInPots.Add(Pots[0], playersLeft);
            }
            return WinnersInPots;
        }

        foreach (var pot in Pots)
        {
            var winner = pot.EligiblePlayers
                .Where(p => p != null && !p.HasFolded)
                .OrderByDescending(p => p!.HandRank)
               
                .FirstOrDefault();
            if (winner == null) continue;

            var tiedWinners = pot.EligiblePlayers
                .Where(p => p != null && !p.HasFolded && p!.HandRank == winner.HandRank)
                .ToList();

            if (tiedWinners.Count == 1)
            {
                winner.AddChips(pot.Chips);
            }
            else
            {
                int split = pot.Chips / tiedWinners.Count;
                foreach (var p in tiedWinners)
                {
                    p!.AddChips(split);
                }

                int remainingChips = pot.Chips % tiedWinners.Count;
                if (remainingChips != 0)
                {
                    for (int i = 1; i <= pot.EligiblePlayers.Count; i++)
                    {
                        int index = (dealerIndex + i) % Players.Length;
                        if (Players[index] != null && !tiedWinners.Any(p => p! == Players[index]))
                        {
                            Players[index]!.AddChips(remainingChips);
                            break;
                        }
                    }
                }
            }

            WinnersInPots.Add(pot, tiedWinners);
        }

        return WinnersInPots;
    }
}
