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

    private bool isAttacking; // 연타 방지(발사 중 중복 입력 차단)

    /// <summary>
    /// 참조 초기화.
    /// - PlayerController2D가 비어있으면 같은 오브젝트에서 자동 연결
    /// </summary>
    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();
    }

    /// <summary>
    /// 입력 처리.
    /// - LeftControl 입력 시 공격 코루틴 시작(연타 방지 포함)
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) && !isAttacking)
        {
            StartCoroutine(DoBowShot());
        }
    }

    /// <summary>
    /// 활 발사 시퀀스(애니메이션 트리거용).
    /// - ShotBow bool을 1프레임만 켰다가 끄는 방식으로 발사 애니를 1회 유도
    /// - 애니 재생 동안 isAttacking으로 중복 입력 차단 
    /// (발사체 생성 FireArrow는 보통 애니메이션 이벤트에서 호출)
    /// </summary>
    private IEnumerator DoBowShot()
    {
        isAttacking = true;

        animator.SetBool("ShotBow", true);
        yield return null; // 1프레임만 true로 유지
        animator.SetBool("ShotBow", false);

        // 발사 애니의 타이밍에 맞춰 입력 잠금 유지
        yield return new WaitForSeconds(0.4f);
        isAttacking = false;
    }

    /// <summary>
    /// 화살 생성 및 발사.
    /// - firePoint 기준으로 바라보는 방향(dir)에 따라 스폰 위치/회전 설정
    /// - 플레이어 콜라이더와 화살 콜라이더는 서로 충돌 무시 처리
    /// - Rigidbody2D 속도로 수평 발사
    /// </summary>
    public void FireArrow()
    {
        if (arrowPrefab == null || firePoint == null)
        {
            Debug.LogWarning("arrowPrefab 또는 firePoint가 연결되지 않았습니다.");
            return;
        }

        // 플레이어가 바라보는 방향(오른쪽: +1 / 왼쪽: -1)
        float dir = playerController != null ? playerController.GetHorizontalFacingDir() : 1f;

        // 스폰 위치: firePoint에서 살짝 앞쪽으로 오프셋
        Vector3 spawnPos = firePoint.position + new Vector3(dir * 0.3f, 0f, 0f);

        // 스프라이트/프리팹 기준에 맞춘 회전 보정(좌/우 반전)
        Quaternion rot = dir > 0f
            ? Quaternion.Euler(0f, 0f, -90f)
            : Quaternion.Euler(0f, 0f, 90f);

        GameObject arrow = Instantiate(arrowPrefab, spawnPos, rot);

        // 플레이어와 화살이 서로 부딪혀서 즉시 튕기는 현상 방지
        Collider2D arrowCol = arrow.GetComponent<Collider2D>();
        Collider2D[] playerCols = GetComponentsInChildren<Collider2D>();

        foreach (Collider2D col in playerCols)
        {
            if (arrowCol != null && col != null)
                Physics2D.IgnoreCollision(arrowCol, col, true);
        }

        // 속도 기반 발사(Rigidbody2D 필요)
        Rigidbody2D rb = arrow.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = new Vector2(dir * arrowSpeed, 0f);
        }
    }
}