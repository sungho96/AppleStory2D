using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth2D : MonoBehaviour
{
    /// <summary>
    /// 플레이어 체력 관리 전용 처리.
    /// - HP 보관
    /// - 데미지 진입
    /// - 무적 상태 체크
    /// - 사망 처리
    /// - 피격 반응 스크립트 호출
    /// </summary>
    [Header("HP")]
    [SerializeField] private int maxHp = 100;
    [SerializeField] private int currentHp;

    [Header("Refs")]
    [SerializeField] private PlayerHitReaction2D hitReaction;
    [SerializeField] private PlayerHpBarUI hpBarUI;
    [SerializeField] private PlayerStats playerStats;

    private bool isDead;

    /// <summary>
    /// 현재 HP 반환.
    /// </summary>
    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public float NormalizedHp => maxHp > 0 ? (float)currentHp / maxHp : 0f;

    /// <summary>
    /// 사망 상태 여부.
    /// </summary>
    public bool IsDead => isDead;

    private void Awake()
    {
        if (hitReaction == null)
            hitReaction = GetComponent<PlayerHitReaction2D>();
        if (hpBarUI == null)
            hpBarUI = Object.FindFirstObjectByType<PlayerHpBarUI>();
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();

    }
    void Start()
    {
        currentHp = maxHp;

        if (hpBarUI != null)
            hpBarUI.Refresh();
    }

    /// <summary>
    /// 데미지 적용 요청.
    /// - 이미 사망 상태면 무시
    /// - 피격 무적 상태면 무시
    /// - HP 감소 후 살아있으면 피격 반응 호출
    /// </summary>
    public void TakeDamage(int damage, Vector2 knockbackForce)
    {
        Debug.Log($"TakeDamage 진입 / damage={damage}, force={knockbackForce}");

        if (isDead)
        {
            Debug.Log("이미 죽은 상태라 return");
            return;
        }

        if (hitReaction != null && hitReaction.IsHitCooldown)
        {
            Debug.Log("현재 피격 무적 상태라 return");
            return;
        }

        currentHp -= damage;

        if (currentHp < 0)
            currentHp = 0;

        if (hpBarUI != null)
            hpBarUI.Refresh();

        if (currentHp <= 0)
        {
            Debug.Log("HP 0 이하 -> Die 호출");
            Die();
            return;
        }
        playerStats.Damage(damage);

        if (hitReaction == null)
        {
            Debug.LogWarning("hitReaction 이 null 이라 넉백 호출 불가");
            return;
        }
        Debug.Log($"HP after damage = {currentHp}/{maxHp}");
        hitReaction.ApplyKnockback(knockbackForce);
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("플레이어 사망");
    }

    private int CalculateFinalDamage(int rawDamage)
    {
        // 나중에 방어력/감소율 넣을 자리
        // 예: rawDamage - defense, 최소 1 보장 등
        int finalDamage = rawDamage;

        // 최소 1 데미지 보장(메이플식 느낌)
        finalDamage = Mathf.Max(1, finalDamage);

        return finalDamage;
    }

}
