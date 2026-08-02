using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;

public class GoblinController2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 2f;        // �⺻ �̵� �ӵ�
    [SerializeField] private float speedVariance = 0.4f;  // ���� ��ȯ ������ �ӵ� ���� ����
    [SerializeField] private float patrolMinTime = 1.2f;  // ���� �ּ� �ð�
    [SerializeField] private float patrolMaxTime = 3.1f;  // ���� �ִ� �ð�

    [Header("Refs")]
    [SerializeField] private AnimationManager animationManager; // �̵�/��� �ִϸ��̼� ����
    [SerializeField] private Rigidbody2D rb;                    // ���� �̵� ó��
    [SerializeField] private float hitStunDuration = 0.15f;

    [Header("Knockback")]
    [SerializeField] private float knockbackSpeed = 3f;
    [SerializeField] private float knockbackDuration = 0.25f;

    [Header("Attack")]
    [SerializeField] private int contactDamage = 10;
    [SerializeField] private float knockbackForceX = 11f;
    [SerializeField] private float knockbackForceY = 4.5f;

    private Transform tLeft;   // ���� ���� ���־�
    private Transform tRight;  // ������ ���� ���־�

    private float patrolTimer;        // ���� ���� ���� �ð� ����
    private float currentPatrolTime;  // �̹� ���� ���� ���� �ð�
    private int moveDir = 1;          // �̵� ����(+1 ������ / -1 ����)
    private float currentMoveSpeed;   // ���� ���� ���� �̵� �ӵ�
    private bool isHitStun;

    private bool isKnockback;
    private float knockbackDir;
    /// <summary>
    /// ���� ĳ�� �� �ʱ� ������ ����.
    /// - Rigidbody2D / AnimationManager �ڵ� ����
    /// - Left/Right ���� ������Ʈ ĳ��
    /// - ���� ���� �ð�/�̵� �ӵ��� �������� ����
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
    /// ���� Ÿ�̸� ����.
    /// - currentPatrolTime�� ������ ���� ����
    /// - ���� �� ���� ���� �ð�/�̵� �ӵ��� �ٽ� ���� ����
    /// - �ִϸ��̼� ���µ� �Բ� ����
    /// </summary>
    private void Update()
    {
        patrolTimer += Time.deltaTime;

        if (patrolTimer >= currentPatrolTime)
        {
            patrolTimer = 0f;
            moveDir *= -1;

            // ������ �ٲ� ������ �ӵ�/���� �ð��� ��ȭ�� ��
            currentMoveSpeed = moveSpeed + Random.Range(-speedVariance, speedVariance);
            currentPatrolTime = Random.Range(patrolMinTime, patrolMaxTime);

            ApplyDirectionVisual();
        }

        UpdateAnimation();
    }

    /// <summary>
    /// ���� �̵� ó��.
    /// - ���� ����(moveDir)�� ���� �ӵ�(currentMoveSpeed)�� ���� �̵�
    /// </summary>
    private void FixedUpdate()
    {
        if (isKnockback)
        {
            rb.linearVelocity = new Vector2(knockbackDir * knockbackSpeed, rb.linearVelocity.y);
            return;
        }

        if (isHitStun)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }
        rb.linearVelocity = new Vector2(moveDir * currentMoveSpeed, rb.linearVelocity.y);
    }

    /// <summary>
    /// ��/�� ���� ���־� ��ȯ.
    /// - moveDir ���� ���� Left/Right ������Ʈ Ȱ��ȭ
    /// </summary>
    private void ApplyDirectionVisual()
    {
        if (tLeft != null)
            tLeft.gameObject.SetActive(moveDir < 0);

        if (tRight != null)
            tRight.gameObject.SetActive(moveDir > 0);
    }

    /// <summary>
    /// �̵� ���� ��� �ִϸ��̼� ����.
    /// - �̵� ���̸� Run
    /// - ���� ���¸� Idle
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
    /// �÷��̾�� �浹 �� �˹� ����.
    /// - Player �±׸� ó��
    /// - �浹 ��ġ�� �������� �˹� ������ ����
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Player"))
            return;

        PlayerHealth2D playerHealth = collision.collider.GetComponentInParent<PlayerHealth2D>();

        if (playerHealth == null)
        {
            Debug.LogWarning("PlayerHealth2D �� ã��");
            return;
        }

        float dir = collision.transform.position.x > transform.position.x ? 1f : -1f;
        Vector2 knockbackForce = new Vector2(dir* knockbackForceX, knockbackForceY);
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