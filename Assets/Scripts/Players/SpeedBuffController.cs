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
/// - 숫자 1: 이동속도 버프
/// - 숫자 2: 공격속도 버프
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
    }

    private void Update()
    {
        // 임시 테스트 키 숫자 1 : 이동속도
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("[SpeedBuff] 이동속도 버프 입력");
            UseSpeedBuff();
        }

        // 숫자 2: 공격속도 버프
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("[Buff] 공격속도 버프 입력");
            UseAttackSpeedBuff();
        }
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

        playerMovement.SetMoveSpeedMultiplier(attackSpeedMultiplier);

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
                attackDurationSlider.value = Mathf.Clamp01(remainingTime / duration);
            }

            yield return null;
        }
        playerAttack.ResetAttackSpeedMultiplier();

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

        if (playerAttack != null)
        {
            playerAttack.ResetAttackSpeedMultiplier();
        }
    }
}
