using System.Collections;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class StartScreenManager : MonoBehaviour
{
    [SerializeField] private UIDocument MainMenu;
    [SerializeField] private GameObject gameUIObject;
    [SerializeField] private UIDocument clientWaitOverlay;
    [SerializeField] private NetworkGameServer serverPrefab;
    [SerializeField] private NetworkPokerBridge pokerBridgePrefab;

    private Button hostButton, joinButton, startHostBtn, joinGameBtn, backFromHostBtn, backFromJoinBtn;
    private VisualElement mainMenu, hostMenu, joinMenu;
    private Label gameCodeLabel, infoLabel;
    private TextField joinGameCode, playerUsername;
    private IntegerField playerChips;
    private Allocation currentHostAllocation;

    public UIDocument ClientWaitOverlay => clientWaitOverlay;
    public UIDocument GameMenu => MainMenu;

    public string playerName = "Player";
    public int playerChipsAmount = 1000;

    private void Awake()
    {
        InitUI();

        // Initially show main menu
        ShowMenu(mainMenu);
    }

    public void InitUI()
    {
        var root = MainMenu.rootVisualElement;

        // Query menus
        mainMenu = root.Q<VisualElement>("MainMenu");
        hostMenu = root.Q<VisualElement>("HostMenu");
        joinMenu = root.Q<VisualElement>("JoinMenu");

        //Query buttons
        hostButton = root.Q<Button>("hostBtn");
        joinButton = root.Q<Button>("joinBtn");
        startHostBtn = root.Q<Button>("startHostBtn");
        joinGameBtn = root.Q<Button>("joinGameBtn");
        backFromHostBtn = root.Q<Button>("backFromHostBtn");
        backFromJoinBtn = root.Q<Button>("backFromJoinBtn");

        // Query other UI elements
        gameCodeLabel = root.Q<Label>("GameCode");
        infoLabel = root.Q<Label>("infoLabel");
        joinGameCode = root.Q<TextField>("JoinGameCode");
        playerUsername = root.Q<TextField>("playerUsername");
        playerChips = root.Q<IntegerField>("playerChips");

        hostButton.clicked -= ShowHostMenu;
        hostButton.clicked += ShowHostMenu;

        joinButton.clicked -= ShowJoinMenu;
        joinButton.clicked += ShowJoinMenu;

        startHostBtn.clicked -= OnHost;
        startHostBtn.clicked += OnHost;

        joinGameBtn.clicked -= OnJoin;
        joinGameBtn.clicked += OnJoin;

        backFromHostBtn.clicked -= () => ShowMenu(mainMenu);
        backFromHostBtn.clicked += () => ShowMenu(mainMenu);

        backFromJoinBtn.clicked -= () => ShowMenu(mainMenu);
        backFromJoinBtn.clicked += () => ShowMenu(mainMenu);
    }

    private async void ShowHostMenu()
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections: 5);

        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        gameCodeLabel.text = joinCode;

        currentHostAllocation = allocation;

        ShowMenu(hostMenu);
    }

    private void OnHost()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(currentHostAllocation, "wss"));
        transport.UseWebSockets = true;

        SetActiveRecursively(gameUIObject, true);

        NetworkManager.Singleton.StartHost();

        SpawnServerObjects();

        NetworkPokerBridge.Instance.SendPlayerInfoServerRpc(playerName, playerChipsAmount);

        MainMenu.gameObject.SetActive(false);
    }

    private void SpawnServerObjects()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        var bridge = Instantiate(pokerBridgePrefab);
        bridge.GetComponent<NetworkObject>().Spawn(true);

        var server = Instantiate(serverPrefab);
        server.GetComponent<NetworkObject>().Spawn(true);
    }

    private async void OnJoin()
    {
        string joinCode = joinGameCode.value;

        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogWarning("Please enter a join code!");
            return;
        }

        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        // Join host allocation using join code
        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        // Configure UnityTransport
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "wss"));

        // Show the main game UI
        SetActiveRecursively(gameUIObject, true);

        // Show a waiting overlay until the game starts
        clientWaitOverlay.gameObject.SetActive(true);

        NetworkManager.Singleton.StartClient();

        MainMenu.gameObject.SetActive(false);
    }

    public void SetActiveRecursively(GameObject obj, bool state)
    {
        obj.SetActive(state);
        foreach (Transform child in obj.transform)
            SetActiveRecursively(child.gameObject, state);
    }

    private void ShowJoinMenu()
    {
        ShowMenu(joinMenu);
    }

    public void ShowMainMenu()
    {
        ShowMenu(mainMenu);

        if (infoLabel.style.display == DisplayStyle.Flex)
            infoLabel.style.display = DisplayStyle.None;
    }

    public void SetInfoLabelMessage(string text)
    {
        infoLabel.text = text;
        infoLabel.style.display = DisplayStyle.Flex;
    }

    public void ShowMenu(VisualElement menuToShow)
    {
        mainMenu.style.display = (menuToShow == mainMenu) ? DisplayStyle.Flex : DisplayStyle.None;
        hostMenu.style.display = (menuToShow == hostMenu) ? DisplayStyle.Flex : DisplayStyle.None;
        joinMenu.style.display = (menuToShow == joinMenu) ? DisplayStyle.Flex : DisplayStyle.None;

        playerName = playerUsername.text == string.Empty ? playerName : playerUsername.text;
        playerChipsAmount = playerChips.value;
    }
}
