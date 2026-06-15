#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class GameManager : MonoBehaviour
{
    private MenuView pokerUI;
    private Dictionary<ulong, PlayerView> playerViewsById = new();
    private Dictionary<ulong, PlayerBetView> playerBetViewsById = new();
    private Dictionary<ulong, List<CardView>> holeCardsViews = new();
    private List<Card> CurrentCommunityCards = new();
    private List<GameObject> roundObjects = new();
    private HashSet<ulong> playersShownCards = new HashSet<ulong>();
    private PlayerInfo[] serverPlayers = Array.Empty<PlayerInfo>();
    private PlayerInfo[] uiPlayers = Array.Empty<PlayerInfo>();
    private DealerView? currentDealerButton;

    [Header("References")]
    [SerializeField] private PotManager potManager;
    [SerializeField] private CardView cardPrefab;
    [SerializeField] private DealerView dealerButtonPrefab;
    [SerializeField] private RectTransform tableTransform;
    [SerializeField] private RectTransform commCardsContainer;
    [SerializeField] private RectTransform holeCardsContainer;
    [SerializeField] private RectTransform dealerButtonContainer;
    [SerializeField] private VisualTreeAsset betBoxTemplate;
    [SerializeField] private VisualTreeAsset playerBoxTemplate;
    [SerializeField] private UIDocument uiDocument;

    private PlayerInfo currentPlayer;
    private PlayerInfo myPlayer;
    private int lastBetAmount;
    private int bigBlindAmount;
    private int potSize;
    public float actionSentTime;

    private GroupBox pokerTable;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        pokerTable = root.Q<GroupBox>("pokerTable");

        if (tableTransform.sizeDelta.x <= 0 || tableTransform.sizeDelta.y <= 0) return;

        pokerTable.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            tableTransform.sizeDelta = new Vector2(
                pokerTable.resolvedStyle.width,
                pokerTable.resolvedStyle.height
            );
        });

        pokerUI = new(uiDocument);
    }

    #region Client RPC Handlers

    public IEnumerator UpdatePublicUI(List<Card> communityCards, PlayerInfo[] playersFromServer, int bigBlindAmount)
    {
        // store server players
        serverPlayers = playersFromServer.Where(p => p.Name != string.Empty).ToArray();

        pokerUI.EnableBetButtons(false);

        //store bigBlind amount
        this.bigBlindAmount = bigBlindAmount;

        foreach (var card in communityCards)
        {
            // Skip spawning if this card is already in the current community cards
            if (CurrentCommunityCards.Contains(card))
                continue;

            Debug.Log($"Spawning card: {card.Rank} of {card.Suit}");
            CurrentCommunityCards.Add(card);
            yield return StartCoroutine(SpawnCard(card));
        }

        // Rotate for local display (your existing method)
        var rotated = RotatePlayers(serverPlayers);

        // Update UI using rotated visual order (uiIndex -> which slot to fill)
        for (int uiIndex = 0; uiIndex < rotated.Length; uiIndex++)
        {
            var pInfo = rotated[uiIndex];
            string panelName = $"player{uiIndex + 1}_box";
            VisualElement panel = uiDocument.rootVisualElement.Q<VisualElement>(panelName);

            //Debug.Log($"// HandName of {pInfo.Name} //\n// {pInfo.HandName} //");

            if (panel == null)
            {
                Debug.LogWarning($"[UpdatePublicUI] Missing UI slot: {panelName}");
                continue;
            }

            ulong ownerId = pInfo.OwnerClientId;

            // If an existing PlayerView for this owner exists, move it into this slot (if needed) and update it
            if (playerViewsById.TryGetValue(ownerId, out var existingView))
            {
                var existingPanel = existingView.GetPlayerBoxPanel();
                // If the view's visual panel is not currently the correct slot, re-parent it
                if (existingPanel != null && existingPanel.parent != panel)
                {
                    existingPanel.RemoveFromHierarchy();
                    panel.Clear();
                    panel.Add(existingPanel);

                    MovePlayerUI(ownerId, uiIndex);
                }
                existingView.UpdateFromInfo(pInfo);
            }
            else
            {
                // No view exists yet for this owner -> create one
                panel.Clear();
                VisualElement playerBox = playerBoxTemplate.Instantiate();
                panel.Add(playerBox);

                var view = new PlayerView(playerBox, pInfo);
                playerViewsById[ownerId] = view;
                view.Refresh();
            }
        }

        // store rotated list for other functions (dealer placement, blinds etc.)
        uiPlayers = rotated;
    }

    private PlayerInfo[] RotatePlayers(PlayerInfo[] players)
    {
        ulong myId = NetworkManager.Singleton.LocalClientId;

        // get only non-empty seats
        var realPlayers = players.Where(p => p.Name != string.Empty).OrderBy(p => p.SeatIndex).ToList();

        int myIndex = realPlayers.FindIndex(p => p.OwnerClientId == myId);

        if (myIndex < 0) myIndex = 0;

        PlayerInfo[] rotated = new PlayerInfo[realPlayers.Count];

        //update players seat indexes
        for (int i = 0; i < rotated.Length; i++)
        {
            rotated[i] = realPlayers[(i + myIndex) % rotated.Length];
            rotated[i].SeatIndex = i;
        }

        return rotated;
    }

    public void ShowPlayerHandRank(ulong playerId)
    {
        if (playerViewsById.TryGetValue(playerId, out var view) && currentPlayer.OwnerClientId == myPlayer.OwnerClientId)
            view.SetHandRankLabel(true);
    }

    public IEnumerator OnReceivePrivateCards(List<Card> holeCards, ulong playerId)
    {
        for (int round = 0; round < 2; round++)
        {
            foreach (var client in serverPlayers)
            {
                SpawnHoleCard(holeCards[round], client.OwnerClientId, round);
                yield return new WaitForSeconds(0.3f);
            }
        }

        yield return new WaitForSeconds(0.2f);

        foreach (var card in holeCardsViews[playerId])
        {
            card.SetCard(card.GetCard());
        }
    }

    private IEnumerator SpawnCard(Card cardLogic)
    {
        CardView cardView = Instantiate(cardPrefab, commCardsContainer);
        roundObjects.Add(cardView.gameObject);
        cardView.SetCard(cardLogic);
        yield return new WaitForSeconds(0.5f);
    }

    private void SpawnHoleCard(Card cardLogic, ulong playerId, int round)
    {
        CardView cardView = Instantiate(cardPrefab, holeCardsContainer);
        roundObjects.Add(cardView.gameObject);

        if (cardLogic != null)
            cardView.SetCard(cardLogic);

        cardView.ShowCardBack(); // show back to other clients

        if (!holeCardsViews.ContainsKey(playerId))
            holeCardsViews[playerId] = new List<CardView>();

        holeCardsViews[playerId].Add(cardView);

        int uiIndex = GetUIIndex(playerId);

        RectTransform rt = cardView.GetComponent<RectTransform>();
        Vector2 anchor = GetPlayerAnchor(uiIndex);

        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        SetCardPosition(rt, uiIndex, round);
    }

    private void MovePlayerUI(ulong playerId, int uiIndex)
    {
        foreach (var pair in holeCardsViews)
        {
            Debug.Log($"{pair.Key} ---- {pair.Value}");
        }

        if (holeCardsViews.TryGetValue(playerId, out var cards))
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                RectTransform rt = card.GetComponent<RectTransform>();

                Vector2 anchor = GetPlayerAnchor(uiIndex);
                rt.anchorMin = anchor;
                rt.anchorMax = anchor;
                rt.pivot = new Vector2(0.5f, 0.5f);

                SetCardPosition(rt, uiIndex, i);
            }
        }

        // Update bet box
        if (playerBetViewsById.TryGetValue(playerId, out var betView))
        {
            betView.SetBetBoxPosition(uiIndex);
        }
    }

    private void SetCardPosition(RectTransform rt, int uiIndex, int round)
    {
        float baseWidth = tableTransform.rect.width * 0.05f;
        float cardWidth = Mathf.Clamp(baseWidth, 60f, 120f);
        float aspect = 3.5f / 2.5f;
        rt.sizeDelta = new Vector2(cardWidth, cardWidth * aspect);

        Vector2 offset = new Vector2(round * (rt.sizeDelta.x + 5), 0);
        if (uiIndex == 0) offset = new Vector2(round * -(rt.sizeDelta.x + 5), 0);
        if (uiIndex == 1 || uiIndex == 4)
        {
            rt.localEulerAngles = new Vector3(0, 0, -90);
            offset = (uiIndex == 4)
                ? new Vector2(0, round * -(rt.sizeDelta.x + 5))
                : new Vector2(0, round * (rt.sizeDelta.x + 5));
        }

        rt.anchoredPosition = offset;
    }

    public void OnTurnChanged(ulong activePlayerId, int highestBet, int lastBetAmount)
    {
        currentPlayer = serverPlayers.FirstOrDefault(p => p.OwnerClientId == activePlayerId);
        myPlayer = serverPlayers.FirstOrDefault(p => p.OwnerClientId == NetworkManager.Singleton.LocalClientId);

        this.lastBetAmount = lastBetAmount;

        HighlightActivePlayer(currentPlayer);

        if (NetworkManager.Singleton.LocalClientId != currentPlayer.OwnerClientId)
            pokerUI.EnableBetButtons(false);
        else
            pokerUI.EnableBetButtons(true);
    }

    public void ChangeCallBtnText(int callAmount)
    {
        if (callAmount > 0)
            pokerUI.SetCallBtnText("Call");
        else
            pokerUI.SetCallBtnText("Check");
    }

    public void ShowWinner(ulong playerId, int amountWon)
    {
        foreach (var playerView in playerViewsById.Values)
        {
            playerView.SetHighlight(false);
        }

        if (playerViewsById.TryGetValue(playerId, out var view))
        {
            view.SetWinner(true, amountWon);
            view.SetHighlight(true);
        }
    }

    public IEnumerator ShowGameResult(ulong playerId, int amountWon)
    {
        if (amountWon != 0)
            ShowWinner(playerId, amountWon);
        else
        {
            int amountLost = serverPlayers.Where(p => p.OwnerClientId == playerId).Select(p => p.TotalBetAmount).FirstOrDefault();
            playerViewsById[playerId].SetLooser(true, amountLost);
        }

        yield return new WaitForSeconds(15f);

        ClearGameResult();
    }

    public void ClearGameResult()
    {
        foreach (var view in playerViewsById.Values)
        {
            if (view.IsWinnerView())
            {
                view.SetWinner(false);
                view.SetHighlight(false);
            }
            else
            {
                view.SetLooser(false);
            }
        }
    }

    #endregion

    #region UI & Player Binding

    public void InitUI()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            ShowStartMenu();
        }
        else
        {
            ShowBetMenu();
        }
    }

    private void ShowStartMenu()
    {
        pokerUI.ShowStartMenu(
            onStart: () =>
            {
                bool IsEnoughPlayers = NetworkGameServer.Instance.game.Players.Count(p => p != null) > 1;

                if (NetworkManager.Singleton.IsServer && IsEnoughPlayers)
                {
                    NetworkGameServer.Instance.StartGame();
                    ShowBetMenu();
                    pokerUI.EnableBetButtons(false);
                }
                else
                    Debug.LogWarning("[GameManager]: Not enough players to start the game!");
            },
            onQuit: () =>
            {
                NetworkPokerBridge.Instance.QuitGameServerRpc();
            }
        );
    }

    private void ShowBetMenu()
    {
        pokerUI.ShowBetMenu(
            onCall: () =>
            {
                actionSentTime = (float)NetworkManager.Singleton.ServerTime.Time;
                NetworkPokerBridge.Instance.SendPlayerActionServerRpc("Call");
            },

            onFold: () =>
            {
                NetworkPokerBridge.Instance.SendPlayerActionServerRpc("Fold");
            },

            onRaise: () =>
            {
                ShowRiseMenu();
            },

            onQuit: () =>
            {
                NetworkPokerBridge.Instance.QuitGameServerRpc();
            }

        );
    }

    private void ShowRiseMenu()
    {
        if (currentPlayer.Name.IsEmpty) return;

        int minBet = lastBetAmount + bigBlindAmount;
        int maxBet = currentPlayer.Chips + currentPlayer.BetAmount;
        int potSize = this.potSize;

        pokerUI.ShowRiseMenu(
            minBet,
            maxBet,
            potSize,
            onConfirm: raiseValue =>
            {
                if (currentPlayer.Name.IsEmpty) return;

                // Send raise action to the server
                NetworkPokerBridge.Instance.SendPlayerActionServerRpc("Rise", raiseValue);

                // Return to main betting menu
                ShowBetMenu();
            },
            onBack: () =>
            {
                // Go back to main betting menu without sending anything
                ShowBetMenu();
            });
    }

    public IEnumerator ShowBlindsUI(int smallBlindId, int bigBlindId)
    {
        //small blind Player
        var playerInfo = serverPlayers.FirstOrDefault(p => p.OwnerClientId == (ulong)smallBlindId);
        int potSize = playerInfo.BetAmount;

        ShowPlayerBet(playerInfo.OwnerClientId);
        potManager.SetPotSize(potSize);

        yield return new WaitForSeconds(1f);

        //big blind Player
        playerInfo = serverPlayers.FirstOrDefault(p => p.OwnerClientId == (ulong)bigBlindId);

        ShowPlayerBet(playerInfo.OwnerClientId);
        potManager.SetPotSize(potSize + playerInfo.BetAmount);

        yield return new WaitForSeconds(1f);
    }

    public void ShowPlayerBet(ulong playerId)
    {
        var playerInfo = uiPlayers.FirstOrDefault(p => p.OwnerClientId == playerId);

        foreach (var player in uiPlayers)

        if (!playerBetViewsById.TryGetValue(playerInfo.OwnerClientId, out var betView))
        {
            VisualElement betBox = betBoxTemplate.Instantiate();
            betView = new PlayerBetView(uiDocument.rootVisualElement, playerInfo, betBox);
            betView.ShowPlayerBet();
            playerBetViewsById.Add(playerInfo.OwnerClientId, betView);
        }
        else
        {
            betView.SetBetBoxActive(true);
            betView.UpdatePlayerBet(playerInfo);
        }
    }

    public void ClearAllBets(float delay = 1f)
    {
        StartCoroutine(ClearBets(delay));
    }

    private IEnumerator ClearBets(float delay)
    {
        // Wait for the delay so players can see the last bet
        yield return new WaitForSeconds(delay);

        potManager.ClearCurrentRoundPot();

        // Remove all bet views
        foreach (var pair in playerBetViewsById)
        {
            pair.Value.SetBetBoxActive(false);
        }
    }

    public void AddToPot(int amount)
    {
        potManager.AddToPot(amount);
    }

    public void SetPotLabelText(string txt)
    {
        potManager.SetPotLabelText(txt);
    }

    public void ShowTotalPotSize(bool state)
    {
        if (state)
            potManager.ShowTotalPotSize();
        else
            potManager.HideTotalPotSize();
    }

    public void GetPotSize(int potSize)
    {
        this.potSize = potSize;
    }

    public void SetFoldedPlayerBox()
    {
        UpdateCurrentPlayerInfo();

        if (currentPlayer.HasFolded != true) return;

        if (playerViewsById.TryGetValue(currentPlayer.OwnerClientId, out var view))
        {
            view.SetHighlight(false);
            view.SetInactive(true);
        }
    }

    public IEnumerator StartShowdown(ulong playerId)
    {
        var player = serverPlayers.FirstOrDefault(p => p.OwnerClientId == playerId);

        if (holeCardsViews.TryGetValue(playerId, out var holeCards))
        {
            if (!playersShownCards.Contains(playerId))
            {
                foreach (var card in holeCards)
                    card.SetCard(card.GetCard());
            }

            if (playerViewsById.TryGetValue(playerId, out var view))
            {
                view.SetHighlight(true);
                view.SetHandRankLabel(true);
            }

            playersShownCards.Add(playerId);
        }

        yield return null;
    }

    public void UpdatePlayerCardView(ulong playerId, List<Card> holeCards)
    {
        for (int i = 0; i < holeCards.Count; i++)
        {
            holeCardsViews[playerId][i].card = holeCards[i];
        }
    }

    public void ClearRound()
    {
        foreach (var view in playerViewsById.Values)
            view.ClearView();

        foreach (var view in playerBetViewsById.Values)
            view.RemoveBetBox();

        foreach (var obj in roundObjects)
            Destroy(obj);

        roundObjects.Clear();
        CurrentCommunityCards.Clear();
        holeCardsViews.Clear();
        playerBetViewsById.Clear();
        potManager.SetPotLabelText("$0");
    }

    public void ClearPlayer(ulong playerId)
    {
        if (NetworkManager.Singleton.LocalClientId == playerId)
            return;

        int uiIndex = GetUIIndex(playerId);
        VisualElement panel = uiDocument.rootVisualElement.Q<VisualElement>($"player{uiIndex + 1}_box");

        //Remove Player panel
        if (playerViewsById.TryGetValue(playerId, out var oldView))
        {
            var oldPanel = oldView.GetPlayerBoxPanel();
            if (oldPanel != null && oldPanel.parent != null)
                oldPanel.RemoveFromHierarchy();
            playerViewsById.Remove(playerId);
        }

        panel.Clear();

        // Remove bets
        if (playerBetViewsById.ContainsKey(playerId))
        {
            playerBetViewsById[playerId].RemoveBetBox();
            playerBetViewsById.Remove(playerId);
        }

        // Remove hole cards
        if (holeCardsViews.TryGetValue(playerId, out var cards))
        {
            foreach (var card in cards)
            {
                Destroy(card.gameObject);
            }

            holeCardsViews.Remove(playerId);
            holeCardsViews.Clear();
        }
    }

    public void QuitToMainMenu(bool hostLeft)
    {
        ResetGameUI();

        uiDocument.gameObject.SetActive(false);

        var startScreenManager = FindAnyObjectByType<StartScreenManager>();
        if (startScreenManager == null)
            return;

        startScreenManager.GameMenu.gameObject.SetActive(true);
        startScreenManager.InitUI();
        startScreenManager.ShowMainMenu();

        if (hostLeft)
            startScreenManager.SetInfoLabelMessage("Host has left the game");
    }

    public void ResetGameUI()
    {
        StopAllCoroutines();

        // Clear round visuals
        foreach (var obj in roundObjects)
            if (obj != null) Destroy(obj);

        roundObjects.Clear();
        CurrentCommunityCards.Clear();
        playersShownCards.Clear();

        // Clear player UI
        foreach (var view in playerViewsById.Values)
            view.ClearView();

        playerViewsById.Clear();
        playerBetViewsById.Clear();
        holeCardsViews.Clear();

        serverPlayers = Array.Empty<PlayerInfo>();
        uiPlayers = Array.Empty<PlayerInfo>();

        currentDealerButton = null;

        potManager.SetPotLabelText("$0");
    }

    public void ShowDealerButton(ulong dealerId)
    {
        if (currentDealerButton != null)
            Destroy(currentDealerButton.gameObject);

        currentDealerButton = Instantiate(dealerButtonPrefab, dealerButtonContainer);
        roundObjects.Add(currentDealerButton.gameObject);
        RectTransform rt = currentDealerButton.GetComponent<RectTransform>();

        ulong playerId = serverPlayers.Where(p => p.OwnerClientId == dealerId).Select(p => p.OwnerClientId).FirstOrDefault();
        int uiIndex = GetUIIndex(playerId);

        // Get normalized (0–1) position on the table
        Vector2 anchor = GetPlayerAnchor(uiIndex);

        // Convert normalized position to pixel coordinates relative to the table
        Vector2 localPos = new Vector2(
            (anchor.x - 0.5f) * tableTransform.rect.width,
            (anchor.y - 0.5f) * tableTransform.rect.height
        );

        // Add small offset (optional)
        Vector2 offset = Vector2.zero;
        switch (uiIndex)
        {
            case 0: offset = new Vector2(55, 0); break;
            case 1: offset = new Vector2(0, -55); break;
            case 4: offset = new Vector2(0, 55); break;
            default: offset = new Vector2(-55, 0); break;
        }

        localPos += offset;

        // Set position relative to the table transform
        rt.SetParent(tableTransform, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = localPos;

        // Rotation for each seat (optional)
        switch (uiIndex)
        {
            case 0: rt.localEulerAngles = Vector3.zero; break;
            case 1: rt.localEulerAngles = new Vector3(0, 0, -90); break;
            case 4: rt.localEulerAngles = new Vector3(0, 0, -270); break;
            default: rt.localEulerAngles = new Vector3(0, 0, -180); break;
        }
    }

    private void HighlightActivePlayer(PlayerInfo playerInfo)
    {
        foreach (var view in playerViewsById.Values)
            view.SetHighlight(false);

        if (playerInfo.Name.IsEmpty)
            return;

        if (playerViewsById.TryGetValue(playerInfo.OwnerClientId, out var activeView))
        {
            activeView.SetHighlight(true);
        }
    }

    private Vector2 GetPlayerAnchor(int uiIndex)
    {
        return uiIndex switch
        {
            0 => new Vector2(0.53f, 0.165f),
            1 => new Vector2(0.09f, 0.45f),
            2 => new Vector2(0.27f, 0.835f),
            3 => new Vector2(0.675f, 0.835f),
            4 => new Vector2(0.91f, 0.55f),
            _ => new Vector2(0.5f, 0.5f)
        };
    }

    private int GetUIIndex(ulong playerId)
    {
        int index = Array.FindIndex(uiPlayers, p => p.OwnerClientId == playerId);
        return index >= 0 ? index : 0;
    }

    private PlayerInfo UpdateCurrentPlayerInfo() => currentPlayer = serverPlayers.FirstOrDefault(p => p.OwnerClientId == currentPlayer.OwnerClientId);

    #endregion
}
