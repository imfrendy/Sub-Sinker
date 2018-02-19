﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class EngineLight : NetworkBehaviour {
    public Light engineLight;

    public const float startRad = 10;

    [SyncVar(hook = "OnLightRadiusChange")]
    public float currentRad;
    private float newRad;
    private float scrollSpeed;

    public float maxRad = 25;
    public float minRad = 2;
    private float startIntensity;

    public RectTransform circle;
    public GameObject model;
    Text debugText;

    GameObject localPlayer;
    PlayerHealth health;
    EngineLight lt;

    public bool controllerEnabled;

    public GameObject nametag;

    public override void OnStartClient()
    {
        AdjustEngineLight(currentRad);
    }

    void Start () {
        localPlayer = GameObject.Find("LocalPlayer");
        scrollSpeed = 10;
        startIntensity = engineLight.intensity;
        health = GetComponent<PlayerHealth>();
        Spawn();
    }

    void Update() {
        if (!health.alive)
        {
            // vvvv   maybe use this instead   vvvv (but does it matter)
            //engineLight.intensity = 0;
            if (currentRad != 0 && isLocalPlayer)
            {
                AdjustEngineLight(0);
                CmdChangeRadius(0); // light off
            }
            return;
        }

        // i eyeballed this value....
        if (Vector3.Distance(transform.position, localPlayer.transform.position) > currentRad * currentRad * 0.08f)
        {
            // hide light/nametag when not within distance of localplayer
            engineLight.intensity = 0;
            nametag.SetActive(false);
        }
        else
        {
            engineLight.intensity = startIntensity;
            nametag.SetActive(true);
        }

        // network awareness
        if (!isLocalPlayer)
            return;

        if (Input.GetKeyDown(KeyCode.F3))
        {
            controllerEnabled = !controllerEnabled;
        }

        if (controllerEnabled)
        {
            newRad += (Input.GetAxisRaw("C EngineUp") - Input.GetAxisRaw("C EngineDown")) * 0.2f; // slow it
            if (newRad == currentRad)
            {
                newRad = Mathf.Round(currentRad);
            }
        }
        else
        {
            if (Input.GetAxis("EngineLight") < 1 && newRad >= minRad)
            {
                newRad -= Input.GetAxis("EngineLight") * scrollSpeed;
            }
            else if (Input.GetAxis("EngineLight") > 1 && newRad <= maxRad)
            {
                newRad += Input.GetAxis("EngineLight") * scrollSpeed;
            }
        }

        if (GameManager.instance.playerSettings.ScrollInvert)
        {
            // do the math. it inverts it
            newRad = 2 * currentRad - newRad;
        }

        if (newRad < minRad) {
            newRad = minRad;
        }
        else if (newRad > maxRad) {
            newRad = maxRad;
        }

        if (newRad != currentRad) {
            // change the light instantaneously, so you dont have to wait for the server
            AdjustEngineLight(newRad);
            CmdChangeRadius(newRad);
        }
    }

    [Command]
    public void CmdChangeRadius(float radius)
    {
        currentRad = radius;
    }

    void OnLightRadiusChange(float radius)
    {
        currentRad = radius;
        if (!isLocalPlayer)
        {
            AdjustEngineLight(radius);
        }
    }

    public float GetLightMultiplier()
    {
        return currentRad / maxRad;
    }

    void AdjustEngineLight(float r)
    {
        engineLight.range = r;
        engineLight.spotAngle = r * 7;
        circle.sizeDelta = new Vector2(r * r * 2.8f, r * r * 2.8f);
    }

    // run on client
    public void Spawn()
    {
        newRad = startRad;
        if (isLocalPlayer)
        {
            CmdChangeRadius(newRad);
            AdjustEngineLight(newRad);
        }
    }
}
