using System.Collections;
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

    // 보스를 보여주는 시간
    [SerializeField] private float bossHoldDuration = 2f;

    [Header("Boss Intro Animation")]
    [SerializeField] private Animator bossAnimator;

    // CastU.anim 파일을 여기에 직접 넣습니다.
    [SerializeField] private AnimationClip bossCastClip;

    [Tooltip("CastU 클립 길이를 Boss Hold Duration에 맞춰 재생합니다.")]
    [SerializeField] private bool fitAnimationToHoldDuration = true;

    // =========================================================
    // ★ 보스 인트로 방향
    // =========================================================

    [Header("Boss Facing")]

    [Tooltip("인트로가 진행되는 동안 보스가 왼쪽을 바라보도록 강제합니다.")]
    [SerializeField] private bool forceBossFaceLeftDuringIntro = true;

    // 인트로 진행 중인지 확인
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

    // CastU 직접 재생용
    private PlayableGraph bossAnimationGraph;
    private bool bossAnimationGraphCreated;

    // =============================================================
    // Awake
    // =============================================================

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
    }

    // =============================================================
    // Start
    // =============================================================

    private IEnumerator Start()
    {
        // 씬 초기 배치가 끝난 후
        yield return null;

        gameplayCameraPosition = transform.position;
        gameplayCameraSize = targetCamera.orthographicSize;

        // =========================================================
        // 보스 자동 탐색
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

        // =========================================================
        // ★ 인트로 시작
        // ★ 보스를 왼쪽 방향으로 고정
        // =========================================================

        if (bossTarget != null)
        {
            introPlaying = true;

            ForceBossFaceLeft();

            Debug.Log(
                "[BossIntroCamera] Boss Intro 시작 - 보스 왼쪽 방향 고정"
            );
        }

        // =========================================================
        // 보스 Animator 자동 탐색
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
        // 1. 전체 화면 → 보스
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
            // 2. CastU 직접 재생
            // =====================================================

            PlayBossCastClip();

            // =====================================================
            // 3. CastU 재생과 동시에 카메라 Shake
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
            // 4. CastU 직접 재생 종료
            // =====================================================

            StopBossCastClip();
        }
        else
        {
            Debug.LogWarning(
                "[BossIntroCamera] 보스를 찾지 못했습니다."
            );
        }

        // =========================================================
        // 5. 로컬 플레이어 검색
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
        // 6. 보스 → 내 캐릭터
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
                "[BossIntroCamera] 로컬 플레이어를 찾지 못했습니다."
            );
        }

        // =========================================================
        // 7. 내 캐릭터 → 전체 전투 화면
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
        // ★ 인트로 종료
        // ★ 이제 보스 방향을 AI가 다시 제어할 수 있음
        // =========================================================

        introPlaying = false;

        Debug.Log(
            "[BossIntroCamera] Intro Complete - Boss Facing Control Released"
        );
    }

    // =============================================================
    // ★ 보스 왼쪽 방향 강제
    // =============================================================

    private void ForceBossFaceLeft()
    {
        if (!forceBossFaceLeftDuringIntro)
            return;

        if (bossTarget == null)
            return;

        Vector3 scale =
            bossTarget.localScale;

        // X 스케일을 음수로 만들어 왼쪽을 바라보게 함
        scale.x =
            -Mathf.Abs(scale.x);

        bossTarget.localScale =
            scale;
    }

    // =============================================================
    // ★ AI가 방향을 바꾸더라도
    // ★ 인트로 중에는 마지막에 다시 왼쪽으로 돌림
    // =============================================================

    private void LateUpdate()
    {
        if (!introPlaying)
            return;

        ForceBossFaceLeft();
    }

    // =============================================================
    // CastU AnimationClip 직접 재생
    // =============================================================

    private void PlayBossCastClip()
    {
        if (bossAnimator == null)
        {
            Debug.LogWarning(
                "[BossIntroCamera] Boss Animator를 찾지 못했습니다."
            );

            return;
        }

        if (bossCastClip == null)
        {
            Debug.LogWarning(
                "[BossIntroCamera] Boss Cast Clip이 비어 있습니다. CastU.anim을 Inspector에 넣어주세요."
            );

            return;
        }

        // 혹시 이전 Graph가 남아있다면 제거
        StopBossCastClip();

        // PlayableGraph 생성
        bossAnimationGraph =
            PlayableGraph.Create(
                "BossIntroCastU"
            );

        bossAnimationGraphCreated = true;

        bossAnimationGraph.SetTimeUpdateMode(
            DirectorUpdateMode.UnscaledGameTime
        );

        // CastU AnimationClip을 직접 Playable로 만듦
        AnimationClipPlayable clipPlayable =
            AnimationClipPlayable.Create(
                bossAnimationGraph,
                bossCastClip
            );

        // =========================================================
        // CastU 전체 길이를 bossHoldDuration에 맞춤
        // =========================================================

        if (
            fitAnimationToHoldDuration &&
            bossHoldDuration > 0f &&
            bossCastClip.length > 0f
        )
        {
            double speed =
                bossCastClip.length /
                bossHoldDuration;

            clipPlayable.SetSpeed(speed);

            Debug.Log(
                $"[BossIntroCamera] CastU 길이 {bossCastClip.length:F2}초 → {bossHoldDuration:F2}초에 맞춤 / Speed={speed:F2}"
            );
        }
        else
        {
            clipPlayable.SetSpeed(1.0);
        }

        // =========================================================
        // Animator에 직접 출력
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
            $"[BossIntroCamera] AnimationClip 직접 재생 시작 : {bossCastClip.name}"
        );
    }

    // =============================================================
    // CastU 직접 재생 종료
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

        Debug.Log(
            "[BossIntroCamera] CastU 직접 재생 종료"
        );
    }

    // =============================================================
    // 카메라 Shake
    // =============================================================

    private IEnumerator ShakeCamera(
        float duration
    )
    {
        Vector3 originalPosition =
            transform.position;

        float elapsed = 0f;

        float noiseSeedX =
            Random.Range(0f, 1000f);

        float noiseSeedY =
            Random.Range(0f, 1000f);

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
                (x - 0.5f) * 2f;

            y =
                (y - 0.5f) * 2f;

            Vector3 shakeOffset =
                new Vector3(
                    x,
                    y,
                    0f
                ) * shakeStrength;

            transform.position =
                originalPosition +
                shakeOffset;

            yield return null;
        }

        // 원위치
        transform.position =
            originalPosition;
    }

    // =============================================================
    // 로컬 플레이어 검색
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
    // 특정 대상에게 카메라 이동
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
    // 카메라 이동
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
                t * t *
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
    // 오브젝트 파괴 시 Graph 정리
    // =============================================================

    private void OnDestroy()
    {
        StopBossCastClip();
    }
}