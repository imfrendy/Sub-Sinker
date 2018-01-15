﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class PlayerHealth : NetworkBehaviour
{
    public const float maxHealth = 100;
    [SyncVar(hook = "OnChangeHealth")]
    public float currentHealth = maxHealth;
    public RectTransform healthBar;
    float barWidth;

    private void Start()
    {
        barWidth = healthBar.sizeDelta.x;
    }

    public void TakeDamage(float amount)
    {
        if (!isServer)
        {
            return;
        }
        print("oof");
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Debug.Log("Dead!");

        }
    }

    void OnChangeHealth(float health)
    {
        healthBar.sizeDelta = new Vector2((health / maxHealth) * barWidth, healthBar.sizeDelta.y);
    }
}