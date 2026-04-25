using UnityEngine;
using UnityEngine.Localization; // <-- AÑADIDO

// Clase base para todos los objetos interactivos
public class InteractableObject : MonoBehaviour, IInteractable
{
    // --- MODIFICADO: Usar LocalizedString ---
    [Header("Localización")]
    [Tooltip("Mensaje de interacción localizado. Configúralo en el Inspector seleccionando la Tabla y la Clave.")]
    [SerializeField] protected LocalizedString promptMessage;
    // ------------------------------------------

    [Header("Configuración Básica")]
    // [SerializeField] protected string promptMessage = "Press E to interact"; // <-- ELIMINADO
    [SerializeField] protected float interactionDistance = 3f;
    [SerializeField] protected AudioClip interactionSound;
    [SerializeField] protected bool oneTimeUse = false;

    protected bool hasBeenUsed = false;
    protected AudioSource audioSource;

    protected virtual void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && interactionSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; // Buena práctica
        }
    }

    // --- MODIFICADO: Obtener texto desde LocalizedString ---
    public virtual string GetPromptMessage()
    {
        // Comprueba si el LocalizedString está correctamente configurado
        if (promptMessage == null || promptMessage.IsEmpty)
        {
            Debug.LogWarning($"InteractableObject ({gameObject.name}): promptMessage (LocalizedString) no configurado en el Inspector.", this);
            return "Interact"; // Mensaje genérico por defecto
        }

        // Obtiene la cadena localizada de forma síncrona (más simple para prompts)
        string localizedText = promptMessage.GetLocalizedString();

        // Comprueba si la carga falló (clave no encontrada, etc.)
        

        return localizedText;
    }
    // -------------------------------------------------------

    public virtual void Interact()
    {
        if (oneTimeUse && hasBeenUsed) return;

        if (audioSource != null && interactionSound != null)
        {
            audioSource.PlayOneShot(interactionSound);
        }

        hasBeenUsed = true;
    }

    public bool CanInteract()
    {
        // Añadido chequeo por si el objeto ha sido usado (oneTimeUse)
        return (!oneTimeUse || !hasBeenUsed);
    }
}