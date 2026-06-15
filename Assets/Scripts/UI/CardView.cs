using UnityEngine;
using UnityEngine.UI;

public class CardView : MonoBehaviour
{
    private Image image;
    public Card card;
    private static Sprite[] allSprites;

    [SerializeField] private Sprite backSprite;

    void Awake()
    {
        image = GetComponent<Image>();

        if (allSprites == null)
            allSprites = Resources.LoadAll<Sprite>("card_deck/cards");
    }

    public void SetCard(Card newCard)
    {
        card = newCard;
        image.sprite = GetFrontSprite(card);
    }

    public void ShowCardBack()
    {
        image.sprite = backSprite;
    }

    private Sprite GetFrontSprite(Card card)
    {
        int index = (int)card.Suit * 13 + ((int)card.Rank - 2);
        return allSprites[index];
    }

    public Card GetCard() => card;
}
