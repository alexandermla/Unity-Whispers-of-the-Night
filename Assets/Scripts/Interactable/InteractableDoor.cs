using UnityEngine;
using UnityEngine.Localization; // <-- AÑADIDO
using System.Collections; // Para la corutina ShakeDoor

// Hereda de la base modificada
public class InteractableDoor : InteractableObject
{
    [Header("Configuración de Puerta")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 2.0f;
    [SerializeField] private AudioClip lockedSound;
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip ambientSoundClip;
    [SerializeField, Range(0f, 1f)] private float ambientVolumeScale = 0.5f;
    [SerializeField] private AudioClip heartbeatSoundClip;
    [SerializeField, Range(0f, 1f)] private float heartbeatVolumeScale = 0.8f;

    // --- MODIFICADO: Campos LocalizedString ---
    [Header("Localización (Puerta)")]
    [Tooltip("Mensaje localizado para cuando la puerta está bloqueada. Asignar Tabla y Clave en Inspector.")]
    [SerializeField] private LocalizedString lockedPromptMessage;
    [Tooltip("Mensaje localizado para cuando la puerta se puede abrir. Asignar Tabla y Clave en Inspector.")]
    [SerializeField] private LocalizedString unlockedPromptMessage;
    // -----------------------------------------

    [Header("Referencias")]
    [SerializeField] private DoorSequenceController sequenceController;
    [SerializeField] private AudioSource heartbeatAudioSource;
    [SerializeField] private GameObject doorVisual;

    private bool isLocked = true;
    private bool isOpening = false;
    private float currentOpenAngle = 0f;
    private Quaternion initialRotation;

    // Awake ahora solo inicializa lo específico de la puerta
    protected override void Awake()
    {
        base.Awake(); // Llama al Awake base (importante para audioSource)

        if (doorVisual != null)
        {
            initialRotation = doorVisual.transform.localRotation;
        }
        // Ya no se necesita asignar promptMessage aquí
    }

    private void Start()
    {
        // Iniciar sonido ambiental si existe
        if (audioSource != null && ambientSoundClip != null)
        {
            audioSource.clip = ambientSoundClip;
            audioSource.volume = ambientVolumeScale;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    private void Update()
    {
        if (isOpening && doorVisual != null)
        {
            // Animar apertura (sin cambios)
            currentOpenAngle = Mathf.MoveTowards(currentOpenAngle, openAngle, openSpeed * Time.deltaTime);
            doorVisual.transform.localRotation = initialRotation * Quaternion.Euler(0, currentOpenAngle, 0);

            if (Mathf.Approximately(currentOpenAngle, openAngle))
            {
                isOpening = false;
                if (sequenceController != null)
                {
                    // Asegurarse que el BoxCollider esté activo para la secuencia si es necesario
                    GetComponent<Collider>().enabled = true; // O usa BoxCollider si sabes que es ese tipo
                    sequenceController.ActivateSequence();
                }
            }
        }
    }

    // --- MODIFICADO: Sobrescribir GetPromptMessage ---
    public override string GetPromptMessage()
    {
        // Elige el LocalizedString correcto según el estado de la puerta
        LocalizedString stringToGet = isLocked ? lockedPromptMessage : unlockedPromptMessage;

        // Comprueba si el LocalizedString elegido está configurado
        if (stringToGet == null || stringToGet.IsEmpty)
        {
            Debug.LogWarning($"InteractableDoor ({gameObject.name}): LocalizedString no configurada para el estado actual (isLocked={isLocked}) en el Inspector.", this);
            // Usa la clave de la base como fallback si existe, o un string genérico
             return base.GetPromptMessage(); // Intenta obtener el de la base (que podría ser DEFAULT_INTERACT)
             // return "Interact";
        }

        // Obtiene la cadena localizada de forma síncrona
        string localizedText = stringToGet.GetLocalizedString();

        

        return localizedText;
    }
    // ---------------------------------------------

    public override void Interact()
    {
        // --- Comprobación de la linterna (Sin cambios funcionales) ---
        GameObject flashlightPickupObject = GameObject.FindWithTag("PickupFlashlight");
        bool flashlightExistsInScene = (flashlightPickupObject != null && flashlightPickupObject.activeInHierarchy);
        bool playerHasLamp = PlayerPrefs.GetInt(SaveSystem.LAMP_PICKED_UP_PREF_KEY, 0) == 1;

        // Debug.Log($"Door interact - isLocked: {isLocked}, PlayerHasLamp: {playerHasLamp}, FlashlightPickupExists: {flashlightExistsInScene}");

        if (isLocked)
        {
            // Desbloquear si tiene la lámpara Y el objeto recogible NO está en la escena
            if (playerHasLamp && !flashlightExistsInScene)
            {
                isLocked = false;
                if (audioSource != null && unlockSound != null) audioSource.PlayOneShot(unlockSound);
                Invoke(nameof(OpenDoor), 0.5f); // Abrir con un pequeño retraso
                Debug.Log("Door unlocked");
            }
            else // Sigue bloqueada
            {
                 // (Opcional) Localizar el mensaje de Debug si fuera necesario mostrarlo al usuario
                 // string lockedMessage = LocalizationSettings.StringDatabase.GetLocalizedString(promptTableName, "DOOR_NEEDS_LIGHT_SOURCE");
                 // Debug.Log(string.IsNullOrEmpty(lockedMessage) ? "[DOOR_NEEDS_LIGHT_SOURCE]" : lockedMessage);
                 Debug.Log("You need to find a light source..."); // Mantener simple para Debug

                if (audioSource != null && lockedSound != null) audioSource.PlayOneShot(lockedSound);
                if (doorVisual != null) StartCoroutine(ShakeDoor());
            }
        }
        else if (!isOpening) // Si no está bloqueada y no se está abriendo ya
        {
            OpenDoor();
        }
    }

    private void OpenDoor()
    {
        if (isOpening) return;

        // Detener sonido ambiental ANTES de abrir
        if (audioSource != null && audioSource.isPlaying && audioSource.loop && audioSource.clip == ambientSoundClip)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.volume = 1.0f; // Restaurar volumen para sonidos one-shot
        }

        // Llamar a la lógica base si es necesario (actualmente solo gestiona oneTimeUse y sonido base)
        // base.Interact(); // No es necesario aquí si ya manejamos sonido y hasBeenUsed no aplica a la puerta

        isOpening = true;
        if (audioSource != null && openSound != null) audioSource.PlayOneShot(openSound);
        Debug.Log("Opening door");
    }

    private IEnumerator ShakeDoor()
    {
        // ... (ShakeDoor sin cambios) ...
        Quaternion originalRotation = doorVisual.transform.localRotation;
        for (float t = 0; t < 0.1f; t += Time.deltaTime)
        {
            doorVisual.transform.localRotation = originalRotation * Quaternion.Euler(0, Mathf.Sin(t * 40) * 2, 0);
            yield return null;
        }
        doorVisual.transform.localRotation = originalRotation;
    }

    // --- Manejo de sonido del corazón (sin cambios) ---
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            bool flashlightExists = GameObject.FindWithTag("PickupFlashlight")?.activeInHierarchy ?? true; // Asume que existe si no lo encuentra
            bool playerHasLamp = PlayerPrefs.GetInt(SaveSystem.LAMP_PICKED_UP_PREF_KEY, 0) == 1;
            bool canUnlock = playerHasLamp && !flashlightExists;

            if (isLocked && canUnlock && heartbeatAudioSource != null && !heartbeatAudioSource.isPlaying && heartbeatSoundClip != null)
            {
                heartbeatAudioSource.clip = heartbeatSoundClip;
                heartbeatAudioSource.volume = heartbeatVolumeScale;
                heartbeatAudioSource.loop = true;
                heartbeatAudioSource.Play();
            }
        }
    }
     // OnTriggerExit no se usa actualmente, pero se podría añadir para detener el corazón si sale antes de abrir.
}