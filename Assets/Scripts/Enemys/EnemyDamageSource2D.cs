using UnityEngine;

public class EnemyDamageSource2D : MonoBehaviour
{
    [SerializeField] private int contactDamage = 10;
    public int ContactDamage => contactDamage;
}