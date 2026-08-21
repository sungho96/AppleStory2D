using System.Collections;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[RequireComponent(typeof(Camera))]
public class BossIntroCameraSequence : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform bossTarget;
    [SerializeField] private string playerTag = "Player";

    [Header("Boss Camera")]
    [SerializeField] private Vector3 bossOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float bossZoomSize = 3.2f;
    [SerializeField] private float moveToBossDuration = 1.6f;

    [SerializeField] private float bossHoldDuration = 3.2f;

    [Header("Boss Intro Animation")]
    [SerializeField] private Animator bossAnimator;

    [SerializeField] private AnimationClip bossCastClip;

    [Tooltip("CastU 클립 1회를 Boss Hold Duration에 맞춰 재생합니다.")]
    [SerializeField] private bool fitAnimationToHoldDuration = true;

    [Tooltip("켜면 Boss Hold Duration 동안 Boss Cast Clip을 반복 재생합니다.")]
    [SerializeField] private bool loopBossCastClipDuringHold = true;

    // =============================================================
    // ���� ��Ʈ�� ����
    // =============================================================
    [Header("Boss Facing")]

    // ���� ��Ʈ�ΰ� ���� ������
    private bool introPlaying = false;

    [Header("Boss Camera Shake")]
    [SerializeField] private bool useCameraShake = true;

    [SerializeField] private float shakeStrength = 0.1f;

    [SerializeField] private float shakeFrequency = 22f;

    [Header("Player Camera")]
    [SerializeField] private Vector3 playerOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float playerZoomSize = 3.5f;
    [SerializeField] private float moveToPlayerDuration = 0.9f;
    [SerializeField] private float playerHoldDuration = 0.5f;

    [Header("Return To Battle")]
    [SerializeField] private float returnDuration = 1.2f;

    [Header("Network")]
    [SerializeField] private float playerFindTimeout = 5f;

    private Camera targetCamera;

    private Vector3 gameplayCameraPosition;
    private float gameplayCameraSize;
    private GoblinBossCombatController2D bossCombat;
    private AnimationManager bossAnimationManager;

    // CastU ���� �����
    private PlayableGraph bossAnimationGraph;
    private AnimationClipPlayable bossClipPlayable;
    private bool bossAnimationGraphCreated;
    private bool bossClipLooping;
    private float bossClipLoopStartTime;
    private float bossClipLoopLength;

    // =============================================================
    // Awake
    // =============================================================

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (!bossClipLooping || !bossAnimationGraphCreated || !bossAnimationGraph.IsValid() || bossClipLoopLength <= 0f)
            return;

        // [Codex Boss Intro Clip Loop] Boss Hold Duration 동안 클립을 늘리지 않고 원래 속도로 반복 재생합니다.
        float elapsed = Time.unscaledTime - bossClipLoopStartTime;
        bossClipPlayable.SetTime(elapsed % bossClipLoopLength);
        bossAnimationGraph.Evaluate(Time.unscaledDeltaTime);
    }

    // =============================================================
    // Start
    // =============================================================

    private IEnumerator Start()
    {
        // �� �ʱ� ��ġ�� ���� ������ �� ������ ���
        yield return null;

        gameplayCameraPosition = transform.position;
        gameplayCameraSize = targetCamera.orthographicSize;

        // =========================================================
        // 0. ���� �ڵ� Ž��
        // =========================================================

        if (bossTarget == null)
        {
            GoblinBossCombatController2D boss =
                FindFirstObjectByType<GoblinBossCombatController2D>();

            if (boss != null)
            {
                bossTarget = boss.transform;
            }
        }

        if (bossTarget != null)
        {
            bossCombat = bossTarget.GetComponent<GoblinBossCombatController2D>();
            bossAnimationManager = bossTarget.GetComponent<AnimationManager>();
        }

        // =========================================================
        // �� ���� ���� ���� ����
        // =========================================================

        if (bossTarget != null)
        {
            if (bossCombat != null)
                bossCombat.SetIntroLocked(true);

            // ��Ʈ�� ����
            introPlaying = true;

            Debug.Log(
                "[BossIntroCamera] Intro Start"
            );
        }
        else
        {
            Debug.LogWarning(
                "[BossIntroCamera] ������ ã�� ���߽��ϴ�."
            );
        }

        // =========================================================
        // ���� Animator �ڵ� Ž��
        // =========================================================

        if (bossAnimator == null && bossTarget != null)
        {
            bossAnimator =
                bossTarget.GetComponent<Animator>();

            if (bossAnimator == null)
            {
                bossAnimator =
                    bossTarget.GetComponentInChildren<Animator>();
            }
        }

        // =========================================================
        // 1. ��ü ȭ�� �� ����
        // =========================================================

        if (bossTarget != null)
        {
            yield return MoveToTarget(
                bossTarget,
                bossOffset,
                bossZoomSize,
                moveToBossDuration
            );

            // =====================================================
            // 2. CastU ���� ���
            // =====================================================

            PlayBossCastClip();

            // =====================================================
            // 3. CastU + ī�޶� Shake
            // =====================================================

            if (useCameraShake)
            {
                yield return ShakeCamera(
                    bossHoldDuration
                );
            }
            else
            {
                yield return new WaitForSecondsRealtime(
                    bossHoldDuration
                );
            }

            // =====================================================
            // 4. CastU ����
            // =====================================================

            StopBossCastClip();
        }

        // =========================================================
        // 5. ���� �÷��̾� �˻�
        // =========================================================

        Transform localPlayer = null;

        float timer = 0f;

        while (
            localPlayer == null &&
            timer < playerFindTimeout
        )
        {
            localPlayer =
                FindLocalPlayer();

            timer +=
                Time.unscaledDeltaTime;

            yield return null;
        }

        // =========================================================
        // 6. ���� �� �� ĳ����
        // =========================================================

        if (localPlayer != null)
        {
            yield return MoveToTarget(
                localPlayer,
                playerOffset,
                playerZoomSize,
                moveToPlayerDuration
            );

            yield return new WaitForSecondsRealtime(
                playerHoldDuration
            );
        }
        else
        {
            Debug.LogWarning(
                "[BossIntroCamera] ���� �÷��̾ ã�� ���߽��ϴ�."
            );
        }

        // =========================================================
        // 7. �� ĳ���� �� ��ü ���� ȭ��
        // =========================================================

        yield return MoveCamera(
            gameplayCameraPosition,
            gameplayCameraSize,
            returnDuration
        );

        transform.position =
            gameplayCameraPosition;

        targetCamera.orthographicSize =
            gameplayCameraSize;

        // =========================================================
        // �ڡڡ� �߿�
        // ��Ʈ�� ����
        // =========================================================

        EndBossIntro();

        Debug.Log(
            "[BossIntroCamera] Intro Complete"
        );
    }
    // =============================================================
    // �ڡڡ� ���� ��Ʈ�� ����
    // =============================================================

    private void EndBossIntro()
    {
        // =========================================================
        // ���� ���� ������ ��
        // =========================================================

        introPlaying = false;

        if (bossCombat != null)
            bossCombat.SetIntroLocked(false);

        Debug.Log(
            "[BossIntroCamera] Boss Facing Control Released"
        );
    }

    // =============================================================
    // CastU AnimationClip ���� ���
    // =============================================================

    private void PlayBossCastClip()
    {
        if (bossAnimator == null)
        {
            Debug.LogWarning(
                "[BossIntroCamera] Boss Animator�� ã�� ���߽��ϴ�."
            );

            return;
        }

        if (bossCastClip == null)
        {
            Debug.LogWarning(
                "[BossIntroCamera] Boss Cast Clip�� ��� �ֽ��ϴ�. HeroEditor BossCast fallback�� ����մϴ�."
            );

            PlayBossCastFallback();
            return;
        }

        // ���� Graph ����
        StopBossCastClip();

        bossAnimationGraph =
            PlayableGraph.Create(
                "BossIntroCastU"
            );

        bossAnimationGraphCreated = true;

        bossAnimationGraph.SetTimeUpdateMode(
            DirectorUpdateMode.UnscaledGameTime
        );

        AnimationClipPlayable clipPlayable =
            AnimationClipPlayable.Create(
                bossAnimationGraph,
                bossCastClip
            );

        bossClipPlayable = clipPlayable;
        bossClipLooping = false;
        bossClipLoopLength = bossCastClip.length;

        // =========================================================
        // CastU ���̸� Hold Duration�� ����
        // =========================================================

        if (
            loopBossCastClipDuringHold &&
            bossCastClip.length > 0f
        )
        {
            clipPlayable.SetSpeed(0.0);
            clipPlayable.SetTime(0.0);
            bossClipLooping = true;
            bossClipLoopStartTime = Time.unscaledTime;
        }
        else if (
            fitAnimationToHoldDuration &&
            bossHoldDuration > 0f &&
            bossCastClip.length > 0f
        )
        {
            double speed =
                bossCastClip.length /
                bossHoldDuration;

            clipPlayable.SetSpeed(
                speed
            );

            Debug.Log(
                $"[BossIntroCamera] CastU ���� {bossCastClip.length:F2}�� �� {bossHoldDuration:F2}�ʿ� ���� / Speed={speed:F2}"
            );
        }
        else
        {
            clipPlayable.SetSpeed(
                1.0
            );
        }

        // =========================================================
        // Animator ���
        // =========================================================

        AnimationPlayableOutput output =
            AnimationPlayableOutput.Create(
                bossAnimationGraph,
                "BossCastUOutput",
                bossAnimator
            );

        output.SetSourcePlayable(
            clipPlayable
        );

        bossAnimationGraph.Play();

        Debug.Log(
            $"[BossIntroCamera] AnimationClip ���� ��� ���� : {bossCastClip.name}"
        );
    }

    private void PlayBossCastFallback()
    {
        if (bossAnimationManager == null && bossTarget != null)
            bossAnimationManager = bossTarget.GetComponent<AnimationManager>();

        if (bossAnimationManager == null)
            return;

        // [Codex Boss Intro Cast Fallback] Inspector 클립이 없을 때도 기존 HeroEditor Cast 동작을 한 번 재생해 인트로가 비지 않게 합니다.
        bossAnimationManager.SetState(CharacterState.Idle);
        bossAnimationManager.BossCast();
    }

    // =============================================================
    // CastU ���� ��� ����
    // =============================================================

    private void StopBossCastClip()
    {
        if (!bossAnimationGraphCreated)
            return;

        if (bossAnimationGraph.IsValid())
        {
            bossAnimationGraph.Destroy();
        }

        bossAnimationGraphCreated = false;
        bossClipLooping = false;
        bossClipLoopLength = 0f;

        Debug.Log(
            "[BossIntroCamera] CastU ���� ��� ����"
        );
    }

    // =============================================================
    // ī�޶� Shake
    // =============================================================

    private IEnumerator ShakeCamera(
        float duration
    )
    {
        Vector3 originalPosition =
            transform.position;

        float elapsed = 0f;

        float noiseSeedX =
            Random.Range(
                0f,
                1000f
            );

        float noiseSeedY =
            Random.Range(
                0f,
                1000f
            );

        while (elapsed < duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float noiseTime =
                Time.unscaledTime *
                shakeFrequency;

            float x =
                Mathf.PerlinNoise(
                    noiseSeedX + noiseTime,
                    0f
                );

            float y =
                Mathf.PerlinNoise(
                    0f,
                    noiseSeedY + noiseTime
                );

            x =
                (x - 0.5f) *
                2f;

            y =
                (y - 0.5f) *
                2f;

            Vector3 shakeOffset =
                new Vector3(
                    x,
                    y,
                    0f
                ) *
                shakeStrength;

            transform.position =
                originalPosition +
                shakeOffset;

            yield return null;
        }

        transform.position =
            originalPosition;
    }

    // =============================================================
    // ���� �÷��̾� �˻�
    // =============================================================

    private Transform FindLocalPlayer()
    {
        GameObject[] players;

        try
        {
            players =
                GameObject.FindGameObjectsWithTag(
                    playerTag
                );
        }
        catch
        {
            return null;
        }

        foreach (
            GameObject player in players
        )
        {
            NetworkObject networkObject =
                player.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                networkObject =
                    player.GetComponentInParent<NetworkObject>();
            }

            if (networkObject == null)
                continue;

            if (!networkObject.IsSpawned)
                continue;

            if (networkObject.IsOwner)
            {
                return player.transform;
            }
        }

        return null;
    }

    // =============================================================
    // Ư�� ��󿡰� ī�޶� �̵�
    // =============================================================

    private IEnumerator MoveToTarget(
        Transform target,
        Vector3 offset,
        float zoomSize,
        float duration
    )
    {
        Vector3 destination =
            target.position +
            offset;

        destination.z =
            gameplayCameraPosition.z;

        yield return MoveCamera(
            destination,
            zoomSize,
            duration
        );
    }

    // =============================================================
    // ī�޶� �̵�
    // =============================================================

    private IEnumerator MoveCamera(
        Vector3 destination,
        float destinationSize,
        float duration
    )
    {
        Vector3 startPosition =
            transform.position;

        float startSize =
            targetCamera.orthographicSize;

        if (duration <= 0f)
        {
            transform.position =
                destination;

            targetCamera.orthographicSize =
                destinationSize;

            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );

            float smoothT =
                t *
                t *
                (3f - 2f * t);

            transform.position =
                Vector3.Lerp(
                    startPosition,
                    destination,
                    smoothT
                );

            targetCamera.orthographicSize =
                Mathf.Lerp(
                    startSize,
                    destinationSize,
                    smoothT
                );

            yield return null;
        }

        transform.position =
            destination;

        targetCamera.orthographicSize =
            destinationSize;
    }

    // =============================================================
    // �ı� �� ����
    // =============================================================

    private void OnDestroy()
    {
        // Ȥ�� ��Ʈ�� ���� Camera�� �ı��Ǵ� ��쿡��
        // ���� Scale�� ������� ����
        introPlaying = false;

        if (bossCombat != null)
            bossCombat.SetIntroLocked(false);

        StopBossCastClip();
    }
}
