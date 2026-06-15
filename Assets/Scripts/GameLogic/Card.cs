using System;
using UnityEngine;

public enum Suit
{
    Hearts,
    Spades,
    Diamonds,
    Clubs
}

public enum Rank
{
    Two = 2,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King,
    Ace
}

public class Card
{
    public Suit Suit { get; private set; }
    public Rank Rank { get; private set; }

    public Card(Rank rank, Suit suit)
    {
        Rank = rank;
        Suit = suit;
    }

    public override bool Equals(object obj)
    {
        if (obj is Card other)
            return this.Rank == other.Rank && this.Suit == other.Suit;

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Rank, Suit);
    }

    public override string ToString()
    {
        return $"{Rank} of {Suit}";
    }

    public static explicit operator int(Card v)
    {
        throw new NotImplementedException();
    }
}
