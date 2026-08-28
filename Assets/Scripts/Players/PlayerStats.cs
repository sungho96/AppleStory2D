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
    /// ������ �ٲ� ������ ȣ��Ǵ� �̺�Ʈ�Դϴ�.
    /// - HUD�� �� �̺�Ʈ�� �����ؼ� UI�� �����մϴ�.
    /// </summary>
    public event Action OnStatChanged;

    // ===== �б� ���� ������Ƽ =====
    public int Level => level;

    public int HP => hp;
    public int MaxHP => maxHp;

    public int MP => mp;
    public int MaxMP => maxMp;

    public int EXP => exp;
    public int NeedEXP => needExp;

    /// <summary>
    /// HP�� ���ҽ�ŵ�ϴ�.
    /// - 0 �Ʒ��� �������� �ʵ��� Clamp �մϴ�.
    /// </summary>
    public void Damage(int amount)
    {
        if (amount <= 0) return;
        hp = Mathf.Max(0, hp - amount);
        OnStatChanged?.Invoke();
    }

    public void SetHpFromNetwork(int currentHp)
    {
        // [Codex Network HP Sync] 서버에서 받은 현재 HP를 로컬 HUD가 보는 PlayerStats에도 그대로 반영합니다.
        hp = Mathf.Clamp(currentHp, 0, maxHp);
        OnStatChanged?.Invoke();
    }

    /// <summary>
    /// HP�� ȸ���մϴ�.
    /// - MaxHP�� ���� �ʵ��� Clamp �մϴ�.
    /// </summary>
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        hp = Mathf.Min(maxHp, hp + amount);
        OnStatChanged?.Invoke();
    }

    /// <summary>
    /// MP�� ����մϴ�.
    /// - MP�� �����ϸ� false�� ��ȯ�մϴ�.
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
    /// MP�� ȸ���մϴ�.
    /// - MaxMP�� ���� �ʵ��� Clamp �մϴ�.
    /// </summary>
    public void RecoverMP(int amount)
    {
        if (amount <= 0) return;
        mp = Mathf.Min(maxMp, mp + amount);
        OnStatChanged?.Invoke();
    }

    /// <summary>
    /// EXP�� ȹ���մϴ�.
    /// - NeedEXP�� ������ �������� �ݺ� ó���մϴ�.
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
    /// ������ ó���Դϴ�.
    /// - ���������� �ִ�ġ ��� + ȸ�� + �䱸 ����ġ ���� ���� �����մϴ�.
    /// </summary>
    private void LevelUp()
    {
        level++;

        // ���� ��Ģ(���Ͻø� ��ġ ��Ģ�� �ٲٸ� �˴ϴ�)
        maxHp += 10;
        maxMp += 5;

        // ������ �� Ǯȸ��(������ ����)
        hp = maxHp;
        mp = maxMp;

        // �ʿ� ����ġ ����(���� ����)
        needExp = Mathf.RoundToInt(needExp * 1.2f) + 1;
    }
}