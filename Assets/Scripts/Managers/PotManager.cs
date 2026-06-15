using UnityEngine;
using UnityEngine.UIElements;

public class PotManager : MonoBehaviour
{
    [SerializeField] private VisualTreeAsset potBox;
    [SerializeField] private VisualTreeAsset totalPotBox;
    [SerializeField] private UIDocument uiDocument;

    private VisualElement totalPot;
    private Label potLabel;
    private Label totalPotLabel;

    int currentRoundPot;
    int totalPotAmount;

    private void Awake()
    {
        if (uiDocument != null)
        {
            totalPot = uiDocument.rootVisualElement.Q<VisualElement>("total_pot_box");
            potLabel = uiDocument.rootVisualElement.Q<Label>("potLabel");
            totalPotLabel = totalPot.Q<Label>("total_pot_value");
            totalPot.style.display = DisplayStyle.None;
        }
    }

    public void AddToPot(int amount)
    {
        if (amount < 0) return;

        currentRoundPot += amount;
        totalPotAmount += amount;

        potLabel.text = $"${currentRoundPot}";
        totalPotLabel.text = $"${totalPotAmount}";
    }

    public int GetPotSize(Pot pot)
    {
        if (pot == null)
        {
            Debug.LogWarning("Pot not bound yet!");
            return 0;
        }

        return pot.Chips;
    }

    public void ShowTotalPotSize()
    {
        currentRoundPot = 0;

        totalPot.style.display = DisplayStyle.Flex;

        totalPotLabel.text = $"${totalPotAmount}";
        potLabel.text = "$0";
    }

    public void ClearCurrentRoundPot() => currentRoundPot = 0;

    public void HideTotalPotSize()
    {
        totalPot.style.display = DisplayStyle.None;
        potLabel.text = totalPotLabel.text;
        totalPotLabel.text = "$0";
    }

    public void SetPotSize(int amount)
    {
        if (potLabel == null) return;

        currentRoundPot = amount;
        totalPotAmount = amount;
        potLabel.text = $"${currentRoundPot}";
    }

    public void SetPotLabelText(string txt)
    {
        if (potLabel == null) return;

        potLabel.text = txt;
    }
}