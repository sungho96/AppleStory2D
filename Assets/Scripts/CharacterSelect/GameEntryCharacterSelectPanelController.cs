using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameEntryCharacterSelectPanelController : MonoBehaviour
{
    private const string SelectionMessageName = "GameEntryCharacterSelection";
    private const byte ClientConfirmRequest = 0;
    private const byte ServerConfirmResult = 1;
    private const byte ServerSelectionSnapshot = 2;

    [SerializeField] private Button archerButton;
    [SerializeField] private Image archerCardImage;
    [SerializeField] private Button warriorButton;
    [SerializeField] private Image warriorCardImage;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Image confirmImage;
    [SerializeField] private TextMeshProUGUI statusText;

    public event Action Confirmed;

    private NetworkManager networkManager;
    private PlayerCharacterType selectedCharacter = PlayerCharacterType.None;
    private bool messageRegistered;
    private bool waitingForServer;
    private bool confirmed;
    private bool confirmAnimationPlaying;
    private Coroutine visualRoutine;
    private Coroutine confirmPulseRoutine;
    private Coroutine rejectShakeRoutine;

    private readonly Color normalColor = Color.white;
    private readonly Color dimmedColor = new(0.55f, 0.55f, 0.55f, 1f);
    private readonly Color blockedColor = new(0.32f, 0.32f, 0.32f, 0.82f);
    private readonly Color disabledConfirmColor = new(0.48f, 0.48f, 0.48f, 0.68f);
    private readonly Color enabledConfirmColor = Color.white;
    private readonly Vector3 normalScale = Vector3.one;
    private readonly Vector3 selectedScale = new(1.05f, 1.05f, 1f);
    private readonly Vector3 popScale = new(1.12f, 1.12f, 1f);
    private readonly Vector3 confirmReadyScale = new(1.04f, 1.04f, 1f);

    private Vector2 archerBasePosition;
    private Vector2 warriorBasePosition;

    private void Awake()
    {
        ResolveReferences();
        SetStatus("캐릭터를 선택하세요.");
        RefreshVisualState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RegisterNetworkMessage();

        if (archerButton != null)
            archerButton.onClick.AddListener(SelectArcher);

        if (warriorButton != null)
            warriorButton.onClick.AddListener(SelectWarrior);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmSelection);

        waitingForServer = false;
        confirmed = false;
        confirmAnimationPlaying = false;
        CaptureBasePositions();
        RefreshVisualState();
    }

    private void OnDisable()
    {
        if (archerButton != null)
            archerButton.onClick.RemoveListener(SelectArcher);

        if (warriorButton != null)
            warriorButton.onClick.RemoveListener(SelectWarrior);

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmSelection);
    }

    private void OnDestroy()
    {
        UnregisterNetworkMessage();
    }

    public void Initialize(Button archer, Image archerImage, Button warrior, Image warriorImage, Button confirm, TextMeshProUGUI status)
    {
        archerButton = archer;
        archerCardImage = archerImage;
        warriorButton = warrior;
        warriorCardImage = warriorImage;
        confirmButton = confirm;
        confirmImage = confirm != null ? confirm.transform.Find("ConfirmImage")?.GetComponent<Image>() : null;
        statusText = status;
        CaptureBasePositions();
        RefreshVisualState();
    }

    private void SelectArcher()
    {
        SelectCharacter(PlayerCharacterType.Archer);
    }

    private void SelectWarrior()
    {
        SelectCharacter(PlayerCharacterType.Warrior);
    }

    private void SelectCharacter(PlayerCharacterType characterType)
    {
        if (IsBlockedByOtherClient(characterType))
        {
            SetStatus("상대가 이미 확정한 캐릭터입니다.");
            PlayRejectShake(characterType);
            return;
        }

        selectedCharacter = characterType;
        GameEntryCharacterSelectionStore.SetLocalSelectedCharacter(characterType);
        SetStatus(characterType == PlayerCharacterType.Archer ? "Archer 선택됨" : "Warrior 선택됨");
        RefreshVisualState(true);
    }

    private void ConfirmSelection()
    {
        if (selectedCharacter == PlayerCharacterType.None || waitingForServer || confirmed)
            return;

        RegisterNetworkMessage();

        if (networkManager == null || !networkManager.IsListening)
        {
            AcceptLocalConfirmation();
            return;
        }

        waitingForServer = true;
        RefreshVisualState();

        if (networkManager.IsServer)
        {
            bool accepted = GameEntryCharacterSelectionStore.TryConfirmSelection(
                networkManager.LocalClientId,
                selectedCharacter);

            waitingForServer = false;
            if (accepted)
                AcceptLocalConfirmation();
            else
            {
                RejectCurrentSelection();
            }

            BroadcastSnapshot();
            return;
        }

        using FastBufferWriter writer = new FastBufferWriter(sizeof(byte) + sizeof(int), Unity.Collections.Allocator.Temp);
        writer.WriteValueSafe(ClientConfirmRequest);
        writer.WriteValueSafe((int)selectedCharacter);
        networkManager.CustomMessagingManager.SendNamedMessage(
            SelectionMessageName,
            NetworkManager.ServerClientId,
            writer);
    }

    private void RegisterNetworkMessage()
    {
        if (messageRegistered)
            return;

        networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.CustomMessagingManager == null)
            return;

        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
            SelectionMessageName,
            OnSelectionMessageReceived);
        messageRegistered = true;
    }

    private void UnregisterNetworkMessage()
    {
        if (!messageRegistered || networkManager == null || networkManager.CustomMessagingManager == null)
            return;

        networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(SelectionMessageName);
        messageRegistered = false;
    }

    private void OnSelectionMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte messageType);

        if (messageType == ClientConfirmRequest)
        {
            if (networkManager == null || !networkManager.IsServer)
                return;

            reader.ReadValueSafe(out int requestedCharacter);
            PlayerCharacterType characterType = (PlayerCharacterType)requestedCharacter;
            bool accepted = GameEntryCharacterSelectionStore.TryConfirmSelection(senderClientId, characterType);
            SendConfirmResult(senderClientId, characterType, accepted);
            BroadcastSnapshot();
            return;
        }

        if (messageType == ServerConfirmResult)
        {
            reader.ReadValueSafe(out int resultCharacter);
            reader.ReadValueSafe(out bool accepted);
            waitingForServer = false;

            if (accepted && (PlayerCharacterType)resultCharacter == selectedCharacter)
            {
                AcceptLocalConfirmation();
                return;
            }

            RejectCurrentSelection();
            return;
        }

        if (messageType == ServerSelectionSnapshot)
            ReadSnapshot(reader);
    }

    private void SendConfirmResult(ulong targetClientId, PlayerCharacterType characterType, bool accepted)
    {
        using FastBufferWriter writer = new FastBufferWriter(sizeof(byte) + sizeof(int) + sizeof(bool), Unity.Collections.Allocator.Temp);
        writer.WriteValueSafe(ServerConfirmResult);
        writer.WriteValueSafe((int)characterType);
        writer.WriteValueSafe(accepted);
        networkManager.CustomMessagingManager.SendNamedMessage(SelectionMessageName, targetClientId, writer);
    }

    private void BroadcastSnapshot()
    {
        if (networkManager == null || !networkManager.IsServer)
            return;

        List<ulong> clientIds = new();
        List<PlayerCharacterType> characterTypes = new();
        foreach (ulong clientId in networkManager.ConnectedClientsIds)
        {
            if (GameEntryCharacterSelectionStore.TryGetConfirmedSelection(clientId, out PlayerCharacterType characterType))
            {
                clientIds.Add(clientId);
                characterTypes.Add(characterType);
            }
        }

        using FastBufferWriter writer = new FastBufferWriter(256, Unity.Collections.Allocator.Temp);
        writer.WriteValueSafe(ServerSelectionSnapshot);
        writer.WriteValueSafe(clientIds.Count);
        for (int i = 0; i < clientIds.Count; i++)
        {
            writer.WriteValueSafe(clientIds[i]);
            writer.WriteValueSafe((int)characterTypes[i]);
        }

        networkManager.CustomMessagingManager.SendNamedMessageToAll(SelectionMessageName, writer);
    }

    private void ReadSnapshot(FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        ulong[] clientIds = new ulong[count];
        PlayerCharacterType[] characterTypes = new PlayerCharacterType[count];

        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out clientIds[i]);
            reader.ReadValueSafe(out int characterType);
            characterTypes[i] = (PlayerCharacterType)characterType;
        }

        GameEntryCharacterSelectionStore.ApplySnapshot(clientIds, characterTypes);
        RefreshVisualState();
    }

    private void AcceptLocalConfirmation()
    {
        confirmed = true;
        waitingForServer = false;
        GameEntryCharacterSelectionStore.SetLocalSelectedCharacter(selectedCharacter);
        SetStatus("선택 확정");

        if (confirmAnimationPlaying)
            return;

        StartCoroutine(PlayConfirmTransition());
    }

    private void RejectCurrentSelection()
    {
        PlayerCharacterType rejectedCharacter = selectedCharacter;
        SetStatus("이미 선택된 캐릭터입니다. 다른 캐릭터를 선택하세요.");
        selectedCharacter = PlayerCharacterType.None;
        GameEntryCharacterSelectionStore.SetLocalSelectedCharacter(PlayerCharacterType.None);
        RefreshVisualState(true);
        PlayRejectShake(rejectedCharacter);
    }

    private bool IsBlockedByOtherClient(PlayerCharacterType characterType)
    {
        ulong localClientId = networkManager != null ? networkManager.LocalClientId : 0;
        return GameEntryCharacterSelectionStore.IsCharacterConfirmedByOtherClient(characterType, localClientId);
    }

    private void RefreshVisualState(bool animated = false)
    {
        bool archerBlocked = IsBlockedByOtherClient(PlayerCharacterType.Archer);
        bool warriorBlocked = IsBlockedByOtherClient(PlayerCharacterType.Warrior);

        if (animated && isActiveAndEnabled)
            PlayVisualTransition(archerBlocked, warriorBlocked);
        else
        {
            ApplyCardStateImmediate(archerCardImage, selectedCharacter == PlayerCharacterType.Archer, archerBlocked);
            ApplyCardStateImmediate(warriorCardImage, selectedCharacter == PlayerCharacterType.Warrior, warriorBlocked);
            ApplyConfirmStateImmediate();
        }

        if (archerButton != null)
            archerButton.interactable = !confirmed && !waitingForServer && !confirmAnimationPlaying && !archerBlocked;

        if (warriorButton != null)
            warriorButton.interactable = !confirmed && !waitingForServer && !confirmAnimationPlaying && !warriorBlocked;

        if (confirmButton != null)
            confirmButton.interactable = !confirmed && !waitingForServer && !confirmAnimationPlaying && selectedCharacter != PlayerCharacterType.None;
    }

    private void ApplyCardStateImmediate(Image cardImage, bool selected, bool blocked)
    {
        if (cardImage == null)
            return;

        cardImage.transform.localScale = selected ? selectedScale : normalScale;
        cardImage.color = blocked ? blockedColor : selected ? normalColor : dimmedColor;
    }

    private void ApplyConfirmStateImmediate()
    {
        if (confirmImage == null && confirmButton != null)
            confirmImage = confirmButton.transform.Find("ConfirmImage")?.GetComponent<Image>();

        if (confirmImage == null)
            return;

        bool active = selectedCharacter != PlayerCharacterType.None && !waitingForServer && !confirmed;
        confirmImage.color = active ? enabledConfirmColor : disabledConfirmColor;
        confirmImage.transform.localScale = active ? confirmReadyScale : normalScale;
        UpdateConfirmPulse(active);
    }

    private void PlayVisualTransition(bool archerBlocked, bool warriorBlocked)
    {
        if (visualRoutine != null)
            StopCoroutine(visualRoutine);

        visualRoutine = StartCoroutine(AnimateVisualTransition(archerBlocked, warriorBlocked));
    }

    private IEnumerator AnimateVisualTransition(bool archerBlocked, bool warriorBlocked)
    {
        Image selectedImage = selectedCharacter == PlayerCharacterType.Archer ? archerCardImage : warriorCardImage;

        Vector3 archerStartScale = archerCardImage != null ? archerCardImage.transform.localScale : normalScale;
        Vector3 warriorStartScale = warriorCardImage != null ? warriorCardImage.transform.localScale : normalScale;
        Color archerStartColor = archerCardImage != null ? archerCardImage.color : normalColor;
        Color warriorStartColor = warriorCardImage != null ? warriorCardImage.color : normalColor;
        Vector3 confirmStartScale = confirmImage != null ? confirmImage.transform.localScale : normalScale;
        Color confirmStartColor = confirmImage != null ? confirmImage.color : disabledConfirmColor;

        float elapsed = 0f;
        const float duration = 0.18f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / duration));

            AnimateCard(archerCardImage, archerStartScale, archerStartColor, PlayerCharacterType.Archer, archerBlocked, selectedImage, t);
            AnimateCard(warriorCardImage, warriorStartScale, warriorStartColor, PlayerCharacterType.Warrior, warriorBlocked, selectedImage, t);
            AnimateConfirm(confirmStartScale, confirmStartColor, t);
            yield return null;
        }

        ApplyCardStateImmediate(archerCardImage, selectedCharacter == PlayerCharacterType.Archer, archerBlocked);
        ApplyCardStateImmediate(warriorCardImage, selectedCharacter == PlayerCharacterType.Warrior, warriorBlocked);
        ApplyConfirmStateImmediate();
    }

    private void AnimateCard(Image cardImage, Vector3 startScale, Color startColor, PlayerCharacterType characterType, bool blocked, Image selectedImage, float t)
    {
        if (cardImage == null)
            return;

        bool selected = selectedCharacter == characterType;
        Vector3 targetScale = selected ? selectedScale : normalScale;
        if (cardImage == selectedImage && selected)
            targetScale = Vector3.Lerp(popScale, selectedScale, t);

        Color targetColor = blocked ? blockedColor : selected ? normalColor : dimmedColor;
        cardImage.transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
        cardImage.color = Color.Lerp(startColor, targetColor, Mathf.Clamp01(t));
    }

    private void AnimateConfirm(Vector3 startScale, Color startColor, float t)
    {
        if (confirmImage == null)
            return;

        bool active = selectedCharacter != PlayerCharacterType.None && !waitingForServer && !confirmed;
        Vector3 targetScale = active ? confirmReadyScale : normalScale;
        Color targetColor = active ? enabledConfirmColor : disabledConfirmColor;
        confirmImage.transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
        confirmImage.color = Color.Lerp(startColor, targetColor, Mathf.Clamp01(t));
    }

    private IEnumerator PlayConfirmTransition()
    {
        confirmAnimationPlaying = true;
        RefreshVisualState();

        Image selectedImage = selectedCharacter == PlayerCharacterType.Archer ? archerCardImage : warriorCardImage;
        RectTransform selectedRoot = GetSelectedCardRoot();
        RectTransform panelRect = transform as RectTransform;

        Vector2 startPosition = selectedRoot != null ? selectedRoot.anchoredPosition : Vector2.zero;
        Vector3 startScale = selectedRoot != null ? selectedRoot.localScale : selectedScale;
        Color startColor = selectedImage != null ? selectedImage.color : normalColor;

        float elapsed = 0f;
        const float duration = 0.46f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float t = SmoothStep(normalized);

            if (selectedRoot != null)
            {
                selectedRoot.anchoredPosition = Vector2.Lerp(startPosition, Vector2.zero, t);
                selectedRoot.localScale = Vector3.LerpUnclamped(startScale, new Vector3(1.2f, 1.2f, 1f), EaseOutBack(t));
            }

            if (selectedImage != null)
                selectedImage.color = Color.Lerp(startColor, Color.white, t);

            if (panelRect != null)
                panelRect.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.985f, 0.985f, 1f), t);

            if (confirmImage != null)
                confirmImage.color = Color.Lerp(enabledConfirmColor, new Color(1f, 1f, 1f, 0.2f), t);

            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.08f);

        if (panelRect != null)
            panelRect.localScale = Vector3.one;

        Confirmed?.Invoke();

        if (selectedRoot != null)
        {
            selectedRoot.anchoredPosition = startPosition;
            selectedRoot.localScale = Vector3.one;
        }

        confirmAnimationPlaying = false;
    }

    private RectTransform GetSelectedCardRoot()
    {
        if (selectedCharacter == PlayerCharacterType.Archer && archerButton != null)
            return archerButton.GetComponent<RectTransform>();

        if (selectedCharacter == PlayerCharacterType.Warrior && warriorButton != null)
            return warriorButton.GetComponent<RectTransform>();

        return null;
    }

    private void PlayRejectShake(PlayerCharacterType characterType)
    {
        RectTransform target = characterType == PlayerCharacterType.Archer
            ? archerCardImage != null ? archerCardImage.rectTransform : null
            : warriorCardImage != null ? warriorCardImage.rectTransform : null;

        if (target == null || !isActiveAndEnabled)
            return;

        if (rejectShakeRoutine != null)
            StopCoroutine(rejectShakeRoutine);

        Vector2 basePosition = characterType == PlayerCharacterType.Archer ? archerBasePosition : warriorBasePosition;
        rejectShakeRoutine = StartCoroutine(ShakeRect(target, basePosition));
    }

    private IEnumerator ShakeRect(RectTransform target, Vector2 basePosition)
    {
        const float duration = 0.22f;
        const float strength = 18f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float offset = Mathf.Sin(normalized * Mathf.PI * 6f) * strength * (1f - normalized);
            target.anchoredPosition = basePosition + new Vector2(offset, 0f);
            yield return null;
        }

        target.anchoredPosition = basePosition;
    }

    private void UpdateConfirmPulse(bool active)
    {
        if (active)
        {
            if (confirmPulseRoutine == null && isActiveAndEnabled)
                confirmPulseRoutine = StartCoroutine(PulseConfirmButton());

            return;
        }

        if (confirmPulseRoutine != null)
        {
            StopCoroutine(confirmPulseRoutine);
            confirmPulseRoutine = null;
        }
    }

    private IEnumerator PulseConfirmButton()
    {
        while (true)
        {
            if (confirmImage != null && selectedCharacter != PlayerCharacterType.None && !waitingForServer && !confirmed)
            {
                float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.5f;
                confirmImage.color = Color.Lerp(new Color(0.88f, 0.88f, 0.88f, 1f), Color.white, pulse);
            }

            yield return null;
        }
    }

    private void CaptureBasePositions()
    {
        if (archerCardImage != null)
            archerBasePosition = archerCardImage.rectTransform.anchoredPosition;

        if (warriorCardImage != null)
            warriorBasePosition = warriorCardImage.rectTransform.anchoredPosition;
    }

    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private void ResolveReferences()
    {
        archerButton ??= transform.Find("ArcherButton")?.GetComponent<Button>();
        archerCardImage ??= transform.Find("ArcherButton/ArcherCardImage")?.GetComponent<Image>();
        warriorButton ??= transform.Find("WarriorButton")?.GetComponent<Button>();
        warriorCardImage ??= transform.Find("WarriorButton/WarriorCardImage")?.GetComponent<Image>();
        confirmButton ??= transform.Find("ConfirmButton")?.GetComponent<Button>();
        confirmImage ??= transform.Find("ConfirmButton/ConfirmImage")?.GetComponent<Image>();
        statusText ??= transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }
}
