using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class NetworkGameServer : NetworkBehaviour
{
    public static NetworkGameServer Instance;
    private const float TURN_TIME = 15f;

    struct PlayerAction
    {
        public string Action;
        public int Amount;
    }

    public Game game;
    private Dictionary<ulong, Player> clientToPlayer = new();
    private Dictionary<ulong, PlayerAction> queuedActions = new();
    private Dictionary<ulong, int> clientsBetAmount = new();
    private List<ulong> playerOrder = new();
    private Coroutine turnTimer;
    private int indexNum = 1;
    private int callAmount = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        Instance = this;
        game = new Game();
        game.OnPlayerActionRequest += HandlePlayerTurnRequest;
        clientToPlayer.Clear();
        playerOrder.Clear();
        indexNum = 1;
    }

    public void StartGame()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[Server] Only the server can start the game!");
            return;
        }
        NetworkPokerBridge.Instance.HideClientWaitOverlayClientRpc();
        StopAllCoroutines();
        StartCoroutine(GameSequence());
    }

    public void Shutdown()
    {
        StopAllCoroutines();
        UnbindAllEvents();
        NetworkPokerBridge.Instance.HideClientWaitOverlayClientRpc();
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        Destroy(gameObject);
    }

    private void UnbindAllEvents()
    {
        if (game != null) game.OnPlayerActionRequest -= HandlePlayerTurnRequest;
    }

    private IEnumerator GameSequence()
    {
        //Update dealer and broadcast
        game.SetDealerId();
        yield return StartCoroutine(UpdateDealer(game.Players[game.dealerIndex].Id));
        BroadcastState();
        yield return new WaitForSeconds(1f);

        //Assign blinds
        game.BlindBets(game.bigBlindAmount / 2, game.bigBlindAmount);
        BroadcastState();
        UpdateBlindPlayers(game.GetBlindPlayers());
        yield return new WaitForSeconds(1.5f);

        //Deal hole cards
        game.DealingCards();
        yield return StartCoroutine(UpdateHoleCards());
        BroadcastState();
        yield return new WaitForSeconds(1f);

        //Pre-flop betting
        yield return StartCoroutine(game.PlacingBets());
        BroadcastState();

        //If everyone folded early, skip to next round
        if (game.roundSkipped)
        {
            yield return StartCoroutine(ResolveRound());
            ShowStartUI();
            yield break;
        }

        ClearPlayersBetBox();

        //Flop
        game.DealFlop();
        yield return new WaitForSeconds(1f);
        BroadcastState();
        ShowTotalPotSize(true);
        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(game.PlacingBets());
        BroadcastState();

        if (game.roundSkipped)
        {
            yield return StartCoroutine(ResolveRound());
            ShowStartUI();
            yield break;
        }

        ClearPlayersBetBox();

        //Turn
        game.DealTurn();
        yield return new WaitForSeconds(1f);
        BroadcastState();
        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(game.PlacingBets());
        BroadcastState();

        if (game.roundSkipped)
        {
            yield return StartCoroutine(ResolveRound());
            ShowStartUI();
            yield break;
        }

        ClearPlayersBetBox();

        //River
        game.DealRiver();
        yield return new WaitForSeconds(1f);
        BroadcastState();
        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(game.PlacingBets());
        BroadcastState();

        if (game.roundSkipped)
        {
            yield return StartCoroutine(ResolveRound());
            ShowStartUI();
            yield break;
        }

        ClearPlayersBetBox();

        //Showdown
        yield return StartCoroutine(ResolveRound());
        ShowStartUI();
    }

    public void RegisterPlayer(ulong clientId, string playerUsername, int startingChips)
    {
        // find first empty seat
        int seatIndex = Array.FindIndex(game.Players, p => p == null);
        if (seatIndex == -1)
        {
            Debug.LogWarning("No free seat for player; rejecting or queueing connection.");
            return;
        }

        bool isUsernameTaken = Array.Find(game.Players, p => p != null && p.Name == playerUsername) != null;
        if (isUsernameTaken)
        {
            playerUsername += $"{indexNum}";
            indexNum++;
        }

        if (game.Players[0] != null)
            startingChips = startingChips > game.Players[0].Chips ? game.Players[0].Chips : startingChips;
       
        // create Player with id = clientId (important)
        var player = new Player(clientId, playerUsername, startingChips);

        // assign to game seat and mappings
        game.Players[seatIndex] = player;
        clientToPlayer[clientId] = player;
        playerOrder.Add(clientId);

        // Send initial state
        BroadcastState();
    }

    private void HandlePlayerTurnRequest(Player player)
    {
        ulong playerId = player.Id;
        int highestBet = game.highestBet;
        int lastBetAmount = game.lastBetAmount;

        if (turnTimer != null) StopCoroutine(turnTimer);

        // Send a message to that client only
        NetworkPokerBridge.Instance.NotifyTurnClientRpc(playerId, highestBet, lastBetAmount);

        foreach (var p in game.Players)
        {
            if (p == null) continue;

            callAmount = Mathf.Max(0, highestBet - p.BetAmount);
            if (!clientsBetAmount.ContainsKey(p.Id)) clientsBetAmount.Add(p.Id, callAmount);
            else clientsBetAmount[p.Id] = callAmount;

            if (p.Id == playerId) NetworkPokerBridge.Instance.ChangePlayerCallBtnNameClientRpc(clientsBetAmount[playerId]);
        }

        turnTimer = StartCoroutine(TurnTimer(player));

        if (queuedActions.TryGetValue(playerId, out var playerAction))
        {
            HandlePlayerAction(playerId, playerAction.Action, playerAction.Amount);
            queuedActions.Remove(playerId);
        }
    }

    private IEnumerator TurnTimer(Player player)
    {
        float timeLeft = TURN_TIME;
        while (timeLeft > 0f)
        {
            yield return new WaitForSeconds(1f);
            timeLeft -= 1f;
        }
        if (clientsBetAmount.TryGetValue(player.Id, out var callAmount))
        {
            if (callAmount == 0) HandlePlayerAction(player.Id, "Check");
            else HandlePlayerAction(player.Id, "Fold");
        }
    }

    public void HandlePlayerAction(ulong clientId, string action, int amount = 0)
    {
        if (turnTimer != null)
        {
            StopCoroutine(turnTimer);
            turnTimer = null;
        }

        if (!clientToPlayer.TryGetValue(clientId, out var player)) return;

        if (!Enum.TryParse<Game.ActionType>(action, true, out var actType)) return;

        if (actType == Game.ActionType.Rise) amount -= player.BetAmount;

        game.PlayerAction(player, actType, amount);
        BroadcastState();
        NetworkPokerBridge.Instance.UpdateBetClientRpc(clientId);
        AddToPot(amount);
    }

    public void QueuePlayerAction(ulong clientId, string action, int amount = 0)
    {
        int betAmount = game.Players.Where(p => p != null && p.Id == clientId).Select(p => p.BetAmount).FirstOrDefault();

        if (action == "Call")
        {
            if (clientsBetAmount.TryGetValue(clientId, out int callAmount))
            {
                if (callAmount == 0) action = "Check";
                if (callAmount > 0) amount = callAmount;
            }
        }

        if (game.currentPlayer != null)
        {
            if (clientId == game.currentPlayer.Id)
            {
                HandlePlayerAction(clientId, action, amount);
                return;
            }
        }
        queuedActions[clientId] = new PlayerAction { Action = action, Amount = amount };
    }

    public void BroadcastState()
    {
        var playersInfo = new PlayerInfo[game.Players.Length];

        for (int i = 0; i < game.Players.Length; i++)
        {
            var p = game.Players[i];

            if (p == null)
                continue;
            
            playersInfo[i] = new PlayerInfo
            {
                Name = new FixedString64Bytes(p.Name),
                Chips = p.Chips,
                OwnerClientId = p.Id,
                SeatIndex = i,
                BetAmount = p.BetAmount,
                TotalBetAmount = p.TotalBetAmount,
                HandName = new FixedString64Bytes(p.HandName),
                HasChecked = p.HasChecked,
                HasFolded = p.HasFolded,
                IsAllIn = p.IsAllIn,
            };
        }

        var state = new PublicStateData
        {
            CommunityCards = CardSerializer.ToBytes(game.CommCards),
            PotSize = game.Pots.Count != 0 ? game.Pots[0].Chips : 0,
            Players = playersInfo,
            BigBlindAmount = game.bigBlindAmount,
        };

        NetworkPokerBridge.Instance.BroadcastPublicStateClientRpc(state);
    }

    private void UpdateBlindPlayers(List<Player> blindPlayers)
    {
        var blindPlayersIds = blindPlayers.Select(p => p.Id).ToArray();
        int smallBlindPlayerId = (int)blindPlayersIds[0];
        int bigBlindPlayerId = (int)blindPlayersIds[1];
        NetworkPokerBridge.Instance.SendBlindsIdsClientRpc(smallBlindPlayerId, bigBlindPlayerId);
    }

    private IEnumerator UpdateDealer(ulong dealerId)
    {
        yield return new WaitForSeconds(0.5f); //delay dealer update
        NetworkPokerBridge.Instance.SendDealerClientRpc(dealerId);
    }

    private void AddToPot(int amount)
    {
        NetworkPokerBridge.Instance.AddToPotClientRpc(amount);
    }

    private IEnumerator UpdateHoleCards()
    {
        var Clients = NetworkPokerBridge.Instance.ClientsReady;
        Clients.Clear();

        // Send cards to each client
        foreach (var player in game.Players)
        {
            if (player == null) continue;

            NetworkPokerBridge.Instance.SendPrivateCardsClientRpc(
                CardSerializer.ToBytes(player.HoleCards),
                player.Id,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { player.Id }
                    }
                }
            );
        }

        // Wait for all clients to respond
        while (Clients.Count < game.Players.Count(p => p != null)) yield return null;

        // NOW we can update hand ranks safely
        StartCoroutine(UpdatePlayersHandRank());
    }

    private IEnumerator UpdatePlayersHandRank()
    {
        foreach (var player in game.Players)
        {
            if (player == null) continue;

            NetworkPokerBridge.Instance.UpdateHandRankClientRpc(
                player.Id,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { player.Id } }
                }
            );
        }

        yield break;
    }

    private void ClearPlayersBetBox(float delay = 1f)
    {
        NetworkPokerBridge.Instance.ClearBetBoxesClientRpc(delay);
    }

    private void ShowTotalPotSize(bool state)
    {
        NetworkPokerBridge.Instance.ShowTotalPotSizeClientRpc(state);
    }

    private void SendPlayersHoleCardsInfo()
    {
        foreach (var player in game.Players)
        {
            if (player == null) continue;

            NetworkPokerBridge.Instance.SendPlayersHoleCardsClientRpc(
                CardSerializer.ToBytes(player.HoleCards),
                player.Id
            );
        }
    }

    private IEnumerator StartShowdown(List<Pot> Pots)
    {
        SendPlayersHoleCardsInfo();
        int startingIndex = game.GetShowdownPlayerIndex();
        var winnersInPots = game.DetermineWinners();

        foreach (var pot in Pots)
        {
            var players = pot.EligiblePlayers.Where(p => !p.HasFolded).ToList();
            string PotName = Pots.IndexOf(pot) != Pots.Count - 1 ? $"{Pots.IndexOf(pot) + 1} Pot" : "Main Pot";
            NetworkPokerBridge.Instance.SetPotInfoLabelClientRpc(PotName, pot.Chips);
            yield return new WaitForSeconds(2f); // delay before showdown starts

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[(startingIndex + i) % players.Count];
                if (player == null) continue;
                NetworkPokerBridge.Instance.StartShowdownClientRpc(player.Id);
                yield return new WaitForSeconds(5f);
            }

            if (winnersInPots.TryGetValue(pot, out var potWinners))
            {
                ShowGameResult(potWinners, players);
            }

            yield return new WaitForSeconds(15f);
        }
    }

    private IEnumerator ResolveRound()
    {
        yield return new WaitForSeconds(1f);
        ShowTotalPotSize(false);

        if (game.Players.Count(p => p != null && !p.HasFolded) > 1)
        {
            game.DealFlop();
            BroadcastState();
            game.DealTurn();
            BroadcastState();
            game.DealRiver();
            BroadcastState();

            var sidePots = game.CreatePots();
            yield return StartCoroutine(StartShowdown(sidePots));
        }
        else
        {
            var winnersInPots = game.DetermineWinners();
            ShowGameResult(winnersInPots[game.Pots[0]], game.Players.ToList()); //game.Pots[0] == Main Pot
        }

        BroadcastState();
        yield return new WaitForSeconds(2f);
        ClearRound();
        yield return new WaitForSeconds(1f);
        BroadcastState();
    }

    private void ShowGameResult(List<Player> winnersInPot, List<Player> eligiblePlayers)
    {
        foreach (var player in eligiblePlayers)
        {
            if (player == null) continue;

            if (winnersInPot.Contains(player))
            {
                NetworkPokerBridge.Instance.ShowGameResultClientRpc(
                player.Id,
                player.AmountWon
                );   
            }
            else
            {
                NetworkPokerBridge.Instance.ShowGameResultClientRpc(
                player.Id,
                0
                );   
            }
        }
    }

    private void ClearRound()
    {
        foreach (var player in game.Players.Where(p => p != null && p.Chips <= 0))
        {
            RemovePlayer(player.Id);
        }
        game.ClearRound();
        NetworkPokerBridge.Instance.ClearRoundClientRpc();
    }

    private void ShowStartUI()
    {
        NetworkPokerBridge.Instance.InitUIClientRpc();
    }

    public void RemovePlayer(ulong playerId)
    {
        if (game.Players[playerId] == null) return;

        // Remove from internal lists
        clientToPlayer.Remove(playerId);
        playerOrder.Remove(playerId);

        // Remove from game.Players array
        for (int i = 0; i < game.Players.Length; i++)
        {
            if (game.Players[i] != null && game.Players[i].Id == playerId)
            {
                game.Players[i] = null;
                if (i == game.dealerIndex && game.Players[i] != null)
                {
                    game.SetDealerId();
                    StartCoroutine(UpdateDealer(game.Players[game.dealerIndex].Id));
                }
                break;
            }
        }

        NetworkPokerBridge.Instance.ClearPlayerClientRpc(playerId);

        // Clean up server-side
        BroadcastState(); // updates UI for remaining players
    }
}
