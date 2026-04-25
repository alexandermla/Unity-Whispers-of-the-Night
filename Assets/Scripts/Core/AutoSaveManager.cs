using UnityEngine;
using System.Collections;
using System; // Necesario para Action

public class AutoSaveManager : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private bool enableAutoSave = true; // Habilitar/deshabilitar guardado automático por evento
    [SerializeField] private float saveDelay = 0.5f; // Retraso corto después del evento
    [SerializeField] private bool showDebugMessages = true;
    [SerializeField] private float minTimeBetweenSaves = 10f; // Evitar guardados demasiado seguidos

    [Header("UI Feedback (Opcional)")]
    [SerializeField] private GameObject savingIndicator;
    [SerializeField] private float indicatorDuration = 1.5f;

    private static AutoSaveManager _instance;
    private float lastSaveTime = -100f; // Para permitir el primer guardado

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LogMessage("AutoSaveManager (Event-Based) inicializado");

            // Suscribirse a eventos de estatuas SI el guardado automático está habilitado
            if (enableAutoSave)
            {
                StatueEvents.OnStatueCollected += HandleStatueCollected;
                LogMessage("Suscrito a OnStatueCollected de StatueEvents.");

                 // Opcional: Suscribirse a otros eventos si es necesario
                 // VillageQuestManager questManager = FindObjectOfType<VillageQuestManager>();
                 // if (questManager != null) questManager.OnQuestStatusChanged += HandleQuestStatusChanged;
            }
             else
             {
                 LogMessage("Guardado automático por eventos desactivado.");
             }
        }
        else
        {
            Destroy(gameObject);
        }

        if (savingIndicator != null) savingIndicator.SetActive(false);
    }

     private void OnDestroy()
    {
        // Desuscribirse siempre, incluso si estaba deshabilitado al inicio
        StatueEvents.OnStatueCollected -= HandleStatueCollected;
         // VillageQuestManager questManager = FindObjectOfType<VillageQuestManager>();
         // if (questManager != null) questManager.OnQuestStatusChanged -= HandleQuestStatusChanged;
    }


    // --- Manejador para el evento de recolección de estatua ---
    private void HandleStatueCollected(string statueId, Vector3 position)
    {
        // Solo guardar si enableAutoSave es true
        if (!enableAutoSave) return;

        LogMessage($"Evento recibido: Estatua '{statueId}' recolectada.");
        // Llamar al guardado con un retraso y descripción del evento
        SaveAfterImportantEvent($"Statue '{statueId}' collected");
    }

    // --- Opcional: Manejador para el evento de cambio de estado de misión ---
    /*
    private void HandleQuestStatusChanged(string questId, bool isActive, bool isCompleted)
    {
        if (!enableAutoSave) return;
        // Comprobar si la misión completada es la de fusión de luciérnaga
        if (questId == "FIREFLY_FUSION" && isCompleted) // Asegúrate que el ID sea correcto
        {
            LogMessage($"Evento recibido: Misión '{questId}' completada.");
            SaveAfterImportantEvent("Firefly fusion completed");
        }
    }
    */

    // Método para guardar después de eventos importantes (con chequeo de tiempo mínimo)
    public void SaveAfterImportantEvent(string eventDescription)
    {
        // Solo guardar si enableAutoSave es true y estamos en la escena correcta
        if (!enableAutoSave || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "mainSceneQuest")
        {
             if(enableAutoSave) LogMessage($"Guardado omitido ({eventDescription}): Escena incorrecta.");
             return;
        }

        // Verificar tiempo mínimo desde el último guardado
        if (Time.time < lastSaveTime + minTimeBetweenSaves)
        {
            LogMessage($"Guardado omitido ({eventDescription}): Demasiado pronto desde el último guardado.");
            return;
        }

        if (_instance != null)
        {
            LogMessage($"Guardando después de evento importante: {eventDescription}");
            // Usar la corutina para aplicar el retraso y opcionalmente mostrar indicador
            _instance.StartCoroutine(_instance.SaveGameWithDelay(true, eventDescription));
        }
    }

    private IEnumerator SaveGameWithDelay(bool showIndicator = true, string reason = "Evento importante")
    {
        yield return new WaitForSeconds(saveDelay);

        // Guardar el estado del juego usando SaveSystem.Instance
        SaveGame(reason);

        // Mostrar indicador si existe y se solicita
        if (savingIndicator != null && showIndicator)
        {
            if (saveIndicatorCoroutine != null) StopCoroutine(saveIndicatorCoroutine);
            saveIndicatorCoroutine = StartCoroutine(ShowSaveIndicatorTemporarily());
        }
    }

    private void SaveGame(string reason)
    {
        // Verificar si SaveSystem existe
        if (SaveSystem.Instance != null)
        {
             // Comprobar si estamos en la escena permitida ANTES de guardar
             if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "mainSceneQuest")
             {
                 LogMessage($"Guardando estado del juego (Razón: {reason})...");
                 SaveSystem.Instance.SaveGame(); // Llamar al SaveSystem simplificado
                 lastSaveTime = Time.time; // Actualizar tiempo del último guardado
                 LogMessage("Estado guardado correctamente.");
             } else {
                 LogMessage($"Guardado omitido ({reason}): No estamos en 'mainSceneQuest'.");
             }

        }
        else
        {
            LogMessage("Error: No se encontró SaveSystem.Instance. No se puede guardar.");
        }
    }


    // Método estático/público para guardar manualmente si aún se necesita
    public void SaveGameManually(string reason = "Guardado manual")
    {
         // Comprobar si estamos en una escena donde se permite guardar
         if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "mainSceneQuest") {
             LogMessage($"Guardado manual omitido ({reason}): Escena incorrecta.");
             return;
         }

         // Verificar tiempo mínimo
         if (Time.time < lastSaveTime + minTimeBetweenSaves) {
             LogMessage($"Guardado manual omitido ({reason}): Demasiado pronto.");
             return;
         }


        if (_instance != null)
        {
            _instance.LogMessage($"Solicitud de guardado manual: {reason}");
            _instance.StartCoroutine(_instance.SaveGameWithDelay(true, reason));
        }
    }


    // Corutina para mostrar/ocultar indicador de guardado (sin cambios)
    private Coroutine saveIndicatorCoroutine;
    private IEnumerator ShowSaveIndicatorTemporarily()
    {
        if (savingIndicator == null) yield break;

        savingIndicator.SetActive(true);
        CanvasGroup canvasGroup = savingIndicator.GetComponent<CanvasGroup>() ?? savingIndicator.AddComponent<CanvasGroup>();

        // Fade In
        float elapsed = 0f; float fadeDuration = 0.3f;
        while (elapsed < fadeDuration) { canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration); elapsed += Time.unscaledDeltaTime; yield return null; }
        canvasGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(indicatorDuration);

        // Fade Out
        elapsed = 0f;
        while (elapsed < fadeDuration) { canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration); elapsed += Time.unscaledDeltaTime; yield return null; }
        canvasGroup.alpha = 0f;

        savingIndicator.SetActive(false);
        saveIndicatorCoroutine = null;
    }


    private void LogMessage(string message)
    {
        if (showDebugMessages) Debug.Log($"[AutoSaveManager] {message}");
    }
}