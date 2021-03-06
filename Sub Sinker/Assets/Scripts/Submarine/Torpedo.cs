﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Torpedo : NetworkBehaviour
{
    //public GameObject PingLight;
    //public float lightZOffset = -3f;
    //public float firstLightIntensity = 6f;

    [SyncVar]
    public NetworkInstanceId spawnedBy;

    public Vector3 srcPos;

    public float explosionForce = 20f;
    public float explosionRadius = 20f;

    public float maxDist = 300f;

    public float maxHitDmg = 35f;
    public float splashDmgMax = 30f;

    GameObject source;

    public GameObject bubblesPrefab;
    GameObject bubbles;
    public GameObject explPrefab;

    // ignore collisions on the server
    public override void OnStartClient()
    {
        source = ClientScene.FindLocalObject(spawnedBy);
        Physics2D.IgnoreCollision(GetComponent<Collider2D>(), source.GetComponents<Collider2D>()[0]);
        Physics2D.IgnoreCollision(GetComponent<Collider2D>(), source.GetComponents<Collider2D>()[1]);
    }

    private void Start()
    {
        bubbles = Instantiate(bubblesPrefab, transform.position + transform.up * 0.9f, transform.rotation * Quaternion.Euler(Vector3.right * -90));
        bubbles.GetComponent<TorpedoTrail>().source = this.gameObject;
    }

    void OnCollisionEnter2D(Collision2D coll)
    {
        // deal damage
        // spawn explosion light + particles
        gameObject.GetComponent<MeshRenderer>().enabled = false;

        GameObject hit = coll.gameObject;

        // direct hit
        if (hit.tag == "Player")
        {
            var health = hit.GetComponent<PlayerHealth>();
            if (health != null)
            {
                print("dmg: " + Mathf.Lerp(maxHitDmg, 0, Vector3.Distance(transform.position, srcPos) / maxDist) + ", dist to src: " + Vector3.Distance(transform.position, srcPos));
                health.CmdTakeDamage(Mathf.Lerp(maxHitDmg, 0, Vector3.Distance(transform.position, srcPos) / maxDist), 
                    source.GetComponent<PlayerInfo>().playerName, source.GetComponent<PlayerInfo>().primaryColor); 
                
            }
        }

        // splash damage
        GameObject[] players;

        players = GameObject.FindGameObjectsWithTag("Player");

        // a_player meaning generic player, not the current player
        foreach (GameObject a_player in players)
        {
            // do not deal damage to direct hit
            if (a_player.GetComponent<Rigidbody2D>() != null)
            {
                // no splash + direct hit compounding
                if (a_player != hit.gameObject)
                {
                    var health = a_player.GetComponent<PlayerHealth>();
                    if (health != null)
                    {
                        float damage = Mathf.Lerp(Mathf.SmoothStep(0, splashDmgMax, (explosionRadius - Vector3.Distance(transform.position, a_player.transform.position)) / explosionRadius), 0, Vector3.Distance(transform.position, srcPos) / maxDist);
                        health.CmdTakeDamage(damage, source.GetComponent<PlayerInfo>().playerName, source.GetComponent<PlayerInfo>().primaryColor);
                    }
                }

                // add explosion force to player hit
                ExplosionForce expl = a_player.GetComponent<ExplosionForce>();
                if (isServer)
                    expl.RpcAddExplosionForce(explosionForce, transform.position, explosionRadius);
            }
        }

        SpawnExplosion();
        NetworkServer.Destroy(this.gameObject);
    }

    void SpawnExplosion()
    {
        var b = Instantiate(explPrefab, transform.position, transform.rotation * Quaternion.Euler(Vector3.right * -90));
        NetworkServer.Spawn(b);
    }
}