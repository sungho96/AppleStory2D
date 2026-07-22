using System;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Level")]
    [SerializeField] private int level = 1;

    [Header("HP")]
    [SerializeField] private int maxHp = 100;
    [SerializeField] private int hp = 100;

    [Header("MP")]
    [SerializeField] private int maxMp = 100;
    [SerializeField] private int mp = 100;

    [Header("EXP")]
    [SerializeField] private int exp = 0;
    [SerializeField] private int needExp = 10;

    /// <summary>
    /// 스탯이 바뀔 때마다 호출되는 이벤트입니다.
    /// - HUD는 이 이벤트를 구독해서 UI를 갱신합니다.
    /// </summary>
    public event Action OnStatChanged;

    // ===== 읽기 전용 프로퍼티 =====
    public int Level => level;

    public int HP => hp;
    public int MaxHP => maxHp;

    public int MP => mp;
    public int MaxMP => maxMp;

    public int EXP => exp;
    public int NeedEXP => needExp;

    /// <summary>
    /// HP를 감소시킵니다.
    /// - 0 아래로 내려가지 않도록 Clamp 합니다.
    /// </summary>
    public void Damage(int amount)
    {
        if (amount <= 0) return;
        hp = Mathf.Max(0, hp - amount);
        OnStatChanged?.Invoke();
    }

    /// <summary>
    /// HP를 회복합니다.
    /// - MaxHP를 넘지 않도록 Clamp 합니다.
    /// </summary>
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        hp = Mathf.Min(maxHp, hp + amount);
        OnStatChanged?.Invoke();
    }

    /// <summary>
    /// MP를 사용합니다.
    /// - MP가 부족하면 false를 반환합니다.
    /// </summary>
    public bool UseMP(int amount)
    {
        if (amount <= 0) return true;
        if (mp < amount) return false;

        mp -= amount;
        OnStatChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// MP를 회복합니다.
    /// - MaxMP를 넘지 않도록 Clamp 합니다.
    /// </summary>
    public void RecoverMP(int amount)
    {
        if (amount <= 0) return;
        mp = Mathf.Min(maxMp, mp + amount);
        OnStatChanged?.Invoke();
    }

    /// <summary>
    /// EXP를 획득합니다.
    /// - NeedEXP를 넘으면 레벨업을 반복 처리합니다.
    /// </summary>
    public void AddEXP(int amount)
    {
        if (amount <= 0) return;

        exp += amount;
        while (exp >= needExp)
        {
            exp -= needExp;
            LevelUp();
        }

        OnStatChanged?.Invoke();
    }

    /// <summary>
    /// 레벨업 처리입니다.
    /// - 정석적으로 최대치 상승 + 회복 + 요구 경험치 증가 등을 수행합니다.
    /// </summary>
    private void LevelUp()
    {
        level++;

        // 예시 규칙(원하시면 수치 규칙만 바꾸면 됩니다)
        maxHp += 10;
        maxMp += 5;

        // 레벨업 시 풀회복(메이플 느낌)
        hp = maxHp;
        mp = maxMp;

        // 필요 경험치 증가(간단 버전)
        needExp = Mathf.RoundToInt(needExp * 1.2f) + 1;
    }
}