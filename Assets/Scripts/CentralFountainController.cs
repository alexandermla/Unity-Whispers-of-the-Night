using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Cinemachine; // Asegúrate que esta línea esté presente

public class CentralFountainController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Referencia al componente PlayerController del jugador")]
    [SerializeField] private PlayerController playerController;
    
    
    [Header("Referencias Diosa")]
    [Tooltip("El Renderer de esta misma diosa-estatua (la que tiene este script). Asignar desde el Inspector.")]
    [SerializeField] private Renderer goddessStatueRenderer;
    [Tooltip("El GameObject de la 'diosa real' que se activará. Debe estar desactivado al inicio en la escena.")]
    [SerializeField] private GameObject realGoddessObject;
    [Tooltip("Material emisivo de la diosa-estatua. Debe ser HDRP/Lit.")]
    [SerializeField] private Material emissiveMaterial;
    [Tooltip("Sistema de partículas que se activa sobre la diosa-estatua al recoger las 4 estatuas.")]
    [SerializeField] private ParticleSystem overheadParticles;

    [Header("Configuración de Secuencia")]
    [Tooltip("Duración de la transición del material de la estatua.")]
    [SerializeField] private float materialTransitionDuration = 3.0f;
    [Tooltip("Hasta qué punto (0 a 1) llega la transición del material antes de parar (ej: 0.8 para 80%).")]
    [SerializeField] [Range(0f, 1f)] private float materialTransitionEndProgress = 0.8f;
    [Tooltip("Duración del movimiento de cámara de vuelta al jugador.")]
    [SerializeField] private float cameraReturnDuration = 2.0f;
    [Tooltip("Tiempo para esperar la transición inicial a la cámara de secuencia.")]
    [SerializeField] private float cameraInitialTransitionTime = 1.0f;

    [Header("Activación")]
    [Tooltip("Si es true, la luz se enciende automáticamente al recoger las 4 estatuas. La secuencia principal requiere un Trigger.")]
    [SerializeField] private bool activateLightOnAllStatues = true;

    [Header("Cinemachine")]
    [Tooltip("Arrastra aquí la Cámara Virtual que sigue al jugador normalmente.")]
    [SerializeField] private CinemachineCamera playerFollowCamera;
    [Tooltip("Arrastra aquí la Cámara Virtual que se usará para enfocar la diosa-estatua.")]
    [SerializeField] private CinemachineCamera goddessSequenceCamera;

    [Header("Eventos")]
    [Tooltip("Se dispara cuando el jugador entra en el trigger DESPUÉS de recoger las estatuas.")]
    [SerializeField] private UnityEvent onSequenceStarted;
    [Tooltip("Se dispara cuando la secuencia de revelación termina y la cámara vuelve al jugador.")]
    [SerializeField] private UnityEvent onSequenceCompleted;

    // --- Componentes Opcionales ---
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sequenceSound;
    [SerializeField] private float fadeOutDuration = 2.0f;
    [SerializeField] private float volumeScale = 1.0f;
    [Header("Enemigos Opcionales")]
    [SerializeField] private List<EnemyBase> enemiesToActivate;
    // ...

    // Estado interno
    private bool allStatuesCollected = false;
    private bool sequenceTriggered = false;
    private bool sequenceRunning = false;
    private Material goddessMaterialInstance; // Instancia del material para modificar

    private void Awake()
    {
        if (goddessStatueRenderer == null) goddessStatueRenderer = GetComponent<Renderer>();

        if (goddessStatueRenderer != null && emissiveMaterial != null)
        {
            // Crear instancia para no modificar el asset original
            goddessMaterialInstance = new Material(emissiveMaterial);
            goddessMaterialInstance.EnableKeyword("_EMISSION");
            // Inicializar con emisivo apagado
            if (goddessMaterialInstance.HasProperty("_EmissiveColor"))
            {
                goddessMaterialInstance.SetColor("_EmissiveColor", Color.black);
            }
            goddessStatueRenderer.material = goddessMaterialInstance;
        }
        else Debug.LogError("GoddessRevealController: Renderer o Emissive Material no asignado!", this);

        if (overheadParticles != null) overheadParticles.Stop(); // Asegurarse que esté detenido al inicio
        if (realGoddessObject != null) realGoddessObject.SetActive(false);

        // Comprobar estado inicial de estatuas
        if (StatueEvents.GetCollectedCount() >= 4)
        {
             Debug.Log("Detectado al inicio: Todas las estatuas ya recolectadas.");
             allStatuesCollected = true;
             if (overheadParticles != null) overheadParticles.Play(); // Si ya estaban recogidas, iniciar partículas
        }
    }

    private void OnEnable()
    {
        if (activateLightOnAllStatues)
        {
            StatueEvents.OnAllStatuesCollected += HandleAllStatuesCollected;
             if (StatueEvents.GetCollectedCount() >= 4 && !allStatuesCollected)
             {
                 HandleAllStatuesCollected();
             }
        }
    }

    private void OnDisable()
    {
        if (activateLightOnAllStatues)
        {
            StatueEvents.OnAllStatuesCollected -= HandleAllStatuesCollected;
        }
    }

    private void HandleAllStatuesCollected()
    {
        if (allStatuesCollected) return;
        Debug.Log("HandleAllStatuesCollected: Todas las estatuas recolectadas! Encendiendo luz overhead.");
        allStatuesCollected = true;
        
        // Solo activar el sistema de partículas overhead
        if (overheadParticles != null)
        {
            overheadParticles.Play();
            Debug.Log("Sistema de partículas overhead activado al recolectar todas las estatuas.");
        }
        else Debug.LogWarning("HandleAllStatuesCollected: Overhead Particles no asignado.", this);
    }

    // Método llamado por el script del Trigger (SequenceTrigger.cs)
    public void StartRevealSequenceFromTrigger()
    {
        if (allStatuesCollected && !sequenceTriggered && !sequenceRunning)
        {
             Debug.Log("StartRevealSequenceFromTrigger: Player entró en el trigger. Iniciando secuencia.");
            sequenceTriggered = true;
            StartCoroutine(GoddessRevealSequence());
        }
         else Debug.Log($"StartRevealSequenceFromTrigger: No iniciada (allStatuesCollected={allStatuesCollected}, sequenceTriggered={sequenceTriggered}, sequenceRunning={sequenceRunning})");
    }

    private IEnumerator FadeOutAudio(float duration)
    {
        if (audioSource == null) yield break;

        float startVolume = audioSource.volume;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        audioSource.Stop();
        audioSource.volume = startVolume;
    }

    private IEnumerator GoddessRevealSequence()
    {
        if (sequenceRunning) yield break;
        sequenceRunning = true;
        Debug.Log("Iniciando GoddessRevealSequence...");
        enemiesToActivate?.ForEach(enemy => enemy.gameObject.SetActive(false)); // Desactivar enemigos al inicio
        
        onSequenceStarted?.Invoke();

        // Iniciar audio de la secuencia
        if (audioSource != null && sequenceSound != null)
        {
            audioSource.clip = sequenceSound;
            audioSource.volume = volumeScale;
            audioSource.Play();
            Debug.Log("Audio de secuencia iniciado.");
        }

        // Desactivar movimiento y animaciones del jugador
        if (playerController != null)
        {
            playerController.enabled = false;
            var animator = playerController.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
            }
            Debug.Log("Movimiento y animaciones del jugador desactivados.");
        }
        else
        {
            Debug.LogWarning("PlayerController no asignado, no se puede desactivar el movimiento.");
        }

        // --- 1. Cambiar Cámara con transición suave ---
        if (goddessSequenceCamera != null && playerFollowCamera != null)
        {
            float elapsedTime = 0f;
            int initialPlayerPriority = playerFollowCamera.Priority.Value;
            int initialGoddessPriority = goddessSequenceCamera.Priority.Value;
            int targetPlayerPriority = 0;
            int targetGoddessPriority = 100;

            while (elapsedTime < cameraInitialTransitionTime)
            {
                float t = elapsedTime / cameraInitialTransitionTime;
                t = Mathf.SmoothStep(0, 1, t); // Suavizar la transición
                
                playerFollowCamera.Priority.Value = (int)Mathf.Lerp(initialPlayerPriority, targetPlayerPriority, t);
                goddessSequenceCamera.Priority.Value = (int)Mathf.Lerp(initialGoddessPriority, targetGoddessPriority, t);
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Asegurar valores finales
            playerFollowCamera.Priority.Value = (int)targetPlayerPriority;
            goddessSequenceCamera.Priority.Value = (int)targetGoddessPriority;
            Debug.Log("Transición suave a cámara de secuencia de diosa completada.");
        }
         else Debug.LogWarning("Cámaras Cinemachine no asignadas.");

        // Detener el sistema de partículas overhead al iniciar la secuencia
        if (overheadParticles != null)
        {
            overheadParticles.Stop();
            Debug.Log("Sistema de partículas overhead detenido para la secuencia.");
        }

        // --- 2. Iniciar Transición de Material Emisivo ---
        Debug.Log("Iniciando transición de material emisivo...");
        Coroutine materialTransitionCoroutine = null;
        if (goddessMaterialInstance != null)
        {
            // Asegurarse de que la instancia de material existe
            if (goddessStatueRenderer.material != goddessMaterialInstance) {
                goddessStatueRenderer.material = goddessMaterialInstance;
                Debug.LogWarning("Reasignando instancia de material al renderer.");
            }
            materialTransitionCoroutine = StartCoroutine(
                TransitionEmissiveMaterial(goddessMaterialInstance, materialTransitionDuration, materialTransitionEndProgress)
            );
        }
        else Debug.LogWarning("Material emisivo no asignado.");

        yield return new WaitForSeconds(materialTransitionDuration * 0.6f);

        // --- 3. Volver Cámara al Jugador (Inicia el movimiento) ---
         Debug.Log("Iniciando retorno de cámara al jugador...");
        if (goddessSequenceCamera != null && playerFollowCamera != null)
        {
            float elapsedTime = 0f;
            float transitionDuration = cameraReturnDuration * 0.5f; // Usar la mitad del tiempo de retorno para la transición
            int initialPlayerPriority = playerFollowCamera.Priority.Value;
            int initialGoddessPriority = goddessSequenceCamera.Priority.Value;
            int targetPlayerPriority = 10;
            int targetGoddessPriority = 0;

            while (elapsedTime < transitionDuration)
            {
                float t = elapsedTime / transitionDuration;
                t = Mathf.SmoothStep(0, 1, t); // Suavizar la transición
                
                playerFollowCamera.Priority.Value = (int)Mathf.Lerp(initialPlayerPriority, targetPlayerPriority, t);
                goddessSequenceCamera.Priority.Value = (int)Mathf.Lerp(initialGoddessPriority, targetGoddessPriority, t);
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Asegurar valores finales
            playerFollowCamera.Priority.Value = (int)targetPlayerPriority;
            goddessSequenceCamera.Priority.Value = (int)targetGoddessPriority;
            Debug.Log("Transición suave de vuelta a cámara del jugador completada.");
        }

        // --- 4. Iniciar Fade Out del Audio ---
        if (audioSource != null && audioSource.isPlaying)
        {
            StartCoroutine(FadeOutAudio(fadeOutDuration));
            Debug.Log("Iniciando fade out del audio.");
        }

        // --- 5. Durante el Retorno de Cámara: Ocultar Estatua, Mostrar Diosa ---
        float cameraReturnElapsed = 0f;
        bool goddessSwapped = false;
        while (cameraReturnElapsed < cameraReturnDuration)
        {
            cameraReturnElapsed += Time.deltaTime;
            if (!goddessSwapped && cameraReturnElapsed >= cameraReturnDuration * 0.4f)
            {
                 Debug.Log("Ocultando estatua, activando diosa real.");
                if (goddessStatueRenderer != null) goddessStatueRenderer.enabled = false;
                if (realGoddessObject != null) realGoddessObject.SetActive(true);
                 else Debug.LogWarning("Real Goddess Object no asignado.");
                goddessSwapped = true;
                enemiesToActivate?.ForEach(enemy => enemy.gameObject.SetActive(true)); // Activar enemigos
            }
            // Reactivar movimiento y animaciones del jugador
            if (playerController != null)
            {
                playerController.enabled = true;
                var animator = playerController.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.enabled = true;
                }
                Debug.Log("Movimiento y animaciones del jugador reactivados.");
            }
            yield return null;
        }
         Debug.Log("Retorno de cámara (tiempo) completado.");

        // --- 5. Asegurar que la transición de material se detuvo ---
        if (materialTransitionCoroutine != null)
        {
            StopCoroutine(materialTransitionCoroutine);
             Debug.Log("Transición de material detenida (si no había terminado).");
        }

        // --- 6. Limpieza y Finalización ---
        VillageQuestManager questManager = Object.FindAnyObjectByType<VillageQuestManager>();
        if (questManager != null)
        {
            questManager.TriggerEvent("GODDESS_REVEALED");
        }

        onSequenceCompleted?.Invoke();
        sequenceRunning = false;
        Debug.Log("Secuencia de revelación de diosa completada.");
    }

    // Corutina para la transición del material emisivo - HDRP/Lit
    private IEnumerator TransitionEmissiveMaterial(Material materialInstance, float duration, float endProgress)
    {
        if (materialInstance == null)
        {
            Debug.LogError("Material de instancia es nulo en TransitionEmissiveMaterial.");
            yield break;
        }

        float elapsed = 0f;
        float targetTime = duration * Mathf.Clamp01(endProgress);
        int frameCount = 0;

        Debug.Log($"Iniciando TransitionEmissiveMaterial. Duración total: {duration:F2}s, Progreso objetivo: {endProgress * 100:F0}%, Tiempo objetivo: {targetTime:F2}s");

        // Habilitar emisión y obtener colores para la transición
        materialInstance.EnableKeyword("_EMISSION");
        
        // Obtener el color base del material original
        Color baseEmissiveColor = emissiveMaterial.GetColor("_EmissiveColor");
        Color startColor = Color.black; // Comenzar desde apagado
        Color targetColor = baseEmissiveColor * 10f; // Intensidad máxima
        
        Debug.Log($"Iniciando transición emisiva desde {startColor} hasta {targetColor}");

        // Bucle de transición
        while (elapsed < targetTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0, 1, t);

            // Aplicar Emisión (Color HDR)
            if (materialInstance.HasProperty("_EmissiveColor"))
            {
                Color lerpedEmissive = Color.Lerp(startColor, targetColor, t);
                materialInstance.SetColor("_EmissiveColor", lerpedEmissive);
            }

            // Loguear con menos frecuencia
            frameCount++;
            if (frameCount % 10 == 0)
            {
                Debug.Log($"Transición Emisiva Frame {frameCount}: t={t:F3}, Emission={materialInstance.GetColor("_EmissiveColor")}");
            }

            yield return null;
        }

        // Forzar valor final
        if (materialInstance.HasProperty("_EmissiveColor"))
        {
            float final_t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(targetTime / duration));
            Color finalEmissive = Color.Lerp(startColor, targetColor, final_t);
            materialInstance.SetColor("_EmissiveColor", finalEmissive);
        }

        Debug.Log($"Transición emisiva ALCANZADA ({endProgress * 100:F0}%). Valores finales aplicados.");
    }



    // --- Código Opcional de Enemigos (sin cambios) ---
    /*
    private IEnumerator ActivateEnemies() { ... }
    private void ChangeEnemyMaterial(GameObject enemyObject, Color emissiveColor) { ... }
    */
}