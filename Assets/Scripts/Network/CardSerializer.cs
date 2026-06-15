using System.Collections.Generic;

public static class CardSerializer
{
    public static byte[] ToBytes(List<Card> cards)
    {
        var bytes = new byte[cards.Count * 2];
        for (int i = 0; i < cards.Count; i++)
        {
            bytes[i * 2] = (byte)cards[i].Rank;
            bytes[i * 2 + 1] = (byte)cards[i].Suit;
        }
        return bytes;
    }

    public static List<Card> FromBytes(byte[] bytes)
    {
        var cards = new List<Card>(bytes.Length / 2);
        for (int i = 0; i < bytes.Length; i += 2)
        {
            Rank rank = (Rank)bytes[i];
            Suit suit = (Suit)bytes[i + 1];
            cards.Add(new Card(rank, suit));
        }
        return cards;
    }
}
