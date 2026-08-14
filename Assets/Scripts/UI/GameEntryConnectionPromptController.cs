using TMPro;
using UnityEngine;

public class GameEntryConnectionPromptController : MonoBehaviour
{
    [Header("Prompt")]
    [SerializeField] private TextMeshProUGUI pressAnyKeyText;
    [SerializeField] private float blinkSpeed = 2.4f;
    [SerializeField] private float minAlpha = 0.35f;
    [SerializeField] private float maxAlpha = 1f;

    [Header("Connection Panel")]
    [SerializeField] private GameObject connectionPanel;

    private bool panelShown;
    private Color promptBaseColor;

    private void Awake()
    {
        if (pressAnyKeyText != null)
            promptBaseColor = pressAnyKeyText.color;

        SetPromptVisible(true);
        SetConnectionPanelVisible(false);
    }

    private void Update()
    {
        if (!panelShown)
        {
            UpdatePromptBlink();

            // [Codex GameEntry] ProjectSettings uses the old Input Manager, so this avoids adding Input System dependency.
            if (Input.anyKeyDown)
                ShowConnectionPanel();
        }
    }

    private void UpdatePromptBlink()
    {
        if (pressAnyKeyText == null)
            return;

        float wave = (Mathf.Sin(Time.unscaledTime * blinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        Color color = promptBaseColor;
        color.a = Mathf.Lerp(minAlpha, maxAlpha, wave);
        pressAnyKeyText.color = color;
    }

    private void ShowConnectionPanel()
    {
        panelShown = true;
        SetPromptVisible(false);
        SetConnectionPanelVisible(true);
    }

    private void SetPromptVisible(bool visible)
    {
        if (pressAnyKeyText != null)
            pressAnyKeyText.gameObject.SetActive(visible);
    }

    private void SetConnectionPanelVisible(bool visible)
    {
        if (connectionPanel != null)
            connectionPanel.SetActive(visible);
    }
}
