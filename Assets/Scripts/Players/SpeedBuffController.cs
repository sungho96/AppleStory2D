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
    [SerializeField] private WarriorAttack2D warriorAttack;

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
        // [Codex 캐릭터 선택 대응] 선택 후 스폰된 플레이어 프리팹 안에서 필요한 참조를 자동으로 보정합니다.
        ResolveMissingPlayerRefs();

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

        // [공격속도 버프 연출 추가] 공속 아이콘과 플레이어 스프라이트를 연출 대상에 연결합니다.
        if (attackSpeedVisualFeedback == null)
            attackSpeedVisualFeedback = GetComponent<AttackSpeedBuffVisualFeedback>();
        if (attackSpeedVisualFeedback == null)
            attackSpeedVisualFeedback = gameObject.AddComponent<AttackSpeedBuffVisualFeedback>();

        InitializeVisualFeedback();
    }

    public void BindPlayerTargets(
        PlayerMovement2D movement,
        PlayerAttack2D attack,
        WarriorAttack2D warriorAttackTarget)
    {
        // [Codex 캐릭터 선택 대응] 씬의 HUD 버프 컨트롤러가 선택 후 스폰된 로컬 플레이어를 대상으로 쓰게 합니다.
        if (movement != null)
            playerMovement = movement;

        if (attack != null)
            playerAttack = attack;

        if (warriorAttackTarget != null)
            warriorAttack = warriorAttackTarget;

        if (animationManager == null)
        {
            Transform target =
                playerMovement != null ? playerMovement.transform :
                playerAttack != null ? playerAttack.transform :
                warriorAttack != null ? warriorAttack.transform :
                null;

            if (target != null)
                animationManager = target.GetComponentInChildren<AnimationManager>(true);
        }

        ResolveMissingPlayerRefs();
        InitializeVisualFeedback();
    }

    private void ResolveMissingPlayerRefs()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement2D>();

        if (playerAttack == null)
            playerAttack = GetComponent<PlayerAttack2D>();

        if (warriorAttack == null)
            warriorAttack = GetComponent<WarriorAttack2D>();

        if (animationManager == null)
            animationManager = GetComponentInChildren<AnimationManager>(true);
    }

    private void InitializeVisualFeedback()
    {
        if (moveSpeedVisualFeedback != null)
        {
            moveSpeedVisualFeedback.Initialize(
                playerMovement != null ? playerMovement.transform : null,
                speedBuffIcon);
        }

        if (attackSpeedVisualFeedback != null)
        {
            attackSpeedVisualFeedback.Initialize(
                playerAttack != null ? playerAttack.transform :
                warriorAttack != null ? warriorAttack.transform :
                null,
                attackSpeedBuffIcon);
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
        if(playerAttack == null && warriorAttack == null)
        {
            Debug.LogWarning("공격속도 버프 대상 공격 스크립트가 연결되지 않았습니다.");
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

        // [공격속도 버프 캐릭터 선택 대응] 아처/워리어 중 현재 로컬 캐릭터의 공격 스크립트에만 적용합니다.
        if (playerAttack != null)
            playerAttack.SetAttackSpeedMultiplier(attackSpeedMultiplier);

        if (warriorAttack != null)
            warriorAttack.SetAttackSpeedMultiplier(attackSpeedMultiplier);

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
        if (playerAttack != null)
            playerAttack.ResetAttackSpeedMultiplier();

        if (warriorAttack != null)
            warriorAttack.ResetAttackSpeedMultiplier();

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

        if (warriorAttack != null)
        {
            warriorAttack.ResetAttackSpeedMultiplier();
        }
    }
}
