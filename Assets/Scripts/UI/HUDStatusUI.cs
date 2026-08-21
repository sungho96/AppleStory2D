using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDStatusUI : MonoBehaviour
{
    [Header("HUD Owner")]
    [SerializeField] private PlayerCharacterType hudCharacterType = PlayerCharacterType.None;

    [Header("Target")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Smooth")]
    [SerializeField] private float fillSmoothSpeed = 8f; // ���� Ŭ���� ���� ����

    [Header("HP")]
    [SerializeField] private Image hpFill;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("MP")]
    [SerializeField] private Image mpFill;
    [SerializeField] private TextMeshProUGUI mpText;

    [Header("EXP")]
    [SerializeField] private Image expFill;
    [SerializeField] private TextMeshProUGUI expText;

    [Header("Level")]
    [SerializeField] private TextMeshProUGUI levelText;

    // ===== ��ǥ��(target) =====
    private float hpTarget;
    private float mpTarget;
    private float expTarget;

    public PlayerCharacterType HudCharacterType => hudCharacterType;

    public void Bind(PlayerStats targetStats)
    {
        // [Codex Local HP HUD] NetworkPlayerOwner가 IsOwner인 로컬 캐릭터의 스탯만 HUD에 연결합니다.
        if (playerStats != null)
            playerStats.OnStatChanged -= RefreshTargets;

        playerStats = targetStats;

        if (isActiveAndEnabled && playerStats != null)
            playerStats.OnStatChanged += RefreshTargets;

        RefreshTargets();
        ForceApply();
    }

    private void OnEnable()
    {
        if (playerStats != null)
            playerStats.OnStatChanged += RefreshTargets;

        RefreshTargets();  // ��ǥ�� ��� + �ؽ�Ʈ ����
        ForceApply();      // ������ �� ��� ���߱�(�������� �ִϸ��̼� ����)
    }

    private void OnDisable()
    {
        if (playerStats != null)
            playerStats.OnStatChanged -= RefreshTargets;
    }
    private void Update()
    {
        // fillAmount�� ��ǥ������ �ε巴�� �̵�
        float dt = Time.unscaledDeltaTime; // UI�� ���� unscaled ��õ(���ο�/�Ͻ��������� �ڿ�������)

        if (hpFill != null)
            hpFill.fillAmount = Mathf.Lerp(hpFill.fillAmount, hpTarget, 1f - Mathf.Exp(-fillSmoothSpeed * dt));

        if (mpFill != null)
            mpFill.fillAmount = Mathf.Lerp(mpFill.fillAmount, mpTarget, 1f - Mathf.Exp(-fillSmoothSpeed * dt));

        if (expFill != null)
            expFill.fillAmount = Mathf.Lerp(expFill.fillAmount, expTarget, 1f - Mathf.Exp(-fillSmoothSpeed * dt));
    }

    /// <summary>
    /// ������ �ٲ� �� ȣ��: "��ǥ��"�� �����մϴ�.
    /// </summary>
    private void RefreshTargets()
    {
        if (playerStats == null) return;

        hpTarget = SafeRatio(playerStats.HP, playerStats.MaxHP);
        mpTarget = SafeRatio(playerStats.MP, playerStats.MaxMP);
        expTarget = SafeRatio(playerStats.EXP, playerStats.NeedEXP);

        // �ؽ�Ʈ�� ���� ��� ����(�����õ� ���ڴ� ��� �ٲ�� ��)
        if (hpText != null) hpText.text = $"{playerStats.HP}/{playerStats.MaxHP}";
        if (mpText != null) mpText.text = $"{playerStats.MP}/{playerStats.MaxMP}";
        if (expText != null) expText.text = $"{playerStats.EXP}/{playerStats.NeedEXP}";
        if (levelText != null) levelText.text = $"LV. {playerStats.Level}";
    }

    /// <summary>
    /// ���� �ε�� fill�� ��� ��ǥ������ ����ϴ�(������ �� �������� �ִϸ��̼� �� �ϰ�).
    /// </summary>
    private void ForceApply()
    {
        if (hpFill != null) hpFill.fillAmount = hpTarget;
        if (mpFill != null) mpFill.fillAmount = mpTarget;
        if (expFill != null) expFill.fillAmount = expTarget;
    }

    private float SafeRatio(int current, int max)
    {
        if (max <= 0) return 0f;
        return Mathf.Clamp01(current / (float)max);
    }
}