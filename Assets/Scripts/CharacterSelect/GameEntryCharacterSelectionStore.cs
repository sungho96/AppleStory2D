using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public static class GameEntryCharacterSelectionStore
{
    private static readonly Dictionary<ulong, PlayerCharacterType> confirmedSelections = new();

    public static PlayerCharacterType LocalSelectedCharacter { get; private set; } = PlayerCharacterType.None;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlaySession()
    {
        ResetSessionState();
    }

    public static void ResetSessionState()
    {
        // [Codex GameEntry Fresh Start] Restart로 GameEntry에 돌아올 때도 새 Play 시작처럼 캐릭터 선택 기록을 비웁니다.
        confirmedSelections.Clear();
        LocalSelectedCharacter = PlayerCharacterType.None;
    }

    public static void SetLocalSelectedCharacter(PlayerCharacterType characterType)
    {
        LocalSelectedCharacter = characterType;
        KeyBindingManager.SetBindingProfileForCharacter(characterType);
    }

    public static bool TryConfirmSelection(ulong clientId, PlayerCharacterType characterType)
    {
        if (characterType == PlayerCharacterType.None)
            return false;

        foreach (KeyValuePair<ulong, PlayerCharacterType> pair in confirmedSelections)
        {
            if (pair.Key != clientId && pair.Value == characterType)
                return false;
        }

        confirmedSelections[clientId] = characterType;
        return true;
    }

    public static bool TryGetConfirmedSelection(ulong clientId, out PlayerCharacterType characterType)
    {
        return confirmedSelections.TryGetValue(clientId, out characterType);
    }

    public static void ApplySnapshot(ulong[] clientIds, PlayerCharacterType[] characterTypes)
    {
        confirmedSelections.Clear();

        int count = Mathf.Min(clientIds.Length, characterTypes.Length);
        for (int i = 0; i < count; i++)
            confirmedSelections[clientIds[i]] = characterTypes[i];
    }

    public static bool IsCharacterConfirmedByOtherClient(PlayerCharacterType characterType, ulong localClientId)
    {
        foreach (KeyValuePair<ulong, PlayerCharacterType> pair in confirmedSelections)
        {
            if (pair.Key != localClientId && pair.Value == characterType)
                return true;
        }

        return false;
    }

    public static PlayerCharacterType GetFallbackCharacterForClient(ulong clientId)
    {
        return clientId == NetworkManager.ServerClientId
            ? PlayerCharacterType.Archer
            : PlayerCharacterType.Warrior;
    }
}
