using System;
using UnityEngine;
using UnityEngine.UIElements;

public class MenuView
{
    private UIDocument uiDocument;
    private VisualElement menuContainer;

    //Menu assets
    private VisualTreeAsset startMenu;
    private VisualTreeAsset betMenu;
    private VisualTreeAsset raiseMenu;

    private VisualElement currentMenu;

    private Button callBtn, raiseBtn, foldBtn;

    public MenuView(UIDocument document)
    {
        uiDocument = document;
        menuContainer = uiDocument.rootVisualElement.Q<VisualElement>("menuContainer");

        startMenu = Resources.Load<VisualTreeAsset>("GameScreen/PlayerActionButtonsUI/start_game_menu");
        betMenu = Resources.Load<VisualTreeAsset>("GameScreen/PlayerActionButtonsUI/action_menu");
        raiseMenu = Resources.Load<VisualTreeAsset>("GameScreen/PlayerActionButtonsUI/raise_menu");
    }

    // Destroys current menu
    private void ClearMenu()
    {
        menuContainer.Clear();
        currentMenu = null;
    }

    // ---- START MENU ----
    public void ShowStartMenu(Action onStart, Action onQuit)
    {
        ClearMenu();
        currentMenu = startMenu.Instantiate();
        currentMenu.name = "start_menu";
        currentMenu.style.width = new StyleLength(Length.Percent(100));
        currentMenu.style.alignItems = Align.Center;
        menuContainer.Add(currentMenu);

        var startBtn = currentMenu.Q<Button>("start_game_button");
        var quitBtn = currentMenu.Q<Button>("quit_button");

        startBtn.clicked += onStart;
        quitBtn.clicked += onQuit;
    }

    // ---- BET MENU ----
    public void ShowBetMenu(Action onCall, Action onFold, Action onRaise, Action onQuit)
    {
        ClearMenu();
        currentMenu = betMenu.Instantiate();
        currentMenu.name = "action_menu";
        Debug.Log($"CurrentMenu name: {currentMenu.name}");
        currentMenu.style.width = new StyleLength(Length.Percent(100));
        currentMenu.style.alignItems = Align.Center;
        menuContainer.Add(currentMenu);

        callBtn = currentMenu.Q<Button>("call_button");
        foldBtn = currentMenu.Q<Button>("fold_button");
        raiseBtn = currentMenu.Q<Button>("raise_button");

        var quitBtn = currentMenu.Q<Button>("quit_button");

        callBtn.clicked += onCall;
        foldBtn.clicked += onFold;
        raiseBtn.clicked += onRaise;
        quitBtn.clicked += onQuit;

    }

    public void EnableBetButtons(bool enable)
    {
        if (currentMenu != null && currentMenu.name == "action_menu")
        {
            raiseBtn.SetEnabled(enable);
            foldBtn.SetEnabled(enable);
        }
    }

    // ---- RAISE MENU ----
    public void ShowRiseMenu(int minBet, int maxBet, int potSize, Action<int> onConfirm, Action onBack)
    {
        ClearMenu();
        currentMenu = raiseMenu.Instantiate();
        currentMenu.name = "rise_menu";
        currentMenu.style.width = new StyleLength(Length.Percent(100));
        currentMenu.style.alignItems = Align.Center;
        menuContainer.Add(currentMenu);

        var slider = currentMenu.Q<SliderInt>("raise_amount_slider");
        var label = currentMenu.Q<Label>("slider_bet_amount");
        var confirmBtn = currentMenu.Q<Button>("bet_button");
        var backBtn = currentMenu.Q<Button>("back_button");

        // Quick raise buttons
        var minBetBtn = currentMenu.Q<Button>("min_bet");
        var halfPotBtn = currentMenu.Q<Button>("half_pot");
        var threeQuarterPotBtn = currentMenu.Q<Button>("three_quarter_pot");
        var potSizeBtn = currentMenu.Q<Button>("bet_pot_size");
        var allInBtn = currentMenu.Q<Button>("all_in");

        // Adjust buttons
        var adjustLeftBtn = currentMenu.Q<Button>("adjust_slider_left");
        var adjustRightBtn = currentMenu.Q<Button>("adjust_slider_right");

        // Initialize slider
        slider.lowValue = minBet;
        slider.highValue = maxBet;
        slider.value = minBet;
        label.text = $"${slider.value}";

        slider.RegisterValueChangedCallback(evt =>
        {
            label.text = $"${evt.newValue}";
        });

        // Set quick-raise buttons
        minBetBtn.clicked += () => SetSlider(slider, label, minBet);
        halfPotBtn.clicked += () => SetSlider(slider, label, potSize / 2);
        threeQuarterPotBtn.clicked += () => SetSlider(slider, label, potSize * 3 / 4);
        potSizeBtn.clicked += () => SetSlider(slider, label, potSize);
        allInBtn.clicked += () => SetSlider(slider, label, maxBet);

        // Adjust slider buttons
        adjustLeftBtn.clicked += () => SetSlider(slider, label, slider.value - 1);
        adjustRightBtn.clicked += () => SetSlider(slider, label, slider.value + 1);

        // Confirm / Back
        if (onConfirm != null)
            confirmBtn.clicked += () => onConfirm.Invoke(slider.value);
        if (onBack != null)
            backBtn.clicked += onBack;
    }

    // Helper function to update slider and label
    private void SetSlider(SliderInt slider, Label label, int value)
    {
        slider.value = Mathf.Clamp(value, slider.lowValue, slider.highValue);
        label.text = $"${slider.value}";
    }

    public void SetCallBtnText(string txt) => callBtn.text = txt;
}
