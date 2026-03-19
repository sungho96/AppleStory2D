using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;

public class GoblinController2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 2f;        // 기본 이동 속도
    [SerializeField] private float speedVariance = 0.4f;  // 방향 전환 때마다 속도 랜덤 편차
    [SerializeField] private float patrolMinTime = 1.2f;  // 순찰 최소 시간
    [SerializeField] private float patrolMaxTime = 3.1f;  // 순찰 최대 시간

    [Header("Refs")]
    [SerializeField] private AnimationManager animationManager; // 이동/대기 애니메이션 제어
    [SerializeField] private Rigidbody2D rb;                    // 물리 이동 처리
    [SerializeField] private float hitStunDuration = 0.15f;

    [Header("Knockback")]
    [SerializeField] private float knockbackSpeed = 3f;
    [SerializeField] private float knockbackDuration = 0.25f;

    [Header("Attack")]
    [SerializeField] private int contactDamage = 10;
    [SerializeField] private float knockbackForceX = 11f;
    [SerializeField] private float knockbackForceY = 4.5f;

    private Transform tLeft;   // 왼쪽 방향 비주얼
    private Transform tRight;  // 오른쪽 방향 비주얼

    private float patrolTimer;        // 현재 방향 유지 시간 누적
    private float currentPatrolTime;  // 이번 순찰 구간 지속 시간
    private int moveDir = 1;          // 이동 방향(+1 오른쪽 / -1 왼쪽)
    private float currentMoveSpeed;   // 현재 구간 실제 이동 속도
    private bool isHitStun;

    private bool isKnockback;
    private float knockbackDir;
    /// <summary>
    /// 참조 캐싱 및 초기 순찰값 설정.
    /// - Rigidbody2D / AnimationManager 자동 연결
    /// - Left/Right 방향 오브젝트 캐싱
    /// - 시작 순찰 시간/이동 속도를 랜덤으로 설정
    /// </summary>
    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (animationManager == null)
            animationManager = GetComponent<AnimationManager>();

        tLeft = transform.Find("Left");
        tRight = transform.Find("Right");

        currentPatrolTime = Random.Range(patrolMinTime, patrolMaxTime);
        currentMoveSpeed = moveSpeed + Random.Range(-speedVariance, speedVariance);

        ApplyDirectionVisual();
    }

    /// <summary>
    /// 순찰 타이머 갱신.
    /// - currentPatrolTime이 지나면 방향 반전
    /// - 반전 시 다음 순찰 시간/이동 속도를 다시 랜덤 설정
    /// - 애니메이션 상태도 함께 갱신
    /// </summary>
    private void Update()
    {
        patrolTimer += Time.deltaTime;

        if (patrolTimer >= currentPatrolTime)
        {
            patrolTimer = 0f;
            moveDir *= -1;

            // 방향을 바꿀 때마다 속도/지속 시간에 변화를 줌
            currentMoveSpeed = moveSpeed + Random.Range(-speedVariance, speedVariance);
            currentPatrolTime = Random.Range(patrolMinTime, patrolMaxTime);

            ApplyDirectionVisual();
        }

        UpdateAnimation();
    }

    /// <summary>
    /// 물리 이동 처리.
    /// - 현재 방향(moveDir)과 현재 속도(currentMoveSpeed)로 수평 이동
    /// </summary>
    private void FixedUpdate()
    {
        if (isKnockback)
        {
            rb.velocity = new Vector2(knockbackDir * knockbackSpeed, rb.velocity.y);
            return;
        }

        if (isHitStun)
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
            return;
        }
        rb.velocity = new Vector2(moveDir * currentMoveSpeed, rb.velocity.y);
    }

    /// <summary>
    /// 좌/우 방향 비주얼 전환.
    /// - moveDir 값에 따라 Left/Right 오브젝트 활성화
    /// </summary>
    private void ApplyDirectionVisual()
    {
        if (tLeft != null)
            tLeft.gameObject.SetActive(moveDir < 0);

        if (tRight != null)
            tRight.gameObject.SetActive(moveDir > 0);
    }

    /// <summary>
    /// 이동 상태 기반 애니메이션 갱신.
    /// - 이동 중이면 Run
    /// - 정지 상태면 Idle
    /// </summary>
    private void UpdateAnimation()
    {
        if (animationManager == null)
            return;

        if (Mathf.Abs(moveDir) > 0.01f)
            animationManager.SetState(CharacterState.Run);
        else
            animationManager.SetState(CharacterState.Idle);
    }

    /// <summary>
    /// 플레이어와 충돌 시 넉백 적용.
    /// - Player 태그만 처리
    /// - 충돌 위치를 기준으로 넉백 방향을 결정
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Player"))
            return;

        PlayerHealth2D playerHealth = collision.collider.GetComponentInParent<PlayerHealth2D>();

        if (playerHealth == null)
        {
            Debug.LogWarning("PlayerHealth2D 못 찾음");
            return;
        }

        float dir = collision.transform.position.x > transform.position.x ? 1f : -1f;
        Vector2 knockbackForce = new Vector2(dir* knockbackForceX, knockbackForceY);

        playerHealth.TakeDamage(contactDamage, knockbackForce);
    }

    public void PlayHitStun()
    {
        StartCoroutine(CoHitStun());
    }

    private IEnumerator CoHitStun()
    {
        isHitStun = true;
        yield return new WaitForSeconds(hitStunDuration);
        isHitStun = false;
    }

    public void PlayKnockback(float dir)
    {
        StartCoroutine(CoKnockback(dir));
    }
    private IEnumerator CoKnockback(float dir)
    {
        isKnockback = true;
        knockbackDir = dir;

        yield return new WaitForSeconds(knockbackDuration);

        isKnockback = false;
    }
}