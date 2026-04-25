using UnityEngine;
// using UnityEngine.Localization; // No es necesario aquí si no se usa directamente

public class InteractableFlashlight : InteractableObject // Hereda de la base modificada
{
    [Header("Referencias")]
    [SerializeField] private GameObject playerFlashlightObject;
    // ¡OJO! Asegúrate que este es el QuestManager correcto para la escena (Tutorial o Village)
    [SerializeField] private TutorialQuestManager questManager;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioSource playerAudioSource;

    [Header("UI References")]
    [SerializeField] private GameObject lampEmptyUI;
    [SerializeField] private GameObject lampFillUI;

    // Ya no se necesita Awake para asignar el mensaje, se hace en el Inspector
    // sobre el campo 'promptMessage' heredado.

    public override void Interact()
    {
        // --- IMPORTANTE: Llama a base.Interact() PRIMERO si quieres la lógica base (sonido, hasBeenUsed) ---
        // base.Interact(); // Descomenta si quieres el sonido de interacción base además del de pickup

        // Reproducir sonido desde el player
        if (playerAudioSource != null && pickupSound != null)
        {
            playerAudioSource.PlayOneShot(pickupSound);
        }
        else if (interactionSound != null && audioSource != null) // Fallback al sonido base si no hay playerAudioSource
        {
             audioSource.PlayOneShot(interactionSound);
        }


        // Activar la linterna del jugador
        if (playerFlashlightObject != null)
        {
            playerFlashlightObject.SetActive(true);

            LampSystem lampSystem = playerFlashlightObject.GetComponent<LampSystem>();
            if (lampSystem != null)
            {
                // Configurar referencias (opcional si ya están en inspector)
                 try // Usar try-catch por si los nombres de campo cambian
                 {
                     var lampsystemType = lampSystem.GetType();
                     var lampEmptyField = lampsystemType.GetField("lampEmptyObj", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                     var lampFillField = lampsystemType.GetField("lampFillObj", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                     if (lampEmptyField != null && lampEmptyUI != null)
                         lampEmptyField.SetValue(lampSystem, lampEmptyUI);

                     if (lampFillField != null && lampFillUI != null)
                         lampFillField.SetValue(lampSystem, lampFillUI);
                 } catch (System.Exception ex) {
                     Debug.LogError($"Error setting LampSystem UI fields via reflection: {ex.Message}", this);
                 }

                lampSystem.SetEnergyUIVisible(true);
            }
        }

        // Activar directamente los objetos UI si tienes referencias (redundante si LampSystem lo hace)
        // if (lampEmptyUI != null) lampEmptyUI.SetActive(true);
        // if (lampFillUI != null) lampFillUI.SetActive(true);

        // Guardar estado en PlayerPrefs
        PlayerPrefs.SetInt(SaveSystem.LAMP_PICKED_UP_PREF_KEY, 1);
        PlayerPrefs.Save();

        // Notificar al sistema de misiones (asegúrate que es el correcto)
        if (questManager != null)
        {
            // Podrías necesitar diferenciar aquí si es el TutorialQuestManager o VillageQuestManager
            questManager.TriggerFlashlightPicked(); // Asumiendo TutorialQuestManager
            // O: VillageQuestManager.Instance?.TriggerEvent("FLASHLIGHT_PICKED_UP");
        }

        Debug.Log("Flashlight picked up");

        // Marcar como usado (si oneTimeUse es true en la base)
        hasBeenUsed = true;

        // Desactivar el objeto interactuable de la linterna
        gameObject.SetActive(false);
    }
}