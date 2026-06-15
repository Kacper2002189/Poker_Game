using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class NetworkPokerBridge : NetworkBehaviour
{
    public static NetworkPokerBridge Instance;
    private GameManager uiManager;
    public List<ulong> ClientsReady = new();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Instance = this;

        // Automatically find GameManager when this object spawns
        var uiManager = FindAnyObjectByType<GameManager>();
        if (uiManager != null)
        {
            BindUIManager(uiManager);
            uiManager.InitUI();
        }
        else
        {
            Debug.LogWarning("[Bridge] GameManager not found during OnNetworkSpawn!");
        }

        if (IsClient && !IsServer)
        {
            var startScreen = FindAnyObjectByType<StartScreenManager>();

            if (startScreen != null)
            {
                SendPlayerInfoServerRpc(
                    startScreen.playerName,
                    startScreen.playerChipsAmount
                );
            }
        }
    }

    public void BindUIManager(GameManager manager)
    {
        if (uiManager != null)
            return; // already bound

        uiManager = manager;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShutdownServer()
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        if (NetworkGameServer.Instance != null)
        {
            NetworkGameServer.Instance.Shutdown();
        }

        NetworkManager.Singleton.Shutdown();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendPlayerActionServerRpc(string action, int amount = 0, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        // Validate & process server-side logic
        NetworkGameServer.Instance.QueuePlayerAction(senderId, action, amount);
    }

    [ServerRpc(RequireOwnership = false)]
    public void QuitGameServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (senderId == NetworkManager.ServerClientId)
        {
            // HOST is quitting
            ReturnToMainMenuClientRpc(true);
            StartCoroutine(ShutdownAfterUI());
        }
        else
        {
            NetworkGameServer.Instance.HandlePlayerAction(senderId, "Fold");
            NetworkGameServer.Instance.RemovePlayer(senderId);
            ReturnToMainMenuClientRpc(false, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } } });
            NetworkManager.Singleton.DisconnectClient(senderId);
        }
    }

    private IEnumerator ShutdownAfterUI()
    {
        yield return new WaitForSeconds(0.2f);

        if (NetworkGameServer.Instance != null)
            NetworkGameServer.Instance.Shutdown();

        NetworkManager.Singleton.Shutdown();
    }

    [ClientRpc]
    public void ReturnToMainMenuClientRpc(bool hostLeft, ClientRpcParams rpcParams = default)
    {
        if (uiManager == null)
            uiManager = FindAnyObjectByType<GameManager>();

        uiManager?.QuitToMainMenu(hostLeft);
    }

    [ClientRpc]
    public void ChangePlayerCallBtnNameClientRpc(int callAmount)
    {
        uiManager.ChangeCallBtnText(callAmount);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendPlayerInfoServerRpc(string playerUsername, int playerChips, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        NetworkGameServer.Instance.RegisterPlayer(senderId, playerUsername, playerChips);
    }

    [ClientRpc]
    public void ClearPlayerClientRpc(ulong playerId)
    {
        if (playerId == NetworkManager.Singleton.LocalClientId)
            QuitGameServerRpc();

        uiManager.ClearPlayer(playerId);
    }

    [ClientRpc]
    public void BroadcastPublicStateClientRpc(PublicStateData stateData)
    {
        if (uiManager == null)
        {
            uiManager = FindAnyObjectByType<GameManager>();
            if (uiManager == null)
            {
                Debug.LogError("[Bridge] UI Manager still not found! Cannot update public UI.");
                return;
            }
        }

        if (stateData.CommunityCards == null || stateData.Players == null || stateData.Players.Length == 0)
        {
            Debug.LogWarning("Received empty or invalid PublicStateData!");
            return;
        }

        var commCards = CardSerializer.FromBytes(stateData.CommunityCards);
        uiManager.GetPotSize(stateData.PotSize);
        StartCoroutine(uiManager.UpdatePublicUI(commCards, stateData.Players, stateData.BigBlindAmount));
    }

    [ClientRpc]
    public void SendPrivateCardsClientRpc(byte[] cardByteArray, ulong playerId, ClientRpcParams rpcParams = default)
    {
        var holeCards = CardSerializer.FromBytes(cardByteArray);
        StartCoroutine(OnReceiveCardsRoutine(holeCards, playerId));
    }

    private IEnumerator OnReceiveCardsRoutine(List<Card> cards, ulong playerId)
    {
        // Wait for your animation to finish
        yield return StartCoroutine(uiManager.OnReceivePrivateCards(cards, playerId));

        // Tell server this client is done
        NotifyPrivateCardsReceivedServerRpc(playerId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void NotifyPrivateCardsReceivedServerRpc(ulong playerId)
    {
        if (!ClientsReady.Contains(playerId))
            ClientsReady.Add(playerId);
    }

    [ClientRpc]
    public void SendBlindsIdsClientRpc(int smallBlindId, int bigBlindId)
    {
        StartCoroutine(uiManager.ShowBlindsUI(smallBlindId, bigBlindId));
    }

    [ClientRpc]
    public void UpdateBetClientRpc(ulong playerId)
    {
        float latency = ((float)NetworkManager.Singleton.ServerTime.Time - uiManager.actionSentTime) * 1000f;

        uiManager.ShowPlayerBet(playerId);
        uiManager.SetFoldedPlayerBox();
    }

    [ClientRpc]
    public void AddToPotClientRpc(int amount)
    {
        uiManager.AddToPot(amount);
    }

    [ClientRpc]
    public void UpdateHandRankClientRpc(ulong playerId, ClientRpcParams rpcParams = default)
    {
        uiManager.ShowPlayerHandRank(playerId);
    }

    [ClientRpc]
    public void SendDealerClientRpc(ulong dealerId)
    {
        uiManager.ShowDealerButton(dealerId);
    }

    [ClientRpc]
    public void NotifyTurnClientRpc(ulong activePlayerId, int highestBet, int lastBetAmount)
    {
        uiManager.OnTurnChanged(activePlayerId, highestBet, lastBetAmount);
        uiManager.ShowPlayerHandRank(activePlayerId);
    }

    [ClientRpc]
    public void ClearBetBoxesClientRpc(float delay)
    {
        uiManager.ClearAllBets(delay);
    }

    [ClientRpc]
    public void ShowGameResultClientRpc(ulong playerId, int amountWon)
    {
        StartCoroutine(ShowGameResultRoutine(playerId, amountWon));
    }

    [ClientRpc]
    public void SetPotInfoLabelClientRpc(string potName, int potValue)
    {
        string txt = $"{potName}  ${potValue}";
        uiManager.SetPotLabelText(txt);
    }

    public IEnumerator ShowGameResultRoutine(ulong playerId, int amountWon)
    {
        yield return StartCoroutine(uiManager.ShowGameResult(playerId, amountWon));
    }

    [ClientRpc]
    public void SendPlayersHoleCardsClientRpc(byte[] cardByteArray, ulong playerId)
    {
        var holeCards = CardSerializer.FromBytes(cardByteArray);
        uiManager.UpdatePlayerCardView(playerId, holeCards);
    }

    [ClientRpc]
    public void StartShowdownClientRpc(ulong playerId)
    {
        StartCoroutine(StartShowdownRoutine(playerId));
    }

    public IEnumerator StartShowdownRoutine(ulong playerId)
    {
        yield return StartCoroutine(uiManager.StartShowdown(playerId));
    }

    [ClientRpc]
    public void ShowTotalPotSizeClientRpc(bool state)
    {
        uiManager.ShowTotalPotSize(state);
    }

    [ClientRpc]
    public void ClearRoundClientRpc()
    {
        uiManager.ClearRound();
    }

    [ClientRpc]
    public void InitUIClientRpc()
    {
        uiManager.InitUI();
    }

    [ClientRpc]
    public void HideClientWaitOverlayClientRpc()
    {
        var waitOverlay = FindAnyObjectByType<StartScreenManager>()?.ClientWaitOverlay;

        if (waitOverlay != null)
        {
            waitOverlay.gameObject.SetActive(false);
        }
    }
}
