using UnityEngine;
using UnityEngine.UIElements;

public class PlayerBetView
{
    private readonly GroupBox pokerTable;
    private Label betBoxLabel;
    private VisualElement betBox;
    private PlayerInfo playerInfo;

    public PlayerBetView(VisualElement root, PlayerInfo playerInfo, VisualElement betBox)
    {
        pokerTable = root.Q<GroupBox>("pokerTable");
        this.playerInfo = playerInfo;
        this.betBox = betBox;
    }

    public void ShowPlayerBet()
    {
        // Clone the template
        if (betBox == null)
        {
            return;
        }
        betBoxLabel = betBox.Q<Label>("player_bet");

        // Add to parent container
        pokerTable.Add(betBox);

        // Set bet text
        if (!playerInfo.HasChecked && !playerInfo.HasFolded && !playerInfo.IsAllIn)
            betBoxLabel.text = $"${playerInfo.BetAmount}";
        else if (playerInfo.HasChecked)
            betBoxLabel.text = "check";
        else if (playerInfo.HasFolded)
            betBoxLabel.text = "fold";
        else if (playerInfo.IsAllIn)
            betBoxLabel.text = "All-in";

        SetBetBoxPosition(playerInfo.SeatIndex);
    }

    public void UpdatePlayerBet(PlayerInfo newInfo)
    {
        playerInfo = newInfo;

        if (!playerInfo.HasChecked && !playerInfo.HasFolded && !playerInfo.IsAllIn)
            betBoxLabel.text = $"${playerInfo.BetAmount}";
        else if (playerInfo.HasChecked)
            betBoxLabel.text = "check";
        else if (playerInfo.HasFolded)
            betBoxLabel.text = "fold";
        else if (playerInfo.IsAllIn)
            betBoxLabel.text = "All-in";
        else
            return;
    }

    public void SetBetBoxActive(bool active)
    {
        if (betBox == null) return;
        betBox.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void RemoveBetBox()
    {
        pokerTable.Remove(betBox);
    }

    public void SetBetBoxPosition(int seatIndex)
    {
        betBoxLabel.style.position = Position.Absolute;
        betBoxLabel.style.alignSelf = Align.FlexStart;

        int pokerTableWidth = Mathf.RoundToInt(pokerTable.resolvedStyle.width);
        int pokerTableHeight = Mathf.RoundToInt(pokerTable.resolvedStyle.height);

        switch (seatIndex)
        {
            case 0:
                betBoxLabel.style.top = new Length(pokerTableHeight * 0.655f);
                betBoxLabel.style.left = new Length(pokerTableWidth * 0.565f);
                break;
            case 1:
                betBoxLabel.style.top = new Length(pokerTableHeight * 0.56f);
                betBoxLabel.style.left = new Length(pokerTableWidth * 0.11f);
                break;
            case 2:
                betBoxLabel.style.top = new Length(pokerTableHeight * 0.15f);
                betBoxLabel.style.left = new Length(pokerTableWidth * 0.21f);
                break;
            case 3:
                betBoxLabel.style.top = new Length(pokerTableHeight * 0.15f);
                betBoxLabel.style.left = new Length(pokerTableWidth * 0.57f);
                break;
            case 4:
                betBoxLabel.style.top = new Length(pokerTableHeight * 0.25f);
                betBoxLabel.style.left = new Length(pokerTableWidth * 0.845f);
                break;
        }
    }
}
