using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;

/// <summary>
/// 이동속 버프 관리.
/// 이동속도 증가
/// 버프 아이콘 표시
/// 지속시간 slider 감소
/// 종료후 속도 복구 및 아이콘 숨김
/// </summary>
public class SpeedBuffController : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerMovement2D playerMovement;
    [SerializeField] private PlayerAttack2D playerAttack;

    [Header("Move Speed Buff UI")]
    [SerializeField] private GameObject speedBuffIcon;
    [SerializeField] private Slider durationSlider;

    [Header("Attack Speed Buff UI")]
    [SerializeField] private GameObject attackSpeedBuffIcon;
    [SerializeField] private Slider attackDurationSlider;

    [Header("Move Speed Buff Settings")]
    [SerializeField] private float moveSpeedMultiplier = 1.5f;
    [SerializeField] private float duration = 10f;

    [Header("Attack Speed Buff Settings")]
    [SerializeField] private float attackSpeedMultiplier = 1.5f;
    [SerializeField] private float attackSpeedDuration = 10f;

    [Header("Animation")]
    [SerializeField] private AnimationManager animationManager;

    [Header("Visual Feedback")]
    [Tooltip("비워두면 같은 오브젝트에 자동으로 생성됩니다.")]
    // [이동속도 버프 연출 추가] 씬 수정을 피하기 위해 미연결 상태에서는 Awake에서 자동 생성합니다.
    [SerializeField] private SpeedBuffVisualFeedback moveSpeedVisualFeedback;
    // [공격속도 버프 연출 추가] 씬 수정을 피하기 위해 미연결 상태에서는 Awake에서 자동 생성합니다.
    [SerializeField] private AttackSpeedBuffVisualFeedback attackSpeedVisualFeedback;

    private Coroutine moveSpeedBuffCoroutine;
    private Coroutine attackSpeedBuffCoroutine;

    private void Awake()
    {
        if (speedBuffIcon !=null)
        {
            speedBuffIcon.SetActive(false);
        }

        if (attackSpeedBuffIcon != null)
        {
            attackSpeedBuffIcon.SetActive(false);
        }

        if (moveSpeedVisualFeedback == null)
            moveSpeedVisualFeedback = GetComponent<SpeedBuffVisualFeedback>();
        if (moveSpeedVisualFeedback == null)
            moveSpeedVisualFeedback = gameObject.AddComponent<SpeedBuffVisualFeedback>();

        moveSpeedVisualFeedback.Initialize(
            playerMovement != null ? playerMovement.transform : null,
            speedBuffIcon);

        // [공격속도 버프 연출 추가] 공속 아이콘과 플레이어 스프라이트를 연출 대상에 연결합니다.
        if (attackSpeedVisualFeedback == null)
            attackSpeedVisualFeedback = GetComponent<AttackSpeedBuffVisualFeedback>();
        if (attackSpeedVisualFeedback == null)
            attackSpeedVisualFeedback = gameObject.AddComponent<AttackSpeedBuffVisualFeedback>();

        attackSpeedVisualFeedback.Initialize(
            playerAttack != null ? playerAttack.transform : null,
            attackSpeedBuffIcon);
    }

    /// <summary>
    /// 이동속도 버프 사용
    /// 이미 적용 중이라면 지속시간을 처음부터 다시 시작.
    /// </summary>
    public void UseSpeedBuff()
    {
        Debug.Log("[SpeedBuff] UseSpeedBuff 호출");
        if (playerMovement == null)
        {
            Debug.LogWarning("PlayerMovement2D가 연결되지 않았습니다.");
            return;
        }

        if (moveSpeedBuffCoroutine !=null)
        {
            Debug.Log("[SpeedBuff] 기존 버프 코루틴 정지");
            StopCoroutine(moveSpeedBuffCoroutine);
        }

        moveSpeedBuffCoroutine = StartCoroutine(MoveSpeedBuffRoutine());
    }

    /// <summary>
    /// 공격속도 버프를 사용합니다.
    /// 재사용하면 지속시간을 처음부터 다시 시작합니다.
    /// </summary>
    public void UseAttackSpeedBuff()
    {
        if(playerAttack == null)
        {
            Debug.LogWarning("PlayerAttack2D가 연결되지 않았습니다.");
            return;
        }

        if (attackSpeedBuffCoroutine != null)
        {
            StopCoroutine(attackSpeedBuffCoroutine);
        }

        attackSpeedBuffCoroutine = StartCoroutine(AttackSpeedBuffRoutine());
    }

    /// <summary>
    /// 이동속도 버프 지속시간 처리.
    /// </summary>
    private IEnumerator MoveSpeedBuffRoutine()
    {
        if (speedBuffIcon != null)
        {
            speedBuffIcon.SetActive(true);
        }

        animationManager?.PlayMoveSpeedBuff();

        moveSpeedVisualFeedback?.PlayStart();

        playerMovement.SetMoveSpeedMultiplier(moveSpeedMultiplier);

        float remainingTime = duration;

        if (durationSlider != null)
        {
            durationSlider.minValue = 0f;
            durationSlider.maxValue = 1f;
            durationSlider.value = 1f;
        }

        while (remainingTime > 0f)
        {
            remainingTime -= Time.deltaTime;

            if (durationSlider != null)
            {
                durationSlider.value = Mathf.Clamp01(remainingTime / duration);
            }

            yield return null;
        }
        playerMovement.ResetmoveSpeedMultiplier();

        moveSpeedVisualFeedback?.PlayEnd();
            
        if (durationSlider != null)
        {
            durationSlider.value = 0f;
        }

        if(speedBuffIcon != null)
        {
            speedBuffIcon.SetActive(false);
        }

        moveSpeedBuffCoroutine = null;
    }

    /// <summary>
    /// 공격속도 버프 지속시간 처리.
    /// </summary>
    private IEnumerator AttackSpeedBuffRoutine()
    {
        if (attackSpeedBuffIcon != null)
        {
            attackSpeedBuffIcon.SetActive(true);
        }

        animationManager?.PlayAttackSpeedBuff();

        // [공격속도 버프 오류 수정] 이동속도가 아니라 실제 공격 대기시간 배율에 적용합니다.
        playerAttack.SetAttackSpeedMultiplier(attackSpeedMultiplier);

        // [공격속도 버프 연출 추가] 발동 플래시와 상단 아이콘 모션을 시작합니다.
        attackSpeedVisualFeedback?.PlayStart();

        float remainingTime = attackSpeedDuration;

        if (attackDurationSlider != null)
        {
            attackDurationSlider.minValue = 0f;
            attackDurationSlider.maxValue = 1f;
            attackDurationSlider.value = 1f;
        }

        while (remainingTime > 0f)
        {
            remainingTime -= Time.deltaTime;

            if (attackDurationSlider != null)
            {
                // [공격속도 버프 오류 수정] 공속 버프 고유 지속시간을 기준으로 게이지를 계산합니다.
                attackDurationSlider.value = Mathf.Clamp01(remainingTime / attackSpeedDuration);
            }

            yield return null;
        }
        playerAttack.ResetAttackSpeedMultiplier();

        attackSpeedVisualFeedback?.PlayEnd();

        if (attackDurationSlider != null)
        {
            attackDurationSlider.value = 0f;
        }

        if (attackSpeedBuffIcon != null)
        {
            attackSpeedBuffIcon.SetActive(false);
        }

        attackSpeedBuffCoroutine = null;
    }
    private void OnDisable()
    {
        // 오브젝트가 비활성화될 때 버프가 남지 않게 복구
        if (playerMovement != null)
        {
            playerMovement.ResetmoveSpeedMultiplier();
        }

        moveSpeedVisualFeedback?.PlayEnd();
        attackSpeedVisualFeedback?.PlayEnd();

        if (playerAttack != null)
        {
            playerAttack.ResetAttackSpeedMultiplier();
        }
    }
}
