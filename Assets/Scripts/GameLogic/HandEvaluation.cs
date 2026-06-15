using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class HandEvaluation
{
    private static readonly string[] HandNames =
    {
    "High Card",        // 0
    "Pair",             // 1
    "Two Pairs",        // 2
    "Three of a Kind",  // 3
    "Straight",         // 4
    "Flush",            // 5
    "Full House",       // 6
    "Four of a Kind",   // 7
    "Straight Flush"    // 8
    };

    private Dictionary<Rank, int> HandFrequencies = new();

    public Int32 EvaluateHand(Card[] hand)
    {
        int evaluationResult = IsStraightFlush(hand);

        if (evaluationResult >> 8 == 8) return evaluationResult;
        evaluationResult = IsFourOfAKind(hand);
        if (evaluationResult >> 8 == 7) return evaluationResult;
        evaluationResult = IsFullHouse(hand);
        if (evaluationResult >> 8 == 6) return evaluationResult;
        evaluationResult = IsFlush(hand);
        if (evaluationResult >> 8 == 5) return evaluationResult;
        evaluationResult = IsStraight(hand);
        if (evaluationResult >> 8 == 4) return evaluationResult;
        evaluationResult = IsThreeOfAKind(hand);
        if (evaluationResult >> 8 == 3) return evaluationResult;
        evaluationResult = IsTwoPair(hand);
        if (evaluationResult >> 8 == 2) return evaluationResult;
        evaluationResult = IsPair(hand);
        if (evaluationResult >> 8 == 1) return evaluationResult;

        return 0;
    }

    public string GetHandName(int encodedValue)
    {
        int rank = encodedValue >> 24;
        return rank >= 0 && rank < HandNames.Length ? HandNames[rank] : "Unknown";
    }

    public Int32 GetHandValue(Card[] hand)
    {
        hand = hand.Where(card => card != null).ToArray();

        int handValue = 0x00000;
        int handEvaluationResult = EvaluateHand(hand);
        int handType = handEvaluationResult >> 8;

        int rankOfHandType = handEvaluationResult & 0xFF;

        handValue |= (rankOfHandType & 0xF) << 16 | (rankOfHandType >> 4) << 20;
        handValue |= handType << 24;

        if (handType == 4 || handType == 5 || handType == 8)
            return handValue;

        Array.Sort(hand, (x, y) => x.Rank.CompareTo(y.Rank));
        var distinctHandRanks = hand.Select(c => c.Rank).Distinct().ToArray();
        var kickerCards = distinctHandRanks.Where(rank => (int)rank != (rankOfHandType >> 4) && (int)rank != (rankOfHandType & 0xF)).Select(rank => rank).ToArray();

        for (int i = 0; i < kickerCards.Length; i++)
        {
            handValue += (Int32)kickerCards[i] << (i * 4);
        }

        return handValue;
    }

    public Int32 GetBestHandValue(Card[] hand)
    {
        HandFrequencies = GetFrequencies(hand);

        var cardCombos = GetBestCardCombo(hand);

        int bestHandValue = int.MinValue;

        foreach (var combo in cardCombos)
        {
            int currentBestHand = GetHandValue(combo);
            if (currentBestHand > bestHandValue)
                bestHandValue = currentBestHand;
        }

        return bestHandValue;
    }

    private List<Card[]> GetBestCardCombo(Card[] hand)
    {
        var combinations = new List<Card[]>();

        if (hand.Count(c => c != null) < 5)
        {
            combinations.Add(hand);
            return combinations;
        }

        for (int c1 = 0; c1 < hand.Length - 4; c1++)
            for (int c2 = c1 + 1; c2 < hand.Length - 3; c2++)
                for (int c3 = c2 + 1; c3 < hand.Length - 2; c3++)
                    for (int c4 = c3 + 1; c4 < hand.Length - 1; c4++)
                        for (int c5 = c4 + 1; c5 < hand.Length; c5++)
                        {
                            combinations.Add(new[] { hand[c1], hand[c2], hand[c3], hand[c4], hand[c5] });
                        }

        return combinations;
    }

    private Dictionary<Rank, int> GetFrequencies(Card[] hand)
    {
        var cardFreq = new Dictionary<Rank, int>();
        foreach (var card in hand)
        {
            if (card == null)
                continue;

            if (!cardFreq.ContainsKey(card.Rank))
                cardFreq[card.Rank] = 0;
            cardFreq[card.Rank]++;
        }

        return cardFreq;
    }

    public Int32 IsPair(Card[] hand)
    {
        if (hand.Count(c => c != null) < 2) return 0;

        var rankCount = HandFrequencies;

        int pairCount = rankCount.Values.Count(v => v == 2);
        int pairValue = rankCount.Where(c => c.Value == 2).Select(c => (int)c.Key).FirstOrDefault();

        if (pairCount == 1 && !rankCount.Values.Any(v => v > 2))
            return (1 << 8) + (pairValue << 4);

        return 0;
    }

    public Int32 IsTwoPair(Card[] hand)
    {
        if (hand.Count(c => c != null) < 4) return 0;

        var rankCount = HandFrequencies;

        int pairCount = rankCount.Values.Count(v => v == 2);
        int[] pairValues = rankCount.Where(c => c.Value == 2).Select(c => (int)c.Key).ToArray();
        Array.Sort(pairValues);

        if (pairCount == 2 && !rankCount.Values.Any(v => v > 2))
            return (pairValues[0] & 0xF) | ((pairValues[1] & 0xF) << 4) | (2 << 8);

        return 0;
    }

    public Int32 IsThreeOfAKind(Card[] hand)
    {
        if (hand.Count(c => c != null) < 3) return 0;

        var rankCount = HandFrequencies;

        bool hasThreeOfAKind = rankCount.Values.Any(v => v == 3);
        bool hasNoPairOrFourOfAKind = !rankCount.Values.Any(v => v == 2 || v == 4);
        int threeOfAKindValue = rankCount.Where(c => c.Value == 3).Select(c => (int)c.Key).FirstOrDefault();

        if (hasThreeOfAKind && hasNoPairOrFourOfAKind)
            return threeOfAKindValue + (3 << 8);

        return 0;
    }

    public Int32 IsFullHouse(Card[] hand)
    {
        if (hand.Count(c => c != null) < 5) return 0;

        var rankCount = HandFrequencies;

        int threeOfAKindCount = rankCount.Values.Count(v => v == 3);
        int threeOfAKindValue = rankCount.Where(c => c.Value == 3).Select(c => (int)c.Key).FirstOrDefault();
        int pairCount = rankCount.Values.Count(v => v == 2);
        int pairValue = rankCount.Where(c => c.Value == 2).Select(c => (int)c.Key).FirstOrDefault();

        if (threeOfAKindCount >= 1 && (pairCount >= 1 || threeOfAKindCount >= 2))
            return (pairValue & 0xF) | ((threeOfAKindValue & 0xF) << 4) | (6 << 8); ;

        return 0;
    }

    public Int32 IsFourOfAKind(Card[] hand)
    {
        if (hand.Count(c => c != null) < 4) return 0;

        var rankCount = HandFrequencies;

        bool hasFourOfAKind = rankCount.Values.Any(v => v == 4);
        int fourOfAKindValue = rankCount.Where(c => c.Value == 4).Select(c => (int)c.Key).FirstOrDefault();

        if (hasFourOfAKind)
            return fourOfAKindValue + (7 << 8);

        return 0;
    }

    public Int32 IsFlush(Card[] hand)
    {
        if (hand.Count(c => c != null) < 5) return 0;

        var groupsBySuit = hand.GroupBy(card => card.Suit);

        foreach (var group in groupsBySuit)
        {
            var suitedCards = group.ToArray();

            if (suitedCards.Length >= 5)
            {
                var flushRanks = group
                .OrderByDescending(card => (int)card.Rank)
                .Take(5)
                .ToArray();

                return (int)flushRanks[0].Rank + (5 << 8);
            }
        }

        return 0;
    }

    public Int32 IsStraight(Card[] hand)
    {
        if (hand.Count(c => c != null) < 5) return 0;

        var ranks = hand.Select(card => (int)card.Rank).Distinct().ToList();

        if (ranks.Contains(14))
            ranks.Add(1);

        ranks.Sort();
        int straightValue = 0;

        for (int i = 4; i < ranks.Count; i++)
        {
            if (ranks[i] - ranks[i - 4] == 4 && straightValue < ranks[i])
                straightValue = ranks[i];
        }

        if (straightValue > 0)
            return straightValue + (4 << 8);

        return 0;
    }


    public Int32 IsStraightFlush(Card[] hand)
    {
        if (hand.Count(c => c != null) < 5) return 0;

        var groupsBySuit = hand.GroupBy(card => card?.Suit);
        int straightValue = 0;

        foreach (var group in groupsBySuit)
        {
            var suitedCards = group.ToArray();
            if (suitedCards.Length < 5)
                continue;

            var ranks = suitedCards.Select(card => (int)card.Rank).Distinct().ToList();

            if (ranks.Contains(14))
                ranks.Add(1);

            ranks.Sort();

            for (int i = 4; i < ranks.Count; i++)
            {
                if (ranks[i] - ranks[i - 4] == 4 && straightValue < ranks[i])
                    straightValue = ranks[i];
            }
        }

        if (straightValue > 0)
            return straightValue + (8 << 8);

        return 0;
    }

}
