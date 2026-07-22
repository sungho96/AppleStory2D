using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHpBarUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Slider hpSlider;              // HUD의 HP Slider만 드래그로 연결
    [SerializeField] private PlayerHealth2D playerHealth;  // Player의 Health만 드래그로 연결

    private void Start()
    {
        Refresh(); // 초기 1회 반영
    }

    public void Refresh()
    {
        if (hpSlider == null || playerHealth == null)
            return;

        hpSlider.value = playerHealth.NormalizedHp;
    }
}

