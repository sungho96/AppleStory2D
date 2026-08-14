using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class PlayerHealth2D : MonoBehaviour
{
    /// <summary>
    /// �÷��̾� ü�� ���� ���� ó��.
    /// - HP ����
    /// - ������ ����
    /// - ���� ���� üũ
    /// - ��� ó��
    /// - �ǰ� ���� ��ũ��Ʈ ȣ��
    /// </summary>
    [Header("HP")]
    [SerializeField] private int maxHp = 100;
    [SerializeField] private int currentHp;

    [Header("Refs")]
    [SerializeField] private PlayerHitReaction2D hitReaction;
    [SerializeField] private PlayerHpBarUI hpBarUI;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private WarriorShieldBlock2D warriorShieldBlock;

    [Header("Death Effect")]
    [SerializeField] private float deathFadeDelay = 1.0f;
    [SerializeField] private float deathFadeDuration = 0.8f;
    [SerializeField] private float deathFreezeCrossFadeBuffer = 0.08f;

    private bool isDead;

    /// <summary>
    /// ���� HP ��ȯ.
    /// </summary>
    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public float NormalizedHp => maxHp > 0 ? (float)currentHp / maxHp : 0f;

    /// <summary>
    /// ��� ���� ����.
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
        if (warriorShieldBlock == null)
            warriorShieldBlock = GetComponent<WarriorShieldBlock2D>();

    }
    void Start()
    {
        currentHp = maxHp;

        if (hpBarUI != null)
            hpBarUI.Refresh();
    }

    /// <summary>
    /// ������ ���� ��û.
    /// - �̹� ��� ���¸� ����
    /// - �ǰ� ���� ���¸� ����
    /// - HP ���� �� ��������� �ǰ� ���� ȣ��
    /// </summary>
    public void TakeDamage(int damage, Vector2 knockbackForce)
    {
        Debug.Log($"TakeDamage ���� / damage={damage}, force={knockbackForce}");

        if (isDead)
        {
            Debug.Log("�̹� ���� ���¶� return");
            return;
        }

        if (hitReaction != null && hitReaction.IsHitCooldown)
        {
            Debug.Log("���� �ǰ� ���� ���¶� return");
            return;
        }

        if (warriorShieldBlock == null)
            warriorShieldBlock = GetComponent<WarriorShieldBlock2D>();

        // [Codex Warrior ShieldBlock] 방패막기 중이면 HP에 적용되는 피해량만 줄입니다.
        int finalDamage = warriorShieldBlock != null
            ? warriorShieldBlock.ReduceDamage(damage)
            : damage;

        currentHp -= finalDamage;

        if (currentHp < 0)
            currentHp = 0;

        if (hpBarUI != null)
            hpBarUI.Refresh();

        if (currentHp <= 0)
        {
            Debug.Log("HP 0 ���� -> Die ȣ��");
            Die();
            return;
        }
        playerStats.Damage(finalDamage);

        if (hitReaction == null)
        {
            Debug.LogWarning("hitReaction �� null �̶� �˹� ȣ�� �Ұ�");
            return;
        }
        Debug.Log($"HP after damage = {currentHp}/{maxHp}");
        hitReaction.ApplyKnockback(knockbackForce);
    }

    private void Die()
    {
        isDead = true;
        PlayDeathAnimation();
        StartCoroutine(CoFadeOutAfterDeath());
        Debug.Log("�÷��̾� ���");
    }

    private void PlayDeathAnimation()
    {
        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.speed = 1f;
            PlayAnimatorState(animator, "Death");
        }

        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null)
                continue;

            TrySetStringMember(behaviours[i], "Expression", "Dead");
            TrySetStringMember(behaviours[i], "Action", "Death");
            TrySetStringMember(behaviours[i], "SoloState", "Death");
            TrySetStringMember(behaviours[i], "State", "Death");
            TrySetEnumMember(behaviours[i], "Action", "Death");
            TrySetEnumMember(behaviours[i], "SoloState", "Death");
            TrySetEnumMember(behaviours[i], "State", "Death");
            TryInvokeStringMethod(behaviours[i], "SetExpression", "Dead");
            TryInvokeStringMethod(behaviours[i], "SetExpressionByName", "Dead");
            TryInvokeStringMethod(behaviours[i], "SetAction", "Death");
            TryInvokeStringMethod(behaviours[i], "SetSoloState", "Death");
            TryInvokeStringMethod(behaviours[i], "SetState", "Death");
            TryInvokeStringMethod(behaviours[i], "SetActionByName", "Death");
            TryInvokeEnumMethod(behaviours[i], "SetAction", "Death");
            TryInvokeEnumMethod(behaviours[i], "SetSoloState", "Death");
            TryInvokeEnumMethod(behaviours[i], "SetState", "Death");
        }
    }

    private void PlayAnimatorState(Animator animator, string stateName)
    {
        int stateHash = Animator.StringToHash(stateName);
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            if (animator.HasState(layer, stateHash))
            {
                animator.CrossFade(stateHash, 0.05f, layer);
                StartCoroutine(CoFreezeDeathAfterOnePlay(animator, layer, stateHash));
                return;
            }
        }
    }

    private IEnumerator CoFreezeDeathAfterOnePlay(Animator animator, int layer, int stateHash)
    {
        // [Codex Death] Death clip plays once, then holds the last pose.
        if (animator == null)
            yield break;

        if (deathFreezeCrossFadeBuffer > 0f)
            yield return new WaitForSeconds(deathFreezeCrossFadeBuffer);

        while (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            if (stateInfo.shortNameHash == stateHash && stateInfo.normalizedTime >= 1f)
            {
                animator.speed = 0f;
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator CoFadeOutAfterDeath()
    {
        // [Codex Death] Keep player object alive, fade only sprites for result/network references.
        if (deathFadeDelay > 0f)
            yield return new WaitForSeconds(deathFadeDelay);

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        Color[] startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                startColors[i] = renderers[i].color;
        }

        float elapsed = 0f;
        while (elapsed < deathFadeDuration)
        {
            float t = deathFadeDuration > 0f ? elapsed / deathFadeDuration : 1f;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color color = startColors[i];
                color.a = Mathf.Lerp(startColors[i].a, 0f, t);
                renderers[i].color = color;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color = renderers[i].color;
            color.a = 0f;
            renderers[i].color = color;
        }
    }
    private void TrySetStringMember(object target, string memberName, string value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        System.Type type = target.GetType();

        FieldInfo field = type.GetField(memberName, flags);
        if (field != null && field.FieldType == typeof(string))
            TrySetValue(() => field.SetValue(target, value));

        PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null && property.CanWrite && property.PropertyType == typeof(string))
            TrySetValue(() => property.SetValue(target, value));
    }

    private void TryInvokeStringMethod(object target, string methodName, string value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo method = target.GetType().GetMethod(methodName, flags, null, new[] { typeof(string) }, null);
        if (method != null)
            TrySetValue(() => method.Invoke(target, new object[] { value }));
    }

    private void TrySetEnumMember(object target, string memberName, string enumName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        System.Type type = target.GetType();

        FieldInfo field = type.GetField(memberName, flags);
        if (field != null && field.FieldType.IsEnum && System.Enum.IsDefined(field.FieldType, enumName))
            TrySetValue(() => field.SetValue(target, System.Enum.Parse(field.FieldType, enumName)));

        PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null && property.CanWrite && property.PropertyType.IsEnum && System.Enum.IsDefined(property.PropertyType, enumName))
            TrySetValue(() => property.SetValue(target, System.Enum.Parse(property.PropertyType, enumName)));
    }

    private void TryInvokeEnumMethod(object target, string methodName, string enumName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo[] methods = target.GetType().GetMethods(flags);
        for (int i = 0; i < methods.Length; i++)
        {
            if (methods[i].Name != methodName)
                continue;

            ParameterInfo[] parameters = methods[i].GetParameters();
            if (parameters.Length != 1 || !parameters[0].ParameterType.IsEnum)
                continue;

            System.Type enumType = parameters[0].ParameterType;
            if (!System.Enum.IsDefined(enumType, enumName))
                continue;

            object enumValue = System.Enum.Parse(enumType, enumName);
            TrySetValue(() => methods[i].Invoke(target, new[] { enumValue }));
        }
    }

    private void TrySetValue(System.Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch
        {
        }
    }

    private int CalculateFinalDamage(int rawDamage)
    {
        // ���߿� ����/������ ���� �ڸ�
        // ��: rawDamage - defense, �ּ� 1 ���� ��
        int finalDamage = rawDamage;

        // �ּ� 1 ������ ����(�����ý� ����)
        finalDamage = Mathf.Max(1, finalDamage);

        return finalDamage;
    }

}
