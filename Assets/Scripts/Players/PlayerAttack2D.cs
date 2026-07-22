using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack2D : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private Animator animator;              // 활 발사 애니메이션 제어용
    [SerializeField] private GameObject arrowPrefab;         // 발사할 화살 프리팹
    [SerializeField] private Transform firePoint;            // 화살 생성 위치
    [SerializeField] private float arrowSpeed = 12f;         // 화살 속도
    [SerializeField] private PlayerController2D playerController; // 바라보는 방향 참조용

    [Header("Attack Speed")]
    [SerializeField] private float baseAttackDelay = 0.4f;

    private float attackSpeedMultiplier = 1f;

    private bool isAttacking; // 연타 방지 (공격 중 추가 입력 차단 플래그)

    /// <summary>
    /// 초기 참조 설정.
    /// - PlayerController2D가 비어 있을 경우 자동으로 같은 오브젝트에서 가져옴
    /// - 공격 방향 계산(GetHorizontalFacingDir)에 사용됨
    /// </summary>
    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();
    }

    /// <summary>
    /// 입력 처리 루프.
    /// - LeftControl 입력 시 공격 코루틴 실행
    /// - isAttacking을 통해 연속 입력(스팸 공격) 방지
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) && !isAttacking)
        {
            StartCoroutine(DoBowShot());
        }
    }

    /// <summary>
    /// 활 발사 애니메이션 시퀀스.
    /// - ShotBow Bool을 1프레임만 true로 설정하여 애니메이션 트리거 역할 수행
    /// - 일정 시간 동안 입력을 잠가 중복 공격 방지
    /// - 실제 화살 생성은 Animation Event에서 FireArrow()로 처리하는 구조
    /// </summary>
    private IEnumerator DoBowShot()
    {
        isAttacking = true; // 공격 시작 → 입력 잠금

        animator.SetBool("ShotBow", true); // 발사 애니 시작
        yield return null;                 // 1프레임 유지
        animator.SetBool("ShotBow", false); // 애니 트리거 OFF

        float currentAttackDelay =
            baseAttackDelay / attackSpeedMultiplier;

        yield return new WaitForSeconds(currentAttackDelay);

        isAttacking = false; // 공격 종료 → 입력 해제
    }

    /// <summary>
    /// 화살 생성 및 발사 처리.
    /// - 플레이어 바라보는 방향(dir)에 따라 위치/회전 결정
    /// - 플레이어와 화살의 충돌을 무시하여 자기 자신과 부딪히는 문제 방지
    /// - Rigidbody2D velocity를 이용해 직선 발사
    /// </summary>
    public void FireArrow()
    {
        // 필수 참조 체크
        if (arrowPrefab == null || firePoint == null)
        {
            Debug.LogWarning("arrowPrefab 또는 firePoint가 연결되지 않았습니다.");
            return;
        }

        // 플레이어 방향 계산 (오른쪽: +1 / 왼쪽: -1)
        float dir = playerController != null ? playerController.GetHorizontalFacingDir() : 1f;

        // 발사 위치 (플레이어 앞쪽으로 약간 이동)
        Vector3 spawnPos = firePoint.position + new Vector3(dir * 0.3f, 0f, 0f);

        // 방향에 따른 회전 (스프라이트 기준 보정)
        Quaternion rot = dir > 0f
            ? Quaternion.Euler(0f, 0f, -90f) // 오른쪽
            : Quaternion.Euler(0f, 0f, 90f); // 왼쪽

        // 화살 생성
        GameObject arrow = Instantiate(arrowPrefab, spawnPos, rot);

        // 화살 콜라이더 가져오기
        Collider2D arrowCol = arrow.GetComponent<Collider2D>();

        // 플레이어의 모든 콜라이더 가져오기 (자식 포함)
        Collider2D[] playerCols = GetComponentsInChildren<Collider2D>();

        // 화살 스크립트 참조 (방향 설정용)
        ArrowProjectile2D arrowProjectile = arrow.GetComponent<ArrowProjectile2D>();

        // 방향 정보 전달 (화살 자체 로직에서 사용)
        if (arrowProjectile != null)
        {
            arrowProjectile.SetDirection(dir);
        }

        // 플레이어와 화살 충돌 무시 처리
        foreach (Collider2D col in playerCols)
        {
            if (arrowCol != null && col != null)
                Physics2D.IgnoreCollision(arrowCol, col, true);
        }

        // Rigidbody2D 기반 발사 (속도 직접 지정)
        Rigidbody2D rb = arrow.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = new Vector2(dir * arrowSpeed, 0f);
        }
    }
    /// <summary>
    /// 공격속도 배율 적용.
    /// 1.5 입력 시 공격 대기시간이 약 33% 감소합니다.
    /// </summary>
    public void SetAttackSpeedMultiplier(float multiplier)
    {
        attackSpeedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    /// <summary>
    /// 공격속도를 기본 상태로 복구합니다.
    /// </summary>
    public void ResetAttackSpeedMultiplier()
    {
        attackSpeedMultiplier = 1f;
    }
}