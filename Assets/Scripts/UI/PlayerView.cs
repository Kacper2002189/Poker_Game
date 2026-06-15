using UnityEngine;
using UnityEngine.UIElements;

public class PlayerView : MonoBehaviour
{
    private Label nameLabel;
    private Label chipsLabel;
    private Label handRankLabel;
    public Label chipsNegativeBalanceLabel;
    public Label chipsPositiveBalanceLabel;
    private GroupBox playerBox;
    private GroupBox playerContainerContent;
    private VisualElement winnerCup;

    private PlayerInfo playerInfo;
    private VisualElement panel;

    private bool playerHighlightState = false;

    public PlayerView(VisualElement root, PlayerInfo playerInfo)
    {
        this.playerInfo = playerInfo;
        this.panel = root;

        // Player container and elements
        playerContainerContent = root.Q<GroupBox>("player_container_content");
        playerBox = root.Q<GroupBox>("player_box");
        nameLabel = root.Q<Label>("player_name");
        chipsLabel = root.Q<Label>("player_chips");
        handRankLabel = root.Q<Label>("handrank_label");
        chipsNegativeBalanceLabel = root.Q<Label>("chips_negative_balance");
        chipsPositiveBalanceLabel = root.Q<Label>("chips_positive_balance");
        winnerCup = root.Q<VisualElement>("winner_cup");

        Refresh();
    }

    public void UpdateFromInfo(PlayerInfo newInfo)
    {
        playerInfo = newInfo;
        Refresh();
    }

    public void Refresh()
    {
        // No null or empty string checks — just bind directly
        if (nameLabel != null)
            nameLabel.text = playerInfo.Name.ToString();

        if (chipsLabel != null)
            chipsLabel.text = $"Chips: {playerInfo.Chips}";

        if (handRankLabel != null)
        {
            handRankLabel.text = playerInfo.HandName.ToString();
        }
    }

    public void SetHighlight(bool active)
    {
        if (playerBox == null) return;

        if (active && !playerHighlightState)
        {
            playerBox.AddToClassList("box_highlight");
            playerHighlightState = true;
        }
        else
        {
            playerBox.RemoveFromClassList("box_highlight");
            playerHighlightState = false;
        }
    }

    public void SetWinner(bool active, int amountWon = 0)
    {
        if (winnerCup == null || playerBox == null) return;

        if (active)
        {
            winnerCup.style.display = DisplayStyle.Flex;
            playerBox.AddToClassList("winner_highlight");
            chipsPositiveBalanceLabel.style.display = DisplayStyle.Flex;
            chipsPositiveBalanceLabel.text = $"+{amountWon}";
        }
        else
        {
            winnerCup.style.display = DisplayStyle.None;
            playerBox.RemoveFromClassList("winner_highlight");
            chipsPositiveBalanceLabel.style.display = DisplayStyle.None;
        }
    }

    public bool IsWinnerView()
    {
        if (winnerCup.style.display == DisplayStyle.Flex)
            return true;
        else
            return false;
    }

    public void SetLooser(bool active, int amountLost = 0)
    {
        if (playerBox == null) return;

        if (active && amountLost != 0)
        {
            chipsNegativeBalanceLabel.style.display = DisplayStyle.Flex;
            chipsNegativeBalanceLabel.text = $"-{amountLost}";
        }
        else
        {
            chipsNegativeBalanceLabel.style.display = DisplayStyle.None;
        }
    }

    public void SetInactive(bool state)
    {
        if (playerContainerContent != null)
            playerContainerContent.style.opacity = state ? 0.15f : 1f;
    }

    public void SetHandRankLabel(bool state)
    {
        if (handRankLabel == null && handRankLabel.text == string.Empty) return;

        handRankLabel.text = playerInfo.HandName.ToString();
        handRankLabel.style.display = state ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void ClearView()
    {
        if (playerContainerContent == null || winnerCup == null || playerBox == null) return;

        if (playerContainerContent.style.opacity != 1f)
            SetInactive(false);

        if (winnerCup.style.display != DisplayStyle.None && chipsPositiveBalanceLabel.style.display != DisplayStyle.None)
            SetWinner(false);

        if (chipsNegativeBalanceLabel.style.display != DisplayStyle.None)
            SetLooser(false);

        if (playerBox.ClassListContains("box_highlight"))
            SetHighlight(false);

        if (handRankLabel.style.display != DisplayStyle.None)
            SetHandRankLabel(false);
    }

    public VisualElement GetPlayerBoxPanel()
    {
        return panel;
    }
}
