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

    [SerializeField] private float bossHoldDuration = 3.2f;

    [Header("Boss Intro Animation")]
    [SerializeField] private Animator bossAnimator;

    [SerializeField] private AnimationClip bossCastClip;

    [Tooltip("CastU 클립 길이를 Boss Hold Duration에 맞춰 재생합니다.")]
    [SerializeField] private bool fitAnimationToHoldDuration = true;

    // =============================================================
    // 보스 인트로 방향
    // =============================================================

    [Header("Boss Facing")]

    [Tooltip("인트로 진행 중에만 보스를 왼쪽으로 고정합니다.")]
    [SerializeField]
    private bool forceBossFaceLeftDuringIntro = true;

    // 현재 인트로가 진행 중인지
    private bool introPlaying = false;

    // ★ 인트로 시작 전 원래 보스 Scale
    private Vector3 bossOriginalScale;

    // ★ 원래 Scale을 정상적으로 저장했는지
    private bool bossOriginalScaleSaved = false;

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
        // 씬 초기 배치가 끝날 때까지 한 프레임 대기
        yield return null;

        gameplayCameraPosition = transform.position;
        gameplayCameraSize = targetCamera.orthographicSize;

        // =========================================================
        // 0. 보스 자동 탐색
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
        // ★ 보스 원래 방향 저장
        // =========================================================

        if (bossTarget != null)
        {
            bossOriginalScale =
                bossTarget.localScale;

            bossOriginalScaleSaved = true;

            Debug.Log(
                $"[BossIntroCamera] Boss Original Scale 저장 : {bossOriginalScale}"
            );

            // 인트로 시작
            introPlaying = true;

            // 처음부터 왼쪽으로
            ForceBossFaceLeft();

            Debug.Log(
                "[BossIntroCamera] Intro Start - Boss Face Left"
            );
        }
        else
        {
            Debug.LogWarning(
                "[BossIntroCamera] 보스를 찾지 못했습니다."
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
            // 3. CastU + 카메라 Shake
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
            // 4. CastU 종료
            // =====================================================

            StopBossCastClip();
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
        // ★★★ 중요
        // 인트로 종료
        // =========================================================

        EndBossIntro();

        Debug.Log(
            "[BossIntroCamera] Intro Complete"
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

        // =========================================================
        // 왼쪽 방향
        //
        // 현재 캐릭터가 반대로 나온다면
        //
        // -Mathf.Abs(scale.x)
        //
        // 를
        //
        // Mathf.Abs(scale.x)
        //
        // 로 변경하면 됨
        // =========================================================

        scale.x =
            -Mathf.Abs(scale.x);

        bossTarget.localScale =
            scale;
    }

    // =============================================================
    // ★ 인트로 동안만 방향 유지
    // =============================================================

    private void LateUpdate()
    {
        if (!introPlaying)
            return;

        ForceBossFaceLeft();
    }

    // =============================================================
    // ★★★ 보스 인트로 종료
    // =============================================================

    private void EndBossIntro()
    {
        // =========================================================
        // 먼저 방향 강제를 끔
        // =========================================================

        introPlaying = false;

        // =========================================================
        // 인트로 시작 전에 저장했던 원래 Scale 복구
        // =========================================================

        if (
            bossTarget != null &&
            bossOriginalScaleSaved
        )
        {
            bossTarget.localScale =
                bossOriginalScale;

            Debug.Log(
                $"[BossIntroCamera] Boss Scale 원상복구 : {bossOriginalScale}"
            );
        }

        Debug.Log(
            "[BossIntroCamera] Boss Facing Control Released"
        );
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

        // 이전 Graph 제거
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

        // =========================================================
        // CastU 길이를 Hold Duration에 맞춤
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

            clipPlayable.SetSpeed(
                speed
            );

            Debug.Log(
                $"[BossIntroCamera] CastU 길이 {bossCastClip.length:F2}초 → {bossHoldDuration:F2}초에 맞춤 / Speed={speed:F2}"
            );
        }
        else
        {
            clipPlayable.SetSpeed(
                1.0
            );
        }

        // =========================================================
        // Animator 출력
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
    // 파괴 시 정리
    // =============================================================

    private void OnDestroy()
    {
        // 혹시 인트로 도중 Camera가 파괴되는 경우에도
        // 보스 Scale을 원래대로 복구
        if (
            introPlaying &&
            bossTarget != null &&
            bossOriginalScaleSaved
        )
        {
            bossTarget.localScale =
                bossOriginalScale;
        }

        introPlaying = false;

        StopBossCastClip();
    }
}