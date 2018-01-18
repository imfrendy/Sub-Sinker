﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class PlayerMovement : NetworkBehaviour {

    public float maxSpeed;
    private Rigidbody2D rb;
    private float prevXVel;
    private Vector2 prevVel;
    public float wallPushbackForce;
    public RectTransform indicator;

    public Canvas[] canvases;
    
    PlayerHealth health;
    EngineLight lt;

    public GameObject model;
    public Canvas mobileCanvas;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        prevXVel = rb.velocity[0];
        health = GetComponent<PlayerHealth>();
        lt = GetComponent<EngineLight>();
    }

    public override void OnStartLocalPlayer() // local player only
    {
        gameObject.name = "LocalPlayer";

        // set ui active for local player only
        foreach (Canvas ui in canvases)
        {
            ui.gameObject.SetActive(true);
        }
    }

    void FixedUpdate ()
    {
        // network awareness
        if (!isLocalPlayer)
        {
            return;
        }
        if (!health.alive)
        {
            return;
        }

        float moveHorizontal = Input.GetAxis ("Horizontal");
        float moveVertical = Input.GetAxis ("Vertical");

        Vector2 movement = new Vector2 (moveHorizontal, moveVertical);

        CmdAddForce(movement.normalized * maxSpeed * lt.GetLightMultiplier());

        // todo: send to ui
        indicator.anchoredPosition = transform.position / 2;
    }

    // visual effects
    private void Update()
    {
        if (!health.alive)
        {
            return;
        }
        // NOT local only
        BroadcastMessage("AdjustVel", Mathf.Abs(rb.velocity[0]));

        if (prevXVel <= 0 && rb.velocity[0] > 0)
        {
            // check if sub is already in this dir
            BroadcastMessage("Flip", "right");
        }
        else if (prevXVel >= 0 && rb.velocity[0] < 0)
        {
            BroadcastMessage("Flip", "left");
        }

        prevXVel = rb.velocity[0];
        prevVel = rb.velocity;
    }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Map"))
        {
            // damage proportional to vel
            gameObject.GetComponent<PlayerHealth>().CmdTakeDamage(prevVel.magnitude * 2);
            CmdAddForce(-1 * prevVel * wallPushbackForce);
        }
    }

    public void FlipCollider(string dir)
    {
        if (dir == "left")
        {
            gameObject.GetComponent<BoxCollider2D>().offset = new Vector2(-0.14f, 0.62f);
        }
        else if (dir == "right")
        {
            gameObject.GetComponent<BoxCollider2D>().offset = new Vector2(0.155f, 0.62f);
        }
    }

    // server does physics 
    //[Command]
    public void CmdAddForce(Vector2 force)
    {
        rb.AddForce(force);
    }
}
