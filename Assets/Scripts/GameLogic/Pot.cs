using System;
using System.Collections.Generic;
public class Pot
{
    public event Action<int> OnPotChanged;
    public int Chips = 0;
    public List<Player> EligiblePlayers;

    public void AddToPot(int amount)
    {
        Chips += amount;
        OnPotChanged?.Invoke(Chips);
    }
}

