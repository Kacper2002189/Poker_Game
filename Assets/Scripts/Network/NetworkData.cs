using System;
using Unity.Collections;
using Unity.Netcode;

[Serializable]
public struct PublicStateData : INetworkSerializable
{
    public byte[] CommunityCards; // serialized cards
    public int PotSize;
    public int CurrentRound;
    public int BigBlindAmount;

    public PlayerInfo[] Players;  // array of PlayerInfo
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        CommunityCards ??= Array.Empty<byte>();
        Players ??= Array.Empty<PlayerInfo>();

        serializer.SerializeValue(ref CommunityCards);
        serializer.SerializeValue(ref PotSize);
        serializer.SerializeValue(ref CurrentRound);
        serializer.SerializeValue(ref BigBlindAmount);

        // Serialize array length first
        int length = Players != null ? Players.Length : 0;
        serializer.SerializeValue(ref length);

        if (serializer.IsReader)
            Players = new PlayerInfo[length];

        for (int i = 0; i < length; i++)
        {
            serializer.SerializeValue(ref Players[i]);
        }
    }
}

[Serializable]
public struct PlayerInfo : INetworkSerializable
{
    public FixedString64Bytes Name;
    public FixedString64Bytes HandName;
    public int Chips;
    public ulong OwnerClientId;
    public int SeatIndex;
    public int BetAmount;
    public int TotalBetAmount;
    public bool HasChecked, HasFolded, IsAllIn;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref Chips);
        serializer.SerializeValue(ref OwnerClientId);
        serializer.SerializeValue(ref SeatIndex);
        serializer.SerializeValue(ref HandName);
        serializer.SerializeValue(ref HasChecked);
        serializer.SerializeValue(ref HasFolded);
        serializer.SerializeValue(ref IsAllIn);
        serializer.SerializeValue(ref BetAmount);
        serializer.SerializeValue(ref TotalBetAmount);
    }
}
