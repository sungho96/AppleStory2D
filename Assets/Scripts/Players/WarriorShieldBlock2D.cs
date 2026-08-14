using System.Collections;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine;

public class WarriorShieldBlock2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerHealth2D playerHealth;

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
        float blockDuration = PlayShieldBlockAnimation();

        yield return new WaitForSeconds(blockDuration);

        isBlocking = false;
        nextUseTime = Time.time + cooldown;
        StopDirectClip();
        blockRoutine = null;
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

    private void OnDisable()
    {
        if (blockRoutine != null)
            StopCoroutine(blockRoutine);

        StopDirectClip();
        isBlocking = false;
        blockRoutine = null;
    }
}
