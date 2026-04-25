// SleepingEnemyController.cs (Modificado)
using UnityEngine;
using WoN.Interfaces;
using System.Collections.Generic; // Necesario para List si usamos array de renderers

// Añadir la interfaz ILockOnVisuals
public class SleepingEnemyController : EnemyIA2, ILockOnVisuals
{
    [Header("Tutorial Enemy")]
    [SerializeField] private GameObject sleepingVisualEffect;
    [SerializeField] private float timeToKill = 2.0f;
    [SerializeField] private Color deathColor = Color.gray;

    // --- NUEVO: Materiales para Lock-On ---
    [Header("Lock-On Visuals")]
    [Tooltip("Material base del enemigo dormido.")]
    [SerializeField] private Material baseMaterial; // Asignar en Inspector
    [Tooltip("Material que se aplica cuando el enemigo está fijado.")]
    [SerializeField] private Material lockedOnMaterial; // Asignar en Inspector
    // --------------------------------------

    private bool hasBeenHitByLight = false;
    private bool isUnderLight = false;
    private float lightExposureTime = 0f;
    private VillageQuestManager questManager;

    // --- Modificado para manejar múltiples materiales/renderers ---
    private List<Renderer> enemyRenderers = new List<Renderer>(); // Lista de todos los renderers
    private List<Material> originalMaterials = new List<Material>(); // Materiales originales (antes de efectos)
    private bool isCurrentlyLockedOn = false; // Estado de lock-on
    // -----------------------------------------------------------


    protected override void Awake()
    {
        base.Awake(); // Llama al Awake de EnemyIA2 si existe

        questManager = Object.FindAnyObjectByType<VillageQuestManager>(); // Usar FindObjectOfType genérico

        if (sleepingVisualEffect != null) sleepingVisualEffect.SetActive(true);

        // --- Obtener Renderers y Materiales Originales ---
        enemyRenderers.AddRange(GetComponentsInChildren<Renderer>()); // Obtener todos los renderers
        originalMaterials.Clear();
        foreach(Renderer rend in enemyRenderers)
        {
            if (rend.material != null)
            {
                // Guardar una copia del material original (si es el base)
                // O guardar la referencia si no se va a instanciar aquí
                 if (baseMaterial != null && rend.sharedMaterial == baseMaterial) {
                     originalMaterials.Add(baseMaterial); // Guardar referencia al Asset original
                 } else {
                     // Si no es el material base, guardar el que tenga actualmente
                     originalMaterials.Add(rend.sharedMaterial);
                 }
            } else {
                originalMaterials.Add(null); // Añadir null si no tiene material
            }
        }
        // Aplicar material base inicial
        ApplyMaterialBasedOnState();
        // -------------------------------------------------
    }

    protected override void Start()
    {
        // Modificado: No llamar a base.Start() para evitar ocultar renderers
        // base.Start();
        SetRenderersEnabled(true); // Asegurar que estén visibles
    }

    protected override void Update()
    {
        // No llamar a base.Update()

        if (isUnderLight)
        {
            lightExposureTime += Time.deltaTime;
            float progress = Mathf.Clamp01(lightExposureTime / timeToKill);

            // Aplicar color de muerte progresivo a TODOS los renderers
            ApplyDeathColorFade(progress);

            if (lightExposureTime >= timeToKill)
            {
                Die();
            }
        }
    }

    // --- NUEVO: Método para aplicar el material correcto según el estado ---
    private void ApplyMaterialBasedOnState()
    {
        if (enemyRenderers.Count == 0) return;

        Material materialToApply = baseMaterial; // Por defecto, el base

        if (isCurrentlyLockedOn && lockedOnMaterial != null)
        {
            materialToApply = lockedOnMaterial;
        }

        // Si no hay material específico que aplicar, usar el original guardado
        // Esto es importante si el modelo tiene múltiples materiales inicialmente
        for(int i = 0; i < enemyRenderers.Count; i++)
        {
             if (enemyRenderers[i] != null) {
                 Material mat = materialToApply;
                 // Si no hay material de lockon/base definido, intentar usar el original para este slot
                 if (mat == null && i < originalMaterials.Count && originalMaterials[i] != null) {
                     mat = originalMaterials[i];
                 }
                 // Si aún así es null, no podemos hacer nada
                 if (mat != null) {
                     enemyRenderers[i].material = mat; // Aplicar material
                 }
             }
        }
    }
    // --- FIN NUEVO ---

    // --- NUEVO: Método para aplicar el fade de color al morir ---
    private void ApplyDeathColorFade(float progress)
    {
        for (int i = 0; i < enemyRenderers.Count; i++)
        {
             // Asegurarse de que trabajamos sobre el material ACTUAL del renderer
             if (enemyRenderers[i] != null && enemyRenderers[i].material != null && i < originalMaterials.Count)
             {
                 // Necesitamos el color original de ESTE material específico para el Lerp
                 // Usaremos el color del material original guardado en Awake como base
                 Color originalColor = (originalMaterials[i] != null) ? originalMaterials[i].color : Color.white; // Fallback a blanco
                 enemyRenderers[i].material.color = Color.Lerp(originalColor, deathColor, progress);
             }
        }
    }
    // --- FIN NUEVO ---

    // --- Implementación de la interfaz ILockOnVisuals ---
    public void SetLockOnMaterial(bool isLockedOn)
    {
        isCurrentlyLockedOn = isLockedOn;

        // No cambiar material si está bajo el efecto de la luz (prioridad al efecto de muerte)
        if (isUnderLight)
        {
             Debug.Log("SleepingEnemy: Lock-on visual omitido (isUnderLight=true)");
            return;
        }

        // Aplicar el material correspondiente (base o lockedOn)
        ApplyMaterialBasedOnState();
    }
    // ----------------------------------------------------

    public override void OnLightExposure(Transform lightSource = null, float intensity = 1.0f)
    {
        if (!hasBeenHitByLight)
        {
            hasBeenHitByLight = true;
            isUnderLight = true;
            lightExposureTime = 0f; // Reiniciar timer al empezar exposición

            if (sleepingVisualEffect != null) sleepingVisualEffect.SetActive(false);

            // IMPORTANTE: Al empezar el efecto de luz, asegurarse de que
            // el material base sea el que está activo para que el fade a gris funcione bien,
            // incluso si estaba fijado justo antes.
            isCurrentlyLockedOn = false; // Forzar estado no-fijado visualmente
            ApplyMaterialBasedOnState(); // Aplicará el material base
        }
    }

    private void Die()
    {
        if (questManager != null)
        {
            questManager.TriggerEvent("FLASHLIGHT_USED_ON_ENEMY");
            questManager.TriggerEvent("ENEMY_KILLED_BY_LIGHT");
        }

        // Asegurar color final gris
        ApplyDeathColorFade(1.0f);

        // Desactivar el LockOnTarget para que no sea seleccionable
        LockOnTarget lockOn = GetComponent<LockOnTarget>();
        if(lockOn != null) lockOn.enabled = false;

        // Desactivar el enemigo (o destruirlo después de un delay)
        gameObject.SetActive(false);
        Debug.Log("Tutorial enemy killed by light");
    }

    public void SetSleepingState(bool sleeping)
    {
        if (sleepingVisualEffect != null) sleepingVisualEffect.SetActive(sleeping);
        if (sleeping) ResetLightHitStatus(); // Resetear al poner a dormir
    }

    public bool WasHitByLight() { return hasBeenHitByLight; }

    public void ResetLightHitStatus()
    {
        hasBeenHitByLight = false;
        isUnderLight = false;
        lightExposureTime = 0f;
        isCurrentlyLockedOn = false; // Resetear estado lockon

        if (sleepingVisualEffect != null) sleepingVisualEffect.SetActive(true);

        // --- Restaurar Materiales Originales ---
        for(int i=0; i < enemyRenderers.Count; i++)
        {
            if(enemyRenderers[i] != null && i < originalMaterials.Count && originalMaterials[i] != null)
            {
                enemyRenderers[i].material = originalMaterials[i];
                // Restaurar color original por si quedó a medio morir
                enemyRenderers[i].material.color = originalMaterials[i].color;
            }
        }
        // --------------------------------------

        // Re-activar LockOnTarget si existe
        LockOnTarget lockOn = GetComponent<LockOnTarget>();
        if(lockOn != null) lockOn.enabled = true;

        gameObject.SetActive(true); // Asegurarse que esté activo
    }


} // Fin de la clase