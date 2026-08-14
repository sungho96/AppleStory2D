using UnityEngine;

/// <summary>
/// [Codex 이동 고정 추적] Play 중 Transform/Rigidbody2D 위치가 어느 단계에서 되돌아가는지 확인하는 임시 진단 스크립트입니다.
/// 문제 오브젝트에 잠깐 붙여 Console 로그를 확인한 뒤 원인을 찾으면 제거하세요.
/// </summary>
public class TransformLockDebugger : MonoBehaviour
{
    [SerializeField] private bool logEveryFrame;
    [SerializeField] private float changeThreshold = 0.0001f;

    private Rigidbody2D rb;
    private Vector3 lastUpdatePosition;
    private Vector3 lastFixedPosition;
    private Vector3 lastLatePosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        lastUpdatePosition = transform.position;
        lastFixedPosition = transform.position;
        lastLatePosition = transform.position;

        Debug.Log(BuildMessage("Awake"));
    }

    private void Update()
    {
        LogIfChanged("Update", ref lastUpdatePosition);
    }

    private void FixedUpdate()
    {
        LogIfChanged("FixedUpdate", ref lastFixedPosition);
    }

    private void LateUpdate()
    {
        LogIfChanged("LateUpdate", ref lastLatePosition);
    }

    private void LogIfChanged(string phase, ref Vector3 previousPosition)
    {
        Vector3 currentPosition = transform.position;
        bool changed = Vector3.Distance(previousPosition, currentPosition) > changeThreshold;

        if (logEveryFrame || changed)
            Debug.Log(BuildMessage(phase, previousPosition, currentPosition));

        previousPosition = currentPosition;
    }

    private string BuildMessage(string phase)
    {
        return BuildMessage(phase, transform.position, transform.position);
    }

    private string BuildMessage(string phase, Vector3 previousPosition, Vector3 currentPosition)
    {
        string rbInfo = rb == null
            ? "Rigidbody2D=None"
            : $"Rigidbody2D position={rb.position}, velocity={rb.linearVelocity}, bodyType={rb.bodyType}, simulated={rb.simulated}, constraints={rb.constraints}";

        string inputInfo = $"input horizontal={Input.GetAxisRaw("Horizontal")}, left={Input.GetKey(KeyCode.LeftArrow)}, right={Input.GetKey(KeyCode.RightArrow)}, a={Input.GetKey(KeyCode.A)}, d={Input.GetKey(KeyCode.D)}";
        string componentInfo = BuildComponentInfo();

        string behavioursInfo = BuildBehavioursInfo();

        return $"[TransformLockDebugger] {name} phase={phase}, previous={previousPosition}, current={currentPosition}, parent={(transform.parent != null ? transform.parent.name : "None")}, {rbInfo}, {inputInfo}, {componentInfo}, behaviours={behavioursInfo}";
    }

    private string BuildComponentInfo()
    {
        PlayerController2D playerController = GetComponent<PlayerController2D>();
        PlayerMovement2D playerMovement = GetComponent<PlayerMovement2D>();
        PlayerLadder2D playerLadder = GetComponent<PlayerLadder2D>();
        PlayerHitReaction2D hitReaction = GetComponent<PlayerHitReaction2D>();
        GoblinBossCombatController2D bossCombat = GetComponent<GoblinBossCombatController2D>();
        GoblinController2D goblinController = GetComponent<GoblinController2D>();

        return $"components playerController={IsEnabled(playerController)}, playerMovement={IsEnabled(playerMovement)}, ladder={IsEnabled(playerLadder)} climbing={(playerLadder != null && playerLadder.IsClimbing)}, grounded={(playerLadder != null && playerLadder.IsGrounded)}, knockback={(hitReaction != null && hitReaction.IsKnockback)}, bossCombat={IsEnabled(bossCombat)}, goblinController={IsEnabled(goblinController)}";
    }

    private string IsEnabled(Behaviour behaviour)
    {
        if (behaviour == null)
            return "None";

        return behaviour.enabled ? "On" : "Off";
    }

    private string BuildBehavioursInfo()
    {
        Behaviour[] behaviours = GetComponents<Behaviour>();
        if (behaviours == null || behaviours.Length == 0)
            return "None";

        string result = "";
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            if (result.Length > 0)
                result += "|";

            result += behaviour.GetType().Name + ":" + (behaviour.enabled ? "On" : "Off");
        }

        return result;
    }
}
