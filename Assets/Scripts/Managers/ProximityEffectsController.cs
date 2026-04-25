using UnityEngine;
using UnityEngine.Rendering; // Necesario para Volume
// Asegúrate de incluir el namespace correcto para tu pipeline (HDRP o URP)
#if UNITY_PIPELINE_HDRP
using UnityEngine.Rendering.HighDefinition;
#elif UNITY_PIPELINE_URP
using UnityEngine.Rendering.Universal;
#endif

public class ProximityEffectsController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Arrastra aquí el componente Volume global que controla los efectos post-procesado.")]
    [SerializeField] private Volume globalVolume;
    [Tooltip("El transform del jugador. Si este script está en el jugador, puedes dejarlo vacío.")]
    [SerializeField] private Transform playerTransform;

    [Header("Configuración del Efecto")]
    [Tooltip("Distancia MÁXIMA a un enemigo para que el efecto empiece a aparecer (intensidad mínima).")]
    [SerializeField] private float maxEffectDistance = 15f;
    [Tooltip("Distancia MÍNIMA a un enemigo para que el efecto alcance su intensidad máxima.")]
    [SerializeField] private float minEffectDistance = 3f;
    [Tooltip("Con qué frecuencia (en segundos) se actualiza el efecto. Más bajo es más reactivo pero consume más.")]
    [SerializeField] private float updateInterval = 0.1f;

    [Header("Vignette")]
    [Range(0f, 1f)]
    [SerializeField] private float minVignetteIntensity = 0f;
    [Range(0f, 1f)]
    [SerializeField] private float maxVignetteIntensity = 0.6f;

    [Header("Chromatic Aberration")]
    [Range(0f, 1f)]
    [SerializeField] private float minChromaticAberrationIntensity = 0f;
    [Range(0f, 1f)]
    [SerializeField] private float maxChromaticAberrationIntensity = 0.5f;

    // Referencias internas a los overrides del Volume
    private Vignette vignetteOverride;
    private ChromaticAberration chromaticAberrationOverride;

    private float timeSinceLastUpdate = 0f;
    private LayerMask enemyLayerMask; // Para optimizar la búsqueda de enemigos

    void Start()
    {
        // Obtener el transform del jugador si no está asignado
        if (playerTransform == null)
        {
            // Intentar encontrarlo por tag
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                // Si este script está en el jugador, usar su propio transform
                playerTransform = transform;
            }
        }

        // Validar referencia al Volume
        if (globalVolume == null)
        {
            Debug.LogError("ProximityEffectsController: ¡Global Volume no asignado!", this);
            enabled = false; // Desactivar el script si no hay volume
            return;
        }

        // Intentar obtener los overrides del profile asignado al Volume
        if (globalVolume.profile == null)
        {
            Debug.LogError($"ProximityEffectsController: El Volume '{globalVolume.gameObject.name}' no tiene un Volume Profile asignado.", this);
            enabled = false;
            return;
        }

        // Usar TryGet para obtener los overrides de forma segura
        globalVolume.profile.TryGet(out vignetteOverride);
        globalVolume.profile.TryGet(out chromaticAberrationOverride);

        // Comprobar si se encontraron los overrides
        if (vignetteOverride == null)
        {
            Debug.LogWarning("ProximityEffectsController: Override 'Vignette' no encontrado o añadido en el Volume Profile asignado.", this);
        }
        else
        {
            // Asegurarse de que la intensidad esté activa para poder modificarla
            vignetteOverride.intensity.overrideState = true;
        }

        if (chromaticAberrationOverride == null)
        {
            Debug.LogWarning("ProximityEffectsController: Override 'Chromatic Aberration' no encontrado o añadido en el Volume Profile asignado.", this);
        }
        else
        {
            // Asegurarse de que la intensidad esté activa para poder modificarla
            chromaticAberrationOverride.intensity.overrideState = true;
        }

        // Obtener la LayerMask de los enemigos (asumiendo que tienes una capa "Enemy")
        // Si no, tendrás que buscar por tag, lo cual es menos eficiente.
        enemyLayerMask = LayerMask.GetMask("Enemy"); // <<< ¡Asegúrate que tu capa se llame "Enemy"!
        if (enemyLayerMask.value == 0) // Si no encontró la capa "Enemy"
        {
            Debug.LogWarning("ProximityEffectsController: No se encontró la Layer 'Enemy'. La detección de enemigos podría no funcionar. Asegúrate de crear y asignar la capa 'Enemy' a tus enemigos.", this);
            // Podrías añadir un fallback para buscar por tag aquí si es necesario
        }

        // Establecer valores iniciales (mínimos)
        UpdateEffects(0f); // Aplicar intensidad 0 al inicio
    }

    void Update()
    {
        // Controlar la frecuencia de actualización
        timeSinceLastUpdate += Time.deltaTime;
        if (timeSinceLastUpdate >= updateInterval)
        {
            timeSinceLastUpdate = 0f;
            UpdateProximityEffects();
        }
    }

    void UpdateProximityEffects()
    {
        if (playerTransform == null) return; // Salir si no tenemos jugador

        float minSqrDistance = float.MaxValue;
        bool enemyFound = false;

        // Buscar enemigos cercanos usando OverlapSphere (más eficiente que FindObjectsOfType)
        Collider[] nearbyColliders = Physics.OverlapSphere(playerTransform.position, maxEffectDistance, enemyLayerMask);

        foreach (Collider col in nearbyColliders)
        {
            // Podrías añadir un check extra si no todos en la capa son EnemyIAVillage
            // if (col.GetComponent<EnemyIAVillage>() != null)
            // {
            float sqrDist = (col.transform.position - playerTransform.position).sqrMagnitude;
            if (sqrDist < minSqrDistance)
            {
                minSqrDistance = sqrDist;
                enemyFound = true;
            }
            // }
        }

        float intensityFactor = 0f; // Factor de 0 a 1 para la intensidad

        if (enemyFound)
        {
            float distance = Mathf.Sqrt(minSqrDistance);

            if (distance <= minEffectDistance)
            {
                // Muy cerca -> Máxima intensidad
                intensityFactor = 1f;
            }
            else if (distance < maxEffectDistance)
            {
                // En el rango -> Interpolar intensidad
                // Calcula t: 1 cuando distance=min, 0 cuando distance=max
                intensityFactor = 1f - ((distance - minEffectDistance) / (maxEffectDistance - minEffectDistance));
            }
            // Si distance >= maxEffectDistance, intensityFactor permanece 0 (ya inicializado)
        }
        // else -> No hay enemigos cerca, intensityFactor permanece 0

        // Aplicar la intensidad calculada a los efectos
        UpdateEffects(intensityFactor);
    }

    void UpdateEffects(float t) // t va de 0 (lejos) a 1 (cerca)
    {
        t = Mathf.Clamp01(t); // Asegurar que t esté entre 0 y 1

        // Actualizar Vignette si existe
        if (vignetteOverride != null)
        {
            vignetteOverride.intensity.value = Mathf.Lerp(minVignetteIntensity, maxVignetteIntensity, t);
        }

        // Actualizar Chromatic Aberration si existe
        if (chromaticAberrationOverride != null)
        {
            chromaticAberrationOverride.intensity.value = Mathf.Lerp(minChromaticAberrationIntensity, maxChromaticAberrationIntensity, t);
        }
    }
}