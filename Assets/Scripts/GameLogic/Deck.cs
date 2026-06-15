using UnityEngine;
using System;
using System.Collections.Generic;
using Random = System.Random;
using System.Linq;
public class Deck
{
    private List<Card> cards;
    private Random rand = new Random();
    public List<Card> GetCards() => cards;
    public int Count => cards.Count;
    public Deck()
    {
        cards = new List<Card>();
    }

    public void AddCards()
    {
        cards.Clear();

        foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                cards.Add(new Card(rank, suit));
    }

    public void Shuffle()
    {
        cards = cards.OrderBy(_ => rand.Next()).ToList();
    }

    public Card Draw()
    {
        if (cards.Count == 0)
        {
            return null;
        } 
        var card = cards[0];
        cards.RemoveAt(0);
        return card;
    }

    public Card DrawSpecific(Rank rank, Suit suit)
    {
        return new Card(rank, suit);
    }
}