using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoblinHealth2D : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 100;     // 고블린의 최대 체력
    [SerializeField] private int currentHp;       // 현재 체력

    [Header("Hit")]
    [SerializeField] private Color hitColor = Color.red;   // 피격 시 잠시 변경할 색상
    [SerializeField] private float hitColorDuration = 0.2f; // 피격 색상 유지 시간
    [SerializeField] private float hitStunDuration = 0.25f; // 피격 경직 관련 시간값 (현재 이 스크립트에서는 직접 사용하지 않음)

    private GoblinController2D goblinController; // 이동/넉백/경직 처리를 맡는 컨트롤러 참조
    private SpriteRenderer[] renderers;          // 자신 및 자식에 있는 모든 SpriteRenderer
    private Color[] originalColors;              // 각 SpriteRenderer의 원래 색상 저장용
    private bool isHitStun;                      // 피격 경직 상태용 변수 (현재 이 스크립트에서는 직접 사용하지 않음)

    /// <summary>
    /// 초기 참조 캐싱.
    /// - GoblinController2D를 연결해 피격 시 이동/넉백 로직을 호출할 준비를 함
    /// - 자식 포함 SpriteRenderer를 모두 수집
    /// - 피격 후 원래 색으로 복구하기 위해 시작 색상을 배열에 저장
    /// </summary>
    private void Awake()
    {
        goblinController = GetComponent<GoblinController2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                originalColors[i] = renderers[i].color;
        }
    }

    /// <summary>
    /// 시작 시 체력을 최대 체력으로 초기화.
    /// - 스폰 직후 currentHp를 maxHp 기준으로 맞춤
    /// </summary>
    void Start()
    {
        currentHp = maxHp;
    }

    /// <summary>
    /// 데미지 처리.
    /// - 현재 체력에서 damage만큼 차감
    /// - 체력이 0 이하가 되면 오브젝트 제거
    /// - 생존 시 피격 색상 효과 실행
    /// - GoblinController2D가 있으면 경직/넉백 연출도 함께 호출
    /// </summary>
    public void TakeDamage(int damage, float hitDir)
    {
        currentHp -= damage;
        Debug.Log($"currentHp:{currentHp}");

        if (currentHp <= 0)
        {
            Destroy(gameObject);
            return;
        }

        StartCoroutine(CoHitColor());

        if (goblinController != null)
        {
            goblinController.PlayHitStun();
            goblinController.PlayKnockback(hitDir);
        }
    }

    /// <summary>
    /// 피격 색상 연출 코루틴.
    /// - 모든 SpriteRenderer를 hitColor로 잠시 변경
    /// - 일정 시간 후 Awake에서 저장한 원래 색상으로 복구
    /// </summary>
    private IEnumerator CoHitColor()
    {
        foreach (SpriteRenderer sr in renderers)
        {
            if (sr != null)
                sr.color = hitColor;
        }

        yield return new WaitForSeconds(hitColorDuration);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = originalColors[i];
        }
    }
}