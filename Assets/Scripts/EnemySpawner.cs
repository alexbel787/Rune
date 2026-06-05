using UnityEngine;
using UnityEngine.AI;
using Breachwardens.Siege;
using Breachwardens.Player;

namespace Breachwardens.AI
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Limits")]
        [SerializeField] private int maxAlive = 100;
        [SerializeField] private float spawnRadius = 16f;

        private int aliveCount;
        private WallSystem wallSystem;
        private Transform playerTarget;
        private int enemyNum;

        public int AliveCount => aliveCount;
        public int MaxAlive => maxAlive;

        private void Awake()
        {
            if (wallSystem == null)
                wallSystem = WallSystem.Instance;

            var player = FindFirstObjectByType<PlayerController>();
            if (player != null) playerTarget = player.transform;
        }

        public void Configure(WallSystem system, int max, float radius)
        {
            wallSystem = system;
            maxAlive = Mathf.Max(1, max);
            spawnRadius = Mathf.Max(1f, radius);
        }

        public bool TrySpawn(GameObject prefab, EnemySpawnStats stats)
        {
            if (prefab == null) return false;
            if (aliveCount >= maxAlive) return false;

            if (!TryGetSpawnPosition(out Vector3 position))
                return false;

            GameObject enemy = EnemyPool.Instance.Get(prefab, position, Quaternion.identity);

            enemy.name = $"Enemy{enemyNum++} " + prefab.name;
            enemy.transform.SetParent(transform);
            aliveCount++;

            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.speed = stats.moveSpeed;
                agent.stoppingDistance = stats.attackRange * 0.7f;

                if (NavMesh.SamplePosition(enemy.transform.position, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                {
                    enemy.transform.position = navHit.position;
                    agent.enabled = true;
                }
            }

            var ai = enemy.GetComponent<EnemyAgent>();
            if (ai != null)
                ai.Configure(wallSystem, playerTarget, stats.attackDamage, stats.attackInterval, stats.attackRange);

            var health = enemy.GetComponent<EnemyHealth>();
            if (health != null)
            {
                float colH = 2f;
                var col = enemy.GetComponent<CapsuleCollider>();
                if (col != null) colH = col.height;
                health.Initialize(stats.health, colH + 0.1f);
            }

            return true;
        }

        private bool TryGetSpawnPosition(out Vector3 position)
        {
            Vector3 center = wallSystem != null ? wallSystem.Center : Vector3.zero;
            const int attempts = 12;

            for (int i = 0; i < attempts; i++)
            {
                float angle = Random.Range(-Mathf.PI * 0.5f, Mathf.PI * 0.5f);
                float radius = Random.Range(spawnRadius * 0.9f, spawnRadius * 1.1f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Vector3 candidate = center + offset;

                // "outside wall front only" 
                if (candidate.z < center.z + 2f)
                    continue;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                {
                    position = hit.position;
                    return true;
                }
            }

            position = Vector3.zero;
            Debug.LogWarning("EnemySpawner: Failed to find spawn position!");
            return false;
        }

        public void NotifyEnemyDespawned()
        {
            aliveCount = Mathf.Max(0, aliveCount - 1);
        }
    }
}