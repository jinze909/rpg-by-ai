using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMana : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private PlayerStats stats;

    public void UseMana(float amount)
    {
        TryUseMana(amount);
    }
    public bool TryUseMana(float amount)
    {
        if (stats == null || amount < 0f || stats.Mana < amount) return false;
        stats.Mana = Mathf.Max(stats.Mana - amount, 0f);
        return true;
    }
}
