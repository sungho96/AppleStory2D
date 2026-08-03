using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowProjectile2D : MonoBehaviour
{
    [SerializeField] private int damage = 10;     // ?”ì‚´??ê°€?˜ëŠ” ?°ë?ì§€ ê°?
    [SerializeField] private float lifeTime = 3f; // ?¼ì • ?œê°„ ???ë™ ?œê±° (ë©”ëª¨ë¦?ê´€ë¦¬ìš©)

    private float moveDir; // ë°œì‚¬ ë°©í–¥ (1: ?¤ë¥¸ìª?/ -1: ?¼ìª½)
    private bool applyHitReaction = true;

    /// <summary>
    /// ì´ˆê¸° ?¤í–‰.
    /// - ?”ì‚´???¼ì • ?œê°„ ???ë™?¼ë¡œ ?? œ?˜ë„ë¡??¤ì •
    /// - ?¬ì— ?¨ì•„?ˆëŠ” ë°œì‚¬ì²??„ì  ë°©ì? (?±ëŠ¥ ê´€ë¦?
    /// </summary>
    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// ì¶©ëŒ ì²˜ë¦¬ (Trigger ê¸°ë°˜).
    /// - ì¶©ëŒ???€?ì—??GoblinHealth2Dë¥?ì°¾ì•„ ?°ë?ì§€ ?„ë‹¬
    /// - ë¶€ëª¨ê¹Œì§€ ?ìƒ‰?˜ì—¬ ì½œë¼?´ë” êµ¬ì¡°??? ì—°?˜ê²Œ ?€??
    /// - ?ê³¼ ì¶©ëŒ ??ì¦‰ì‹œ ?”ì‚´ ?œê±°
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // ë¶€ëª¨ê¹Œì§€ ?¬í•¨?˜ì—¬ ??ì²´ë ¥ ì»´í¬?ŒíŠ¸ ?ìƒ‰
        GoblinHealth2D enemyHealth = other.GetComponentInParent<GoblinHealth2D>();

        if (enemyHealth != null)
        {

            // ?°ë?ì§€ + ë°©í–¥ ?„ë‹¬ (?‰ë°± ?±ì—???¬ìš© ê°€??
            enemyHealth.TakeDamage(damage, moveDir, applyHitReaction);

            // ??ëª…ì¤‘ ???”ì‚´ ?œê±°
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ë°œì‚¬ ë°©í–¥ ?¤ì •.
    /// - PlayerAttack2D?ì„œ ?”ì‚´ ?ì„± ì§í›„ ?¸ì¶œ??
    /// - ë°©í–¥ ê°’ì? ?‰ë°± ë°©í–¥, ?ˆíŠ¸ ?¨ê³¼ ?±ì— ?œìš©??
    /// </summary>
    public void SetDirection(float dir)
    {
        moveDir = dir;
    }

    public void Configure(int configuredDamage, float dir, bool useHitReaction)
    {
        // [?Œì›Œ ??ì¶”ê?] ?”ì‚´ ?¸ìŠ¤?´ìŠ¤ë³??¼í•´?€ ?¼ê²© ë°˜ì‘???¤ì •?©ë‹ˆ??
        damage = Mathf.Max(1, configuredDamage);
        moveDir = Mathf.Sign(dir);
        applyHitReaction = useHitReaction;
    }
}
