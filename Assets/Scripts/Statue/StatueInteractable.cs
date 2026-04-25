using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class StatueInteractable : MonoBehaviour, IInteractable
{
    [Header("Identificación")]
    [SerializeField] private string statueId;
    [SerializeField] private string displayName;

    [Header("Historia")]
    [SerializeField] private StatueStory statueStory;

    [Header("Visuales (HDRP/Lit)")]
    [Tooltip("El material principal de la estatua. Debe usar un shader HDRP/Lit.")]
    [SerializeField] private Material statueMaterial;
    [Tooltip("Color HDR de la emisión cuando la estatua está activada.")]
    [SerializeField] [ColorUsage(true, true)] private Color activatedEmissionColor = Color.yellow;

    [Header("Efectos de Interacción")]
    [Tooltip("Partículas que orbitan la estatua ANTES de interactuar.")]
    [SerializeField] private ParticleSystem orbitingFirefliesParticles;
    [Tooltip("Partículas que explotan/se activan AL interactuar.")]
    [SerializeField] private ParticleSystem activationBurstParticles;
    
    [Header("Audio")]
    [Tooltip("Sonido que se reproduce al activar la estatua")]
    [SerializeField] private AudioClip interactionSound;
    [Tooltip("Sonido que se reproduce en bucle para guiar al jugador hacia la estatua")]
    [SerializeField] private AudioClip guidanceSound;
    [Tooltip("Distancia mínima a la que se escucha el sonido al máximo volumen")]
    [SerializeField] private float audioMinDistance = 2f;
    [Tooltip("Distancia máxima a la que se puede escuchar el sonido")]
    [SerializeField] private float audioMaxDistance = 8f;
    [Tooltip("Volumen general de los sonidos de la estatua")]
    [Range(0f, 1f)]
    [SerializeField] private float audioVolume = 0.3f;
    [Tooltip("Luz puntual opcional que se activa/desactiva con la estatua.")]
    [SerializeField] private Light statueLight;
    [Tooltip("Duración del efecto visual/sonoro de activación inicial.")]
    [SerializeField] private float activationEffectDuration = 1.5f;

    // Estado interno
    private bool hasInteracted = false;
    private bool isPlayerNearby = false;
    private bool isStoryPanelOpen = false; // Estado deseado del panel
    private AudioSource audioSource;
    private Renderer statueRenderer;
    private Material materialInstance;
    private Coroutine activationCoroutine = null;
    private Coroutine panelCoroutine = null; // Corutina específica para el panel

    private void Awake()
    {
        statueRenderer = GetComponent<Renderer>();
        // Intenta obtener el AudioSource existente
        audioSource = GetComponent<AudioSource>();

        // Si no existe, añádelo
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; // Buena práctica establecerlo aquí
        }

        // --- CONFIGURACIÓN 3D (APLICAR SIEMPRE) ---
        // Asegúrate de que estas propiedades se establezcan independientemente
        // de si el componente ya existía o se acaba de añadir.
        audioSource.spatialBlend = 1f; // 1 = 3D completo
        //audioSource.rolloffMode = AudioRolloffMode.Logarithmic; // O la curva que prefieras
        audioSource.minDistance = audioMinDistance;
        audioSource.maxDistance = audioMaxDistance;
        audioSource.volume = audioVolume; // Establecer volumen general aquí
        audioSource.loop = false; // El loop se gestionará en Start/Interact
        audioSource.dopplerLevel = 0f; // Desactivar efecto Doppler si no lo quieres

        // --- Configuración específica del sonido de guía ---
        // (Se moverá a Start para manejar el caso de estatua ya recolectada)

        // Configuración del material
        if (statueRenderer != null && statueMaterial != null)
        {
            materialInstance = new Material(statueMaterial);
            statueRenderer.material = materialInstance;
        }
        else Debug.LogError($"StatueInteractable ({gameObject.name}): Renderer o Material no asignado.", this);
    }

    // Modificación en Start para manejar la reproducción inicial
    private void Start()
    {
        if (StatueEvents.IsStatueCollected(statueId))
        {
            hasInteracted = true;
            SetActivatedVisuals(true, true);
            // Asegurarse de que no se reproduzca ningún sonido (ni guía ni interacción)
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null; // Quitar el clip por si acaso
                audioSource.loop = false;
            }
        }
        else // La estatua NO ha sido recolectada
        {
            hasInteracted = false;
            SetActivatedVisuals(false, true);
            // Iniciar el sonido de guía si existe y el AudioSource está listo
            if (audioSource != null && guidanceSound != null)
            {
                audioSource.clip = guidanceSound; // Asignar el clip aquí
                audioSource.loop = true;          // Poner en bucle
                // El volumen ya se estableció en Awake
                audioSource.Play();               // Iniciar reproducción
                Debug.Log($"Playing guidance sound for {gameObject.name}"); // Log para confirmar
            }
            else if (guidanceSound == null)
            {
                Debug.LogWarning($"StatueInteractable ({gameObject.name}): Guidance Sound no asignado.", this);
            }
        }
    }

    public string GetPromptMessage()
    {
        if (!hasInteracted)
            return $"Press E to interact with {displayName}";
        else
            // The prompt now reflects the DESIRED state (isStoryPanelOpen) to avoid confusion during fade
            return $"Press E to {(isStoryPanelOpen ? "close" : "view")} {displayName}'s story";
            // Alternativa: usar el estado visual real, pero puede ser confuso si se espamea
            // return $"Presiona E para {(StatueUIManager.Instance != null && StatueUIManager.Instance.IsStoryPanelVisible ? "cerrar" : "ver")} la historia de {displayName}";
    }

    public void Interact()
    {
        if (!isPlayerNearby) return;

        if (!hasInteracted)
        {
            // Primera interacción: Iniciar secuencia si no está corriendo
            if (activationCoroutine == null)
            {
                activationCoroutine = StartCoroutine(InteractSequence());
            }
        }
        else
        {
            // --- CORRECCIÓN PARA SPAM ---
            // No hacer nada si ya hay una animación de panel en curso
            if (panelCoroutine != null)
            {
                 Debug.Log("Interact: Ignorado por panelCoroutine en curso.");
                return;
            }
            // --- FIN CORRECCIÓN ---

            // Interacciones posteriores: Abrir/cerrar panel basado en estado deseado
            if (isStoryPanelOpen) // Si nuestro estado dice que debería estar abierto (o abriéndose)
            {
                HideStoryPanel();
            }
            else // Si nuestro estado dice que debería estar cerrado (o cerrándose)
            {
                // Solo mostrar el panel si el jugador sigue en rango
                if (isPlayerNearby)
                {
                    ShowStoryPanel();
                }
            }
        }
    }

    private IEnumerator InteractSequence()
    {
        hasInteracted = true;

        if (orbitingFirefliesParticles != null) orbitingFirefliesParticles.Stop();
        if (activationBurstParticles != null) activationBurstParticles.Play();

        // Detener el sonido de guía explícitamente
        if (audioSource != null)
        {
            // Comprueba si el sonido de guía era el que estaba sonando
            if (audioSource.clip == guidanceSound && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            audioSource.loop = false; // Asegurarse de quitar el loop

            // Reproducir el sonido de activación (como ya lo hacías)
            if (interactionSound != null)
            {
                // Usa PlayOneShot para el sonido de interacción, pero asegúrate
                // de que no interfiera con el volumen global si es necesario.
                // PlayOneShot(clip, volumeScale) puede ser útil.
                audioSource.PlayOneShot(interactionSound /*, audioVolume */); // Puedes ajustar el volumen aquí si quieres
            }
        }

        //if (activationBurstParticles != null) activationBurstParticles.Play();

        yield return new WaitForSeconds(activationEffectDuration * 0.5f);

        SetActivatedVisuals(true, false);

        yield return new WaitForSeconds(activationEffectDuration * 0.5f);

        StatueEvents.StatueCollected(statueId, transform.position);
        if (isPlayerNearby)
        {
            ShowStoryPanel();
        }

        activationCoroutine = null;
    }

    private void SetActivatedVisuals(bool activated, bool instant = false)
    {
        if (materialInstance != null)
        {
            Color targetEmission = activated ? activatedEmissionColor : Color.black;
            if (activated) materialInstance.EnableKeyword("_EMISSION");
            materialInstance.SetColor("_EmissiveColor", targetEmission);
            // if (!activated) materialInstance.DisableKeyword("_EMISSION"); // Opcional
        }

        if (statueLight != null) statueLight.enabled = activated;

        if (orbitingFirefliesParticles != null)
        {
            if (activated && orbitingFirefliesParticles.isPlaying) orbitingFirefliesParticles.Stop();
            else if (!activated && !orbitingFirefliesParticles.isPlaying) orbitingFirefliesParticles.Play();
        }
    }

    private void ShowStoryPanel()
    {
        if (statueStory == null) { Debug.LogWarning($"No StatueStory for {gameObject.name}"); return; }
        if (StatueUIManager.Instance == null) { Debug.LogError("StatueUIManager Instance is null!"); return; }

        // Solo iniciar si no está ya abierto o abriéndose
        if (isStoryPanelOpen) return;
        // Detener corutina anterior (de cierre) si existe
        if (panelCoroutine != null) StopCoroutine(panelCoroutine);

        isStoryPanelOpen = true;
        panelCoroutine = StartCoroutine(RunPanelSequence(true));
    }

    private void HideStoryPanel()
    {
        // Solo iniciar si no está ya cerrado o cerrándose
        if (!isStoryPanelOpen) return;
        if (StatueUIManager.Instance == null) { Debug.LogError("StatueUIManager Instance is null!"); return; }

        // Detener corutina anterior (de apertura) si existe
        if (panelCoroutine != null) StopCoroutine(panelCoroutine);

        isStoryPanelOpen = false;
        panelCoroutine = StartCoroutine(RunPanelSequence(false));
    }

    private IEnumerator RunPanelSequence(bool show)
    {
        // Debug.Log($"RunPanelSequence - Show: {show}");
        if (show)
        {
            StatueUIManager.Instance.ShowStoryPanel(statueStory);
            // Esperar hasta que esté visible O si se canceló el estado deseado
            yield return new WaitUntil(() => (StatueUIManager.Instance != null && StatueUIManager.Instance.IsStoryPanelVisible) || !isStoryPanelOpen);
        }
        else
        {
            StatueUIManager.Instance.HideStoryPanel();
            // Esperar hasta que NO esté visible O si se canceló el estado deseado
            yield return new WaitUntil(() => (StatueUIManager.Instance == null || !StatueUIManager.Instance.IsStoryPanelVisible) || isStoryPanelOpen);
        }
        // Debug.Log($"RunPanelSequence - Finished Show: {show}");
        panelCoroutine = null; // Liberar corutina al finalizar
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            // Ocultar panel si debería estar abierto al salir
            // La llamada a HideStoryPanel ya detiene la corutina si se estaba abriendo
            HideStoryPanel();
        }
    }

    #if UNITY_EDITOR // Solo compilar en el editor
    private void OnDrawGizmosSelected()
    {
        // Dibuja la esfera de Min Distance
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, audioMinDistance);

        // Dibuja la esfera de Max Distance
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, audioMaxDistance);
    }
    #endif
}