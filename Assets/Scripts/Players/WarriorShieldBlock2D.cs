using System.Collections;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine;

public class WarriorShieldBlock2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerHealth2D playerHealth;
    [SerializeField] private WarriorShieldBlockVisualFeedback shieldBlockVisualFeedback;

    [Header("Skill")]
    [SerializeField] private KeyCode fallbackKey = KeyCode.None;
    [SerializeField] private float duration = 1.2f;
    [SerializeField] private float cooldown = 3f;
    [SerializeField, Range(0f, 1f)] private float damageMultiplier = 0.25f;

    [Header("Animator")]
    [SerializeField] private AnimationClip shieldBlockClip;
    [SerializeField] private float directClipSpeed = 1f;
    [SerializeField] private string shieldBlockStateName = "ShieldBlock";
    [SerializeField] private float fadeDuration = 0.05f;

    private bool isBlocking;
    private float nextUseTime;
    private Coroutine blockRoutine;
    private PlayableGraph shieldBlockGraph;

    public bool IsBlocking => isBlocking;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth2D>();
        if (shieldBlockVisualFeedback == null)
            shieldBlockVisualFeedback = GetComponent<WarriorShieldBlockVisualFeedback>();
        if (shieldBlockVisualFeedback == null)
            shieldBlockVisualFeedback = gameObject.AddComponent<WarriorShieldBlockVisualFeedback>();
        if (shieldBlockVisualFeedback != null)
            shieldBlockVisualFeedback.Initialize();
    }

    private void Update()
    {
        if (fallbackKey == KeyCode.None || !Input.GetKeyDown(fallbackKey))
            return;

        UseShieldBlock();
    }

    public void UseShieldBlock()
    {
        if (isBlocking || Time.time < nextUseTime)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        blockRoutine = StartCoroutine(ShieldBlockRoutine());
    }

    public void StartShieldBlockForCapture(float holdDuration)
    {
        // [Codex CaptureShieldBot] 촬영용 자동 방어도 기존 ShieldBlock 시작 흐름을 그대로 재사용합니다.
        if (isBlocking || Time.time < nextUseTime)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        blockRoutine = StartCoroutine(ShieldBlockRoutine(Mathf.Max(0f, holdDuration), true));
    }

    public void StopShieldBlockForCapture()
    {
        // [Codex CaptureShieldBot] 촬영 기능이 꺼질 때 기존 ShieldBlock 종료 정리만 안전하게 호출합니다.
        StopShieldBlockImmediately();
    }

    public int ReduceDamage(int rawDamage)
    {
        if (!isBlocking)
            return rawDamage;

        // [Codex Warrior ShieldBlock] 방어 중일 때만 HP 감소량을 줄이고 피격 흐름은 PlayerHealth2D에 맡깁니다.
        return Mathf.Max(1, Mathf.RoundToInt(rawDamage * damageMultiplier));
    }

    private IEnumerator ShieldBlockRoutine()
    {
        isBlocking = true;
        float animationDuration = PlayShieldBlockAnimation();
        ShowShieldBlockBarrierVfx();

        yield return new WaitForSeconds(Mathf.Max(duration, animationDuration));

        FinishShieldBlock();
    }

    private IEnumerator ShieldBlockRoutine(float blockDuration, bool playAnimation)
    {
        isBlocking = true;
        if (playAnimation)
            PlayShieldBlockAnimation();
        ShowShieldBlockBarrierVfx();

        yield return new WaitForSeconds(blockDuration);

        FinishShieldBlock();
    }

    private void ShowShieldBlockBarrierVfx()
    {
        // [Codex ShieldBlock VFX] 기존 방어 상태 시작 지점에 로컬 Barrier 표시만 붙입니다.
        shieldBlockVisualFeedback?.ShowShieldBlockBarrier();
    }

    private void HideShieldBlockBarrierVfx()
    {
        // [Codex ShieldBlock VFX] 방어 종료 시 데미지/쿨타임 로직과 분리해서 Barrier만 숨깁니다.
        shieldBlockVisualFeedback?.HideShieldBlockBarrier();
    }

    private float PlayShieldBlockAnimation()
    {
        if (animator == null)
            return duration;

        if (shieldBlockClip != null)
        {
            PlayDirectClip(shieldBlockClip, directClipSpeed);
            return Mathf.Max(duration, shieldBlockClip.length / Mathf.Max(0.01f, directClipSpeed));
        }

        PlayAnimatorState(shieldBlockStateName, fadeDuration);
        return duration;
    }

    private void PlayAnimatorState(string stateName, float fade)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        int stateHash = Animator.StringToHash(stateName);
        for (int layer = animator.layerCount - 1; layer >= 0; layer--)
        {
            if (!animator.HasState(layer, stateHash))
                continue;

            animator.CrossFade(stateHash, fade, layer);
            return;
        }
    }

    private void PlayDirectClip(AnimationClip clip, float speed)
    {
        StopDirectClip();

        // [Codex Warrior Direct Clip] Inspector에 넣은 방패막기 클립을 Animator Controller 상태 전환 없이 그대로 재생합니다.
        shieldBlockGraph = PlayableGraph.Create("WarriorShieldBlockClip");
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(
            shieldBlockGraph,
            "ShieldBlockOutput",
            animator);
        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(shieldBlockGraph, clip);
        clipPlayable.SetSpeed(Mathf.Max(0.01f, speed));
        output.SetSourcePlayable(clipPlayable);
        shieldBlockGraph.Play();
    }

    private void StopDirectClip()
    {
        if (shieldBlockGraph.IsValid())
            shieldBlockGraph.Destroy();
    }

    private void FinishShieldBlock()
    {
        isBlocking = false;
        HideShieldBlockBarrierVfx();
        nextUseTime = Time.time + cooldown;
        StopDirectClip();
        blockRoutine = null;
    }

    private void StopShieldBlockImmediately()
    {
        if (blockRoutine != null)
            StopCoroutine(blockRoutine);

        StopDirectClip();
        HideShieldBlockBarrierVfx();
        isBlocking = false;
        blockRoutine = null;
    }

    private void OnDisable()
    {
        StopShieldBlockImmediately();
    }
}
