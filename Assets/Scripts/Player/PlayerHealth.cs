using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Config")]
    [SerializeField] private PlayerStats stats;

    private PlayerAnimations playerAnimations;

    private void Awake()
    {
        playerAnimations = GetComponent<PlayerAnimations>();
    }

    public void TakeDamage(float amount)
    {
        if (stats == null || stats.Health <= 0 || amount <= 0) return;
        stats.Health = Mathf.Max(0f, stats.Health - amount);
        if(stats.Health <= 0f)
        {
            PlayerDead();
        }
    }
    private void PlayerDead()
    {
        if (playerAnimations != null) playerAnimations.SetDeadAnimation();
    }
}
