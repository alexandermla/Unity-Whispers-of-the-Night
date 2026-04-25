using System.Collections;
using UnityEngine;
using WoN.Interfaces;

public class LightVulnerableEnemy : MonoBehaviour, ILightReactive
{
    [Header("Light Vulnerability Settings")]
    [SerializeField] private float lightExposureThreshold = 0.5f;
    [SerializeField] private float deathDuration = 3.0f;
    [SerializeField] private float currentLightExposure = 0f;
    [SerializeField] private bool isDying = false;

    [Header("Visual Feedback")]
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material exposedMaterial;
    [SerializeField] private Renderer enemyRenderer;
    [SerializeField] private ParticleSystem deathParticles;

    [Header("References")]
    [SerializeField] private VillageQuestManager questManager;

    // Track if this enemy can be damaged by light
    private bool isVulnerableToLight = true;

    private void Start()
    {
        // Find quest manager if not assigned
        if (questManager == null)
        {
            questManager = Object.FindAnyObjectByType<VillageQuestManager>();
        }

        // Set initial material
        if (enemyRenderer == null)
        {
            enemyRenderer = GetComponentInChildren<Renderer>();
        }

        if (enemyRenderer != null && normalMaterial != null)
        {
            enemyRenderer.material = normalMaterial;
        }
    }

    // Implements ILightReactive interface
    public void OnLightExposure(Transform lightSource = null, float intensity = 1.0f)
    {
        if (!isVulnerableToLight || isDying)
            return;

        // Increase light exposure based on intensity
        // Using Time.deltaTime to convert to duration-based exposure
        currentLightExposure += intensity * Time.deltaTime;

        // Visual feedback on light exposure
        UpdateVisualFeedback();

        // Check if exposure threshold reached
        if (currentLightExposure >= lightExposureThreshold && !isDying)
        {
            StartCoroutine(DieFromLight());
        }
    }

    private void UpdateVisualFeedback()
    {
        if (enemyRenderer == null || normalMaterial == null || exposedMaterial == null)
            return;

        // Calculate exposure ratio (0-1)
        float exposureRatio = Mathf.Clamp01(currentLightExposure / lightExposureThreshold);

        // Visual feedback - lerp between materials or change material properties
        if (exposureRatio > 0.5f && enemyRenderer.material != exposedMaterial)
        {
            enemyRenderer.material = exposedMaterial;
        }
        else if (exposureRatio <= 0.5f && enemyRenderer.material != normalMaterial)
        {
            enemyRenderer.material = normalMaterial;
        }
    }

    private IEnumerator DieFromLight()
    {
        isDying = true;
        Debug.Log("Enemy dying from light exposure");

        // Trigger event for tutorial progression
        if (questManager != null)
        {
            questManager.TriggerEvent("ENEMY_KILLED_BY_LIGHT");
        }

        // Play death particles if available
        if (deathParticles != null)
        {
            deathParticles.Play();
        }

        // Wait for death duration
        float elapsed = 0f;
        while (elapsed < deathDuration)
        {
            // Optional: Add dissolve effect or other visual effects during death
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Destroy the enemy
        Destroy(gameObject);
    }

    // Used for debugging
    private void OnDrawGizmos()
    {
        if (isDying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 1.0f);
        }
    }
}