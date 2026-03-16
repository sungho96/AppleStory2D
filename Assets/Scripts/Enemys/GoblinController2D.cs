using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;


public class GoblinController2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float patrolTime = 2f;

    [Header("Refs")]
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private Rigidbody2D rb;

    private Transform tLeft;
    private Transform tRight;

    private float patrolTimer;
    private int moveDir = 1;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (animationManager == null)
            animationManager = GetComponent<AnimationManager>();

        tLeft = transform.Find("Left");
        tRight = transform.Find("Right");

        ApplyDirectionVisual();
    }

    private void Update()
    {
        patrolTimer += Time.deltaTime;

        if (patrolTimer >= patrolTime)
        {
            patrolTimer = 0f;
            moveDir *= -1;
            ApplyDirectionVisual();
        }
        UpdateAnimation();
    }
    private void FixedUpdate()
    {
        rb.velocity = new Vector2(moveDir * moveSpeed, rb.velocity.y);
    }

    private void ApplyDirectionVisual()
    {
        if (tLeft != null)
            tLeft.gameObject.SetActive(moveDir < 0);
        
        if (tRight != null)
            tRight.gameObject.SetActive(moveDir > 0);
    }
    private void UpdateAnimation()
    {
        if (animationManager == null)
            return;

        if (Mathf.Abs(moveDir) > 0.01f)
            animationManager.SetState(CharacterState.Run);
        else
            animationManager.SetState(CharacterState.Idle);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Player"))   
            return;

        PlayerController2D player = collision.collider.GetComponent<PlayerController2D>();
        if (player == null) return;

        float dir = collision.transform.position.x > transform.position.x ? 1f : -1f;
        Vector2 knockbackForce = new Vector2(dir*11f, 4.5f);

        player.ApplyKnockback(knockbackForce);
    }

}
