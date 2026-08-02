using UnityEngine;

public class PlayerQuickStep2D : MonoBehaviour
{
    [Header("Double Tap")]
    [SerializeField] private float doubleTapWindow = 0.25f;

    [Header("Step")]
    [SerializeField] private float forwardStepDistance = 2.2f;
    [SerializeField] private float stepDuration = 0.15f;
    [SerializeField] private float stepCooldown = 0.35f;
    [SerializeField] private float collisionSkin = 0.05f;

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[12];

    private Rigidbody2D rb;
    private CapsuleCollider2D playerCollider;
    private QuickStepVisualFeedback quickStepVisual;

    private float lastTapTime = float.NegativeInfinity;
    private float lastTapDirection;
    private float nextStepAllowedTime;

    private float stepStartX;
    private float stepTargetX;
    private float stepElapsed;

    public bool IsStepping { get; private set; }
    public float StepDirection { get; private set; } = 1f;

    public void Initialize(
        Rigidbody2D targetRb,
        CapsuleCollider2D targetCollider,
        AudioClip quickStepSound)
    {
        rb = targetRb;
        playerCollider = targetCollider;

        // [퀵 스텝 연출 추가] 씬 참조 없이 전용 잔상·속도선 연출을 한 번만 연결합니다.
        quickStepVisual = GetComponent<QuickStepVisualFeedback>();
        if (quickStepVisual == null)
        {
            quickStepVisual = gameObject.AddComponent<QuickStepVisualFeedback>();
        }

        quickStepVisual.Initialize(transform, quickStepSound);
    }

    public bool RegisterDirectionTap(
        float inputDirection,
        bool canStep)
    {
        inputDirection = Mathf.Sign(inputDirection);

        bool isSameDirection = Mathf.Approximately(inputDirection, lastTapDirection);
        bool isInsideWindow = Time.time - lastTapTime <= doubleTapWindow;

        if (isSameDirection && isInsideWindow)
        {
            // 세 번째 입력이 이전 더블 탭의 두 번째 입력으로 이어지지 않도록 기록을 초기화합니다.
            lastTapDirection = 0f;
            lastTapTime = float.NegativeInfinity;

            if (!canStep || IsStepping || Time.time < nextStepAllowedTime)
            {
                return false;
            }

            // 첫 입력에서 이미 방향을 전환했으므로 두 번째 입력 방향 그대로 전진 스텝합니다.
            StartStep(inputDirection, forwardStepDistance);
            return IsStepping;
        }

        lastTapDirection = inputDirection;
        lastTapTime = Time.time;
        return false;
    }

    public void HandleStepMove()
    {
        if (!IsStepping || rb == null)
        {
            return;
        }

        stepElapsed += Time.fixedDeltaTime;
        float normalizedTime = Mathf.Clamp01(stepElapsed / stepDuration);

        // 빠르게 출발하고 끝부분에서 부드럽게 감속하는 Ease-Out 이동입니다.
        float easedTime = 1f - Mathf.Pow(1f - normalizedTime, 2f);
        float nextX = Mathf.Lerp(stepStartX, stepTargetX, easedTime);

        rb.MovePosition(new Vector2(nextX, rb.position.y));

        if (normalizedTime >= 1f)
        {
            IsStepping = false;
            nextStepAllowedTime = Time.time + stepCooldown;
            quickStepVisual?.PlayEnd();
        }
    }

    private void StartStep(float stepDirection, float requestedDistance)
    {
        if (rb == null || playerCollider == null)
        {
            return;
        }

        float safeDistance = GetSafeStepDistance(stepDirection, requestedDistance);
        if (safeDistance <= 0.01f)
        {
            return;
        }

        stepStartX = rb.position.x;
        stepTargetX = stepStartX + stepDirection * safeDistance;
        stepElapsed = 0f;
        StepDirection = stepDirection;
        IsStepping = true;
        quickStepVisual?.PlayStart(stepDirection);

        // 이전 일반 이동 속도가 스텝 거리에 섞이지 않도록 수평 속도만 정리합니다.
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private float GetSafeStepDistance(float direction, float requestedDistance)
    {
        ContactFilter2D contactFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = false
        };

        int hitCount = playerCollider.Cast(
            new Vector2(direction, 0f),
            contactFilter,
            castHits,
            requestedDistance);

        float safeDistance = requestedDistance;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = castHits[i];
            if (hit.collider == null || hit.collider.transform.root == transform.root)
            {
                continue;
            }

            // 발밑 바닥 접촉은 무시하고 진행 방향을 막는 벽 성분만 거리 제한에 사용합니다.
            if (Mathf.Abs(hit.normal.x) < 0.5f)
            {
                continue;
            }

            safeDistance = Mathf.Min(
                safeDistance,
                Mathf.Max(0f, hit.distance - collisionSkin));
        }

        return safeDistance;
    }
}
