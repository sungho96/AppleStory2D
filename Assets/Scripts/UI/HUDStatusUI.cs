using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDStatusUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Smooth")]
    [SerializeField] private float fillSmoothSpeed = 8f; // 값이 클수록 빨리 따라감

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

    // ===== 목표값(target) =====
    private float hpTarget;
    private float mpTarget;
    private float expTarget;

    private void OnEnable()
    {
        if (playerStats != null)
            playerStats.OnStatChanged += RefreshTargets;

        RefreshTargets();  // 목표값 계산 + 텍스트 갱신
        ForceApply();      // 시작할 땐 즉시 맞추기(쓸데없는 애니메이션 방지)
    }

    private void OnDisable()
    {
        if (playerStats != null)
            playerStats.OnStatChanged -= RefreshTargets;
    }
    private void Update()
    {
        // fillAmount를 목표값으로 부드럽게 이동
        float dt = Time.unscaledDeltaTime; // UI는 보통 unscaled 추천(슬로우/일시정지에도 자연스러움)

        if (hpFill != null)
            hpFill.fillAmount = Mathf.Lerp(hpFill.fillAmount, hpTarget, 1f - Mathf.Exp(-fillSmoothSpeed * dt));

        if (mpFill != null)
            mpFill.fillAmount = Mathf.Lerp(mpFill.fillAmount, mpTarget, 1f - Mathf.Exp(-fillSmoothSpeed * dt));

        if (expFill != null)
            expFill.fillAmount = Mathf.Lerp(expFill.fillAmount, expTarget, 1f - Mathf.Exp(-fillSmoothSpeed * dt));
    }

    /// <summary>
    /// 스탯이 바뀔 때 호출: "목표값"만 갱신합니다.
    /// </summary>
    private void RefreshTargets()
    {
        if (playerStats == null) return;

        hpTarget = SafeRatio(playerStats.HP, playerStats.MaxHP);
        mpTarget = SafeRatio(playerStats.MP, playerStats.MaxMP);
        expTarget = SafeRatio(playerStats.EXP, playerStats.NeedEXP);

        // 텍스트는 보통 즉시 갱신(메이플도 숫자는 즉시 바뀌는 편)
        if (hpText != null) hpText.text = $"{playerStats.HP}/{playerStats.MaxHP}";
        if (mpText != null) mpText.text = $"{playerStats.MP}/{playerStats.MaxMP}";
        if (expText != null) expText.text = $"{playerStats.EXP}/{playerStats.NeedEXP}";
        if (levelText != null) levelText.text = $"LV. {playerStats.Level}";
    }

    /// <summary>
    /// 최초 로드시 fill을 즉시 목표값으로 맞춥니다(시작할 때 쓸데없이 애니메이션 안 하게).
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