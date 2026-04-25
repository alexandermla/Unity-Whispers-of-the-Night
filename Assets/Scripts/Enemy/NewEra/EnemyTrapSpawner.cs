using UnityEngine;
using System.Collections; // <--- AÑADIDO using
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class EnemyTrapSpawner : MonoBehaviour
{
    [Header("Spawning Settings")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private List<Transform> spawnPoints;
    [SerializeField] private int numberOfEnemies = 1;
    [SerializeField] private float spawnDelay = 0.5f;
    [SerializeField] private bool activateOnce = true;

    [Header("Activation Effects (Optional)")]
    [SerializeField] private ParticleSystem activationVFX;
    [SerializeField] private AudioClip activationSound;
    [SerializeField] private AudioSource audioSource;

    private bool hasActivated = false;
    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null) { enabled = false; return; }
        if (!triggerCollider.isTrigger) { triggerCollider.isTrigger = true; }
        if (activationSound != null && audioSource == null) { audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && (!activateOnce || !hasActivated) && enemyPrefab != null && spawnPoints != null && spawnPoints.Count > 0)
        {
            hasActivated = true;
            StartCoroutine(SpawnSequence()); // Llama a la corutina
        }
    }

    // --- CORREGIDO: Tipo de retorno ---
    private IEnumerator SpawnSequence()
    // --- FIN CORRECCIÓN ---
    {
        if (activationVFX != null) activationVFX.Play();
        if (audioSource != null && activationSound != null) audioSource.PlayOneShot(activationSound);
        if (spawnDelay > 0) yield return new WaitForSeconds(spawnDelay);

        int spawnedCount = 0;
        List<Transform> availablePoints = new List<Transform>(spawnPoints);

        for (int i = 0; i < numberOfEnemies; i++)
        {
            if (availablePoints.Count == 0) break;
            int randomIndex = Random.Range(0, availablePoints.Count);
            Transform spawnPoint = availablePoints[randomIndex];
            availablePoints.RemoveAt(randomIndex);
            if (spawnPoint != null) { Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation); spawnedCount++; }
        }

        if (activateOnce) { gameObject.SetActive(false); } // Desactivar spawner
    }

     private void OnDrawGizmosSelected() { /* ... (Sin cambios) ... */
         if (spawnPoints == null || spawnPoints.Count == 0) return; Gizmos.color = Color.red; foreach (Transform point in spawnPoints) { if (point != null) { Gizmos.DrawWireSphere(point.position, 0.5f); Gizmos.DrawLine(transform.position, point.position); } }
         if (triggerCollider != null) { Gizmos.color = new Color(1f, 0f, 0f, 0.3f); Matrix4x4 oldMatrix = Gizmos.matrix; Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale); if (triggerCollider is BoxCollider box) Gizmos.DrawCube(box.center, box.size); else if (triggerCollider is SphereCollider sphere) Gizmos.DrawSphere(sphere.center, sphere.radius); Gizmos.matrix = oldMatrix; }
      }
}