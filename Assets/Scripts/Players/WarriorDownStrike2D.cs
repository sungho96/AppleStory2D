using System.Collections;
using System.Collections.Generic;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WarriorDownStrike2D : MonoBehaviour
{
    private const string RightDownStrikeClipEditorPath =
        "Assets/_Project/Player/Common/Animation/Upper/downstrike.anim";
    private const string LeftDownStrikeClipEditorPath =
        "Assets/Art/Prefabs/Player/downStrike_L.anim";

    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerHealth2D playerHealth;
    [SerializeField] private PlayerLadder2D playerLadder;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private PlayerDirection2D playerDirection;
    [SerializeField] private WarriorDownStrikeVisualFeedback downStrikeVisualFeedback;

    [Header("Skill")]
    [SerializeField] private KeyCode fallbackKey = KeyCode.None;
    [SerializeField] private int damage = 35;
    [SerializeField] private float cooldown = 1.4f;
    [SerializeField] private Vector2 hitBoxSize = new Vector2(1.55f, 1f);
    [SerializeField] private Vector2 hitBoxOffset = new Vector2(0.85f, 0.05f);
    [SerializeField] private LayerMask enemyMask;

    [Header("Down Strike Motion")]
    [SerializeField] private float hopVelocity = 7.5f;
    [SerializeField] private float slamVelocity = -13f;
    [SerializeField] private float maxLandingVfxWaitDuration = 1.2f;
    [SerializeField] private float slashVfxDelay = 0.08f;

    [Header("Animator Blend")]
    [SerializeField, HideInInspector] private AnimationClip downStrikeClip;
    [SerializeField] private AnimationClip rightDownStrikeClip;
    [SerializeField] private AnimationClip leftDownStrikeClip;
    [SerializeField] private float directClipSpeed = 1f;
    [SerializeField] private string downStrikeStateName = "downstrike";
    [SerializeField] private string downStrikeTriggerName = "";
    [SerializeField] private float downStrikeFadeDuration = 0.02f;
    [SerializeField, Range(0f, 1f)] private float downStrikeStartNormalizedTime = 0f;
    [SerializeField, Range(0f, 1f)] private float hitNormalizedTime = 0.45f;
    [SerializeField] private float maxDownStrikeWaitDuration = 1.4f;

    private bool isUsingSkill;
    private float nextUseTime;
    private PlayableGraph downStrikeGraph;
    private Coroutine landingVfxRoutine;
    private Coroutine slashVfxRoutine;
    private AnimationClip activeDownStrikeClip;

    public bool IsUsingSkill => isUsingSkill;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth2D>();
        if (playerLadder == null)
            playerLadder = GetComponent<PlayerLadder2D>();
        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody2D>();
        if (playerDirection == null)
            playerDirection = GetComponent<PlayerDirection2D>();
        if (downStrikeVisualFeedback == null)
            downStrikeVisualFeedback = GetComponent<WarriorDownStrikeVisualFeedback>();
        if (downStrikeVisualFeedback == null)
            downStrikeVisualFeedback = gameObject.AddComponent<WarriorDownStrikeVisualFeedback>();
        if (downStrikeVisualFeedback != null)
            downStrikeVisualFeedback.Initialize();

#if UNITY_EDITOR
        LoadDirectionalClipsInEditor();
#endif
    }

    private void Update()
    {
        if (fallbackKey == KeyCode.None || !Input.GetKeyDown(fallbackKey))
            return;

        UseDownStrike();
    }

    public void UseDownStrike()
    {
        if (isUsingSkill || Time.time < nextUseTime)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        StartCoroutine(DownStrikeRoutine());
    }

    private IEnumerator DownStrikeRoutine()
    {
        isUsingSkill = true;

        StartDownStrikeHop();
        PlayDownStrike();

        // [Codex Warrior DownStrike 1st] 새로 만든 downstrike 상태를 처음부터 끝까지 한 번 재생하고, 중간 타이밍에만 판정을 넣습니다.
        yield return WaitForDownStrikeHitTiming();

        StartDownStrikeSlashVfxWatch();
        ForceDownStrikeFall();
        ApplyDownStrikeHit();
        StartDownStrikeLandingVfxWatch();
        yield return WaitForDownStrikeAnimationEnd();

        nextUseTime = Time.time + cooldown;
        isUsingSkill = false;
    }

    private void PlayDownStrikeSlashVfx()
    {
        // [Codex DownStrike VFX] 기존 타격 타이밍을 그대로 사용하고, 판정과 무관한 로컬 검기만 재생합니다.
        downStrikeVisualFeedback?.PlayDownStrikeSlashVfx();
    }

    private void StartDownStrikeSlashVfxWatch()
    {
        if (downStrikeVisualFeedback == null)
            return;

        if (slashVfxRoutine != null)
            StopCoroutine(slashVfxRoutine);

        slashVfxRoutine = StartCoroutine(WaitAndPlaySlashVfx());
    }

    private IEnumerator WaitAndPlaySlashVfx()
    {
        float delay = Mathf.Max(0f, slashVfxDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        PlayDownStrikeSlashVfx();
        slashVfxRoutine = null;
    }

    private void PlayDownStrikeDustVfx()
    {
        // [Codex DownStrike VFX] 착지 먼지는 네트워크 Spawn/RPC 없이 현재 클라이언트 화면에서만 재생합니다.
        downStrikeVisualFeedback?.PlayDownStrikeDustVfx();
    }

    private void StartDownStrikeLandingVfxWatch()
    {
        if (downStrikeVisualFeedback == null || playerLadder == null)
            return;

        if (landingVfxRoutine != null)
            StopCoroutine(landingVfxRoutine);

        landingVfxRoutine = StartCoroutine(WaitForLandingAndPlayDustVfx());
    }

    private IEnumerator WaitForLandingAndPlayDustVfx()
    {
        float elapsed = 0f;
        bool waitedForAirborneFrame = false;

        while (elapsed < maxLandingVfxWaitDuration)
        {
            if (playerLadder == null || playerLadder.IsClimbing)
                break;

            if (!playerLadder.IsGrounded)
            {
                waitedForAirborneFrame = true;
            }
            else if (waitedForAirborneFrame || elapsed > 0.04f)
            {
                PlayDownStrikeDustVfx();
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        landingVfxRoutine = null;
    }

    private void StartDownStrikeHop()
    {
        if (playerRigidbody == null)
            return;

        if (playerLadder != null && playerLadder.IsClimbing)
            return;

        if (playerLadder != null && !playerLadder.IsGrounded)
            return;

        // [Codex Warrior DownStrike Hop] 땅에서 바로 내려찍지 않고, 스킬 시작 순간 살짝 떠오르게 합니다.
        playerRigidbody.linearVelocity = new Vector2(playerRigidbody.linearVelocity.x, hopVelocity);
    }

    private void ForceDownStrikeFall()
    {
        if (playerRigidbody == null)
            return;

        if (playerLadder != null && playerLadder.IsClimbing)
            return;

        // [Codex Warrior DownStrike Hop] 타격 타이밍에는 아래 방향 속도를 줘서 내려찍는 느낌을 만듭니다.
        playerRigidbody.linearVelocity = new Vector2(playerRigidbody.linearVelocity.x, slamVelocity);
    }

    private void PlayDownStrike()
    {
        if (animator == null)
            return;

        activeDownStrikeClip = GetCurrentDownStrikeClip();
        if (activeDownStrikeClip != null)
        {
            PrepareDirectClipDirection(activeDownStrikeClip);
            PlayDirectClip(activeDownStrikeClip, directClipSpeed);
            return;
        }

        if (!string.IsNullOrEmpty(downStrikeTriggerName))
        {
            animator.ResetTrigger(downStrikeTriggerName);
            animator.SetTrigger(downStrikeTriggerName);
        }

        PlayAnimatorState(downStrikeStateName, downStrikeFadeDuration, downStrikeStartNormalizedTime);
    }

    private IEnumerator WaitForDownStrikeHitTiming()
    {
        if (activeDownStrikeClip != null)
        {
            yield return new WaitForSeconds(GetDirectClipDuration() * hitNormalizedTime);
            yield break;
        }

        if (animator == null || string.IsNullOrEmpty(downStrikeStateName))
        {
            yield return null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < maxDownStrikeWaitDuration)
        {
            if (IsAnimatorStateAtOrPast(downStrikeStateName, hitNormalizedTime))
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForDownStrikeAnimationEnd()
    {
        if (activeDownStrikeClip != null)
        {
            yield return new WaitForSeconds(GetDirectClipDuration() * (1f - hitNormalizedTime));
            StopDirectClip();
            yield break;
        }

        if (animator == null || string.IsNullOrEmpty(downStrikeStateName))
            yield break;

        float elapsed = 0f;
        while (elapsed < maxDownStrikeWaitDuration)
        {
            if (IsAnimatorStateAtOrPast(downStrikeStateName, 0.98f))
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private bool IsAnimatorStateAtOrPast(string stateName, float normalizedTime)
    {
        int stateHash = Animator.StringToHash(stateName);
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            if (stateInfo.shortNameHash == stateHash && stateInfo.normalizedTime >= normalizedTime)
                return true;
        }

        return false;
    }

    private void PlayAnimatorState(string stateName, float fadeDuration, float normalizedStartTime = 0f)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        int stateHash = Animator.StringToHash(stateName);
        for (int layer = animator.layerCount - 1; layer >= 0; layer--)
        {
            if (!animator.HasState(layer, stateHash))
                continue;

            // [Codex Warrior DownStrike 1st] 내려찍기 첫 구현은 상태를 중간부터 건너뛰지 않고 1회 재생합니다.
            animator.CrossFade(stateHash, fadeDuration, layer, normalizedStartTime);
            return;
        }

        for (int layer = animator.layerCount - 1; layer >= 0; layer--)
        {
            string qualifiedName = animator.GetLayerName(layer) + "." + stateName;
            int qualifiedHash = Animator.StringToHash(qualifiedName);
            if (!animator.HasState(layer, qualifiedHash))
                continue;

            animator.CrossFade(qualifiedHash, fadeDuration, layer, normalizedStartTime);
            return;
        }
    }

    private void PlayDirectClip(AnimationClip clip, float speed)
    {
        StopDirectClip();

        // [Codex Warrior Direct Clip] Inspector에 넣은 애니메이션 클립을 Animator Controller 상태 전환 없이 그대로 재생합니다.
        downStrikeGraph = PlayableGraph.Create("WarriorDownStrikeClip");
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(
            downStrikeGraph,
            "DownStrikeOutput",
            animator);
        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(downStrikeGraph, clip);
        clipPlayable.SetSpeed(Mathf.Max(0.01f, speed));
        output.SetSourcePlayable(clipPlayable);
        downStrikeGraph.Play();
    }

    private void PrepareDirectClipDirection(AnimationClip clip)
    {
        if (playerDirection == null)
            return;

        float dir = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;

        // [Codex Warrior Directional DownStrike] 내려찍기 입력 순간 바라보는 방향에 맞는 오브젝트만 켠 뒤 방향별 클립을 재생합니다.
        if (dir < 0f)
            playerDirection.SetDirectionFromNetwork(PlayerDirection2D.FacingDir.Left);
        else
            playerDirection.SetDirectionFromNetwork(PlayerDirection2D.FacingDir.Right);

        playerDirection.RefreshDirectionVisuals();
    }

    private void RestoreDirectionAfterDirectClip()
    {
        // [Codex Warrior Direct Clip 복구] 클립 종료 뒤에도 AnimationClip이 남긴 GameObject 활성값을 현재 방향 상태로 되돌립니다.
        playerDirection?.RefreshDirectionVisuals();
    }

    private float GetDirectClipDuration()
    {
        if (activeDownStrikeClip == null)
            return maxDownStrikeWaitDuration;

        return activeDownStrikeClip.length / Mathf.Max(0.01f, directClipSpeed);
    }

    private AnimationClip GetCurrentDownStrikeClip()
    {
        float dir = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;

        if (dir < 0f)
            return leftDownStrikeClip != null ? leftDownStrikeClip : downStrikeClip;

        return rightDownStrikeClip != null ? rightDownStrikeClip : downStrikeClip;
    }

    private void StopDirectClip()
    {
        if (!downStrikeGraph.IsValid())
            return;

        downStrikeGraph.Destroy();
        activeDownStrikeClip = null;
        RestoreDirectionAfterDirectClip();
    }

    private void OnDisable()
    {
        StopDirectClip();
        if (slashVfxRoutine != null)
            StopCoroutine(slashVfxRoutine);
        slashVfxRoutine = null;
        if (landingVfxRoutine != null)
            StopCoroutine(landingVfxRoutine);
        landingVfxRoutine = null;
        isUsingSkill = false;
    }

    private void ApplyDownStrikeHit()
    {
        float dir = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;

        Vector2 center = (Vector2)transform.position + new Vector2(
            hitBoxOffset.x * dir,
            hitBoxOffset.y);

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            center,
            hitBoxSize,
            0f,
            enemyMask);

        HashSet<GoblinHealth2D> damagedEnemies = new HashSet<GoblinHealth2D>();
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
                continue;

            GoblinHealth2D enemyHealth = hits[i].GetComponentInParent<GoblinHealth2D>();
            if (enemyHealth == null || !damagedEnemies.Add(enemyHealth))
                continue;

            enemyHealth.TakeDamage(damage, dir);
        }
    }

    private void OnDrawGizmosSelected()
    {
        float dir = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;

        Vector2 center = (Vector2)transform.position + new Vector2(
            hitBoxOffset.x * dir,
            hitBoxOffset.y);

        Gizmos.color = new Color(1f, 0.2f, 0f, 0.8f);
        Gizmos.DrawWireCube(center, hitBoxSize);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        LoadDirectionalClipsInEditor();
    }

    private void LoadDirectionalClipsInEditor()
    {
        if (rightDownStrikeClip == null)
            rightDownStrikeClip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(RightDownStrikeClipEditorPath);
        if (leftDownStrikeClip == null)
            leftDownStrikeClip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(LeftDownStrikeClipEditorPath);
    }
#endif
}
