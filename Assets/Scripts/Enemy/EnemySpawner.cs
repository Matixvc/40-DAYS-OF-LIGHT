using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform playerTransform;

    [Header("Parámetros Iniciales de Spawn")]
    [SerializeField] private float initialSpawnInterval = 2f;  // Intervalo con el que empieza
    [SerializeField] private float minSpawnInterval = 0.3f;     // Límite de velocidad de spawn
    [SerializeField] private float minSpawnDistance = 10f;     // Distancia mínima al jugador
    [SerializeField] private float maxSpawnDistance = 15f;     // Distancia máxima al jugador
    [SerializeField] private int initialMaxEnemies = 20;        // Límite de enemigos inicial
    [SerializeField] private int absoluteMaxEnemies = 100;     // Límite máximo global

    [Header("Progresión y Dificultad")]
    [SerializeField] private float timePerDifficultyLevel = 15f; // Cada cuántos segundos sube el nivel
    [SerializeField] private float intervalDecreaseRate = 0.15f; // Cuánto se reduce el tiempo por nivel
    [SerializeField] private int maxEnemiesIncreasePerLevel = 5;  // Cuántos enemigos más se permiten por nivel

    private float currentSpawnInterval;
    private int currentMaxEnemies;
    private float nextSpawnTime;
    private float gameTimer;
    private int currentWave = 1;

    private void Start()
    {
        // 1. REINICIAR TIEMPO Y OLEADA INICIAL
        gameTimer = 0f;
        currentWave = 1;

        // 2. REINICIAR DIFICULTAD A LOS VALORES BASE
        currentSpawnInterval = initialSpawnInterval;
        currentMaxEnemies = initialMaxEnemies;
        nextSpawnTime = Time.time + currentSpawnInterval;

        // 3. Búsqueda automática del jugador si no se asignó en el Inspector
        if (playerTransform == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    private void Update()
    {
        if (playerTransform == null || enemyPrefab == null) return;

        // Contador de tiempo y progresión de dificultad
        gameTimer += Time.deltaTime;
        UpdateDifficulty();

        // Spawn continuo según el intervalo actual
        if (Time.time >= nextSpawnTime)
        {
            int currentEnemyCount = GameObject.FindGameObjectsWithTag("Enemy").Length;

            if (currentEnemyCount < currentMaxEnemies)
            {
                TrySpawnEnemyOnNavMesh();
            }

            nextSpawnTime = Time.time + currentSpawnInterval;
        }
    }

    private void UpdateDifficulty()
    {
        // Calcular en qué ronda/nivel de dificultad vamos según el tiempo transcurrido
        int calculatedWave = Mathf.FloorToInt(gameTimer / timePerDifficultyLevel) + 1;

        if (calculatedWave > currentWave)
        {
            currentWave = calculatedWave;

            // Aumentar la velocidad de spawn
            currentSpawnInterval = Mathf.Max(minSpawnInterval, currentSpawnInterval - intervalDecreaseRate);

            // Aumentar la capacidad máxima de enemigos en pantalla
            currentMaxEnemies = Mathf.Min(absoluteMaxEnemies, currentMaxEnemies + maxEnemiesIncreasePerLevel);

            Debug.Log($"<color=orange>¡DIFICULTAD AUMENTADA! Oleada {currentWave} | Intervalo: {currentSpawnInterval:F2}s | Máx Enemigos: {currentMaxEnemies}</color>");
        }
    }

    private void TrySpawnEnemyOnNavMesh()
    {
        for (int i = 0; i < 5; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized;
            float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
            Vector3 randomPoint = playerTransform.position + new Vector3(randomCircle.x, 0f, randomCircle.y) * distance;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                GameObject newEnemy = Instantiate(enemyPrefab, hit.position, Quaternion.identity);

                // --- MULTIPLICADORES DE PROGRESIÓN ---
                // Oleada 1 = 1.0x | Oleada 5 = 1.32x velocidad, +40% vida/daño
                float speedMultiplier = 1.0f + ((currentWave - 1) * 0.08f);
                float statMultiplier = 1.0f + ((currentWave - 1) * 0.10f); // +10% de salud/daño por oleada

                // Aplicar velocidad al IA del enemigo
                EnemyAI enemyAI = newEnemy.GetComponent<EnemyAI>();
                if (enemyAI != null)
                {
                    enemyAI.SetDifficultyMultiplier(speedMultiplier);
                }

                // Ajustar vida máxima del enemigo según la oleada
                HealthComponent enemyHealth = newEnemy.GetComponent<HealthComponent>();
                if (enemyHealth != null && enemyHealth.MaxHealth > 0)
                {
                    float scaledHealth = enemyHealth.MaxHealth * statMultiplier;
                    // Inicia con la vida escalada
                    enemyHealth.Heal(scaledHealth); 
                }

                return;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (playerTransform == null) return;

        // Dibuja los rangos de aparición alrededor del jugador en la vista de Escena
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(playerTransform.position, minSpawnDistance);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(playerTransform.position, maxSpawnDistance);
    }
}