using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 들개: 사람을 경계해 도망친다. 잡으면 생고기를 얻는다.
[RequireComponent(typeof(NavMeshAgent))]
public class Animal : MonoBehaviour, IDamageable
{
    public float maxHealth = 30f;
    public float walkSpeed = 1.2f;
    public float fleeSpeed = 6f;
    public float fleeDistance = 9f;
    public Transform[] legs;

    NavMeshAgent agent;
    AudioSource voice;
    float health, wanderTimer, fleeTimer, animPhase;
    bool dead;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = maxHealth;
        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 1;
        voice.maxDistance = 40;
        voice.rolloffMode = AudioRolloffMode.Linear;
    }

    void Update()
    {
        if (dead) return;
        float dt = Time.deltaTime;
        var player = PlayerController.Instance;
        fleeTimer -= dt;
        wanderTimer -= dt;

        if (player != null)
        {
            float range = player.IsCrouching ? fleeDistance * 0.45f : fleeDistance;
            var away = transform.position - player.transform.position;
            if (away.magnitude < range && fleeTimer <= 0)
            {
                if (agent.speed < fleeSpeed) Bark();
                agent.speed = fleeSpeed;
                fleeTimer = 1f;
                away.y = 0;
                var target = transform.position + away.normalized * 12f + Random.insideUnitSphere * 3f;
                if (NavMesh.SamplePosition(target, out var hit, 5f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
            }
        }

        if (fleeTimer < -3f && wanderTimer <= 0)
        {
            agent.speed = walkSpeed;
            wanderTimer = Random.Range(4f, 9f);
            var target = transform.position + Random.insideUnitSphere * 8f;
            if (NavMesh.SamplePosition(target, out var hit, 4f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }

        float speed = agent.velocity.magnitude;
        animPhase += dt * (2 + speed * 3.5f);
        float amp = Mathf.Clamp01(speed / 2f) * 35f;
        for (int i = 0; i < legs.Length; i++)
            if (legs[i] != null)
                legs[i].localRotation = Quaternion.Euler(Mathf.Sin(animPhase + (i % 2 == 0 ? 0 : Mathf.PI) + (i >= 2 ? Mathf.PI * 0.5f : 0)) * amp, 0, 0);
    }

    void Bark()
    {
        voice.pitch = Random.Range(0.9f, 1.15f);
        voice.PlayOneShot(ProceduralAudio.Bark(), 0.9f);
    }

    public void TakeDamage(float amount, Vector3 direction)
    {
        if (dead) return;
        health -= amount;
        Bark();
        fleeTimer = 0;
        if (health <= 0) StartCoroutine(Die());
    }

    IEnumerator Die()
    {
        dead = true;
        agent.enabled = false;
        var start = transform.rotation;
        var end = start * Quaternion.Euler(0, 0, 85);
        for (float t = 0; t < 1; t += Time.deltaTime / 0.4f)
        {
            transform.rotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }
        PickupItem.Spawn("raw_meat", 2, transform.position + Vector3.up * 0.5f);
        Destroy(gameObject, 30f);
    }
}
