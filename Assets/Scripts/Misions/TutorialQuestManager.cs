using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings; // Para acceder a la configuración de localización
using UnityEngine.Localization;
using UnityEngine.ResourceManagement.AsyncOperations;
public class TutorialQuestManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialQuest
    {
        public string questID;
        public LocalizedString questText;
        public string completionTrigger;
        public bool isCompleted = false;
        public bool isActive = false;
        public bool autoActivate = false;
    }

    [Header("Quest Settings")]
    [SerializeField] private List<TutorialQuest> tutorialQuests = new List<TutorialQuest>();
    [SerializeField] private int currentQuestIndex = -1;

    [Header("UI References")]
    [SerializeField] private GameObject questPanel;
    [SerializeField] private TextMeshProUGUI questText;
    
    [SerializeField] private GameObject questCompletedPanel;
    [SerializeField] private float questDisplayDuration = 5f;
    [SerializeField] private float completionDisplayDuration = 2f;

    [Header("Special Quest UI")]
    [SerializeField] private GameObject runTextPanel;
    [SerializeField] private float runTextDuration = 3f;

    // Referencias a otros sistemas
    [SerializeField] private PlayerController playerController;
    [SerializeField] private FireflyGuide fireflyGuide;
    [SerializeField] private GameObject doorSequenceController;

    private Coroutine questDisplayCoroutine;
    private Coroutine completionDisplayCoroutine;

    private void Start()
    {
        // Inicializar las misiones del tutorial
        InitializeQuests();

        // Activar la primera misión si es automática
        ActivateFirstAutoQuest();
    }

    private void InitializeQuests()
    {
        // Define tutorial quests using Keys instead of direct text
        tutorialQuests = new List<TutorialQuest>
        {
            new TutorialQuest
            {
                questID = "TUT_MOVE",
                // questText = "Use <color=#FFD700>WASD</color> to move", // <-- LÍNEA ANTIGUA
                questText = new LocalizedString { TableReference = "Tabla1", TableEntryReference = "TUT_MOVE_TEXT"},
                completionTrigger = "TRIGGER_MOVED",
                autoActivate = true
            },

            new TutorialQuest
            {
                questID = "TUT_JUMP",
                // questText = "Press <color=#FFD700>SPACE</color> to jump", // <-- LÍNEA ANTIGUA
                questText = new LocalizedString { TableReference = "Tabla1", TableEntryReference = "TUT_JUMP_TEXT"},
                completionTrigger = "TRIGGER_JUMPED"
            },

            new TutorialQuest
            {
                questID = "TUT_INTERACT",
                questText = new LocalizedString { TableReference = "Tabla1", TableEntryReference = "G011"},
                completionTrigger = "TRIGGER_FLASHLIGHT_PICKED"
            },

            new TutorialQuest
            {
                questID = "TUT_RUN_AWAY",
                questText = new LocalizedString { TableReference = "Tabla1", TableEntryReference = "TUT_RUN_AWAY_TEXT"},
                completionTrigger = "TRIGGER_REACHED_EXIT_DOOR"
            }
        };
    }

    private void ActivateFirstAutoQuest()
    {
        foreach (var quest in tutorialQuests)
        {
            if (quest.autoActivate && !quest.isCompleted)
            {
                ActivateQuest(quest.questID);
                break;
            }
        }
    }

    public void ActivateQuest(string questID)
    {
        for (int i = 0; i < tutorialQuests.Count; i++)
        {
            if (tutorialQuests[i].questID == questID && !tutorialQuests[i].isCompleted)
            {
                // Desactivar la misión actual si hay alguna
                if (currentQuestIndex >= 0 && currentQuestIndex < tutorialQuests.Count)
                {
                    tutorialQuests[currentQuestIndex].isActive = false;
                }

                // Activar la nueva misión
                tutorialQuests[i].isActive = true;
                currentQuestIndex = i;

                // Mostrar el panel de misión
                DisplayQuestUI(tutorialQuests[i].questText); // Cambiado de questText a questTextKey

                // Si es la misión de correr, mostrar el texto especial
                if (questID == "TUT_RUN_AWAY")
                {
                    ShowRunText();
                }

                return;
            }
        }
    }

    public void CompleteQuest(string triggerID)
    {
        // Verificar si el trigger coincide con alguna misión activa
        for (int i = 0; i < tutorialQuests.Count; i++)
        {
            if (tutorialQuests[i].isActive && tutorialQuests[i].completionTrigger == triggerID)
            {
                // Marcar como completada
                tutorialQuests[i].isCompleted = true; // Cambiado de false a true
                tutorialQuests[i].isActive = false;

                Debug.Log($"Quest completed: {tutorialQuests[i].questID}");

                // Mostrar UI de misión completada
                if (completionDisplayCoroutine != null)
                {
                    StopCoroutine(completionDisplayCoroutine);
                }
                completionDisplayCoroutine = StartCoroutine(ShowCompletionUI());

                // Activar la siguiente misión automáticamente
                ActivateNextQuest();

                // Acciones específicas según la misión completada
                HandleSpecificQuestCompletion(tutorialQuests[i].questID);

                return;
            }
        }
    }

    private void ActivateNextQuest()
    {
        // Solo activamos la siguiente misión si no es la de saltar (esta se activa por trigger)
        if (currentQuestIndex + 1 < tutorialQuests.Count)
        {
            int nextIndex = currentQuestIndex + 1;
            
            // Si la siguiente misión es la de saltar y no estamos en el trigger, no la activamos
            if (tutorialQuests[nextIndex].questID == "TUT_JUMP")
            {
                // No hacer nada, esta misión se activará por trigger
                Debug.Log("Next quest is jump mission, waiting for trigger");
                currentQuestIndex = -1; // Reseteamos para evitar problemas
            }
            else if (tutorialQuests[nextIndex].questID == "TUT_RUN_AWAY")
            {
                Debug.Log("Next quest is RUN_AWAY, NOT activating automatically");
                currentQuestIndex = -1; // Resetear para evitar problemas
            }
            else
            {
                currentQuestIndex = -1; // Resetear para evitar problemas
                ActivateQuest(tutorialQuests[nextIndex].questID);
            }
        }
    }

    private void HandleSpecificQuestCompletion(string questID)
    {
        switch (questID)
        {
            case "TUT_MOVE":
                // La primera misión se completa automáticamente
                break;

            case "TUT_JUMP":
                // Activar la misión de interacción con la linterna
                break;

            case "TUT_INTERACT":
                // Guardar que se ha recogido la linterna
                PlayerPrefs.SetInt("HasLampBeenPickedUp", 1);
                PlayerPrefs.Save();
                
                // Indicar a la luciérnaga que vaya a la puerta
                if (fireflyGuide != null)
                {
                    fireflyGuide.GoToFirstDoor();
                }
                break;

            case "TUT_RUN_AWAY":
                // Misión final completada, cargar la siguiente escena o mostrar final
                Debug.Log("Tutorial completado, jugador escapó exitosamente");
                break;
        }
    }

    // Métodos para actualizar la UI
    private void DisplayQuestUI(LocalizedString localizedQuestText)
    {
        if (questPanel == null || questText == null) return;

        if (questDisplayCoroutine != null)
        {
            StopCoroutine(questDisplayCoroutine);
        }
        // Pasa el LocalizedString a la corutina
        questDisplayCoroutine = StartCoroutine(ShowQuestUI(localizedQuestText));
    }

    private IEnumerator ShowQuestUI(LocalizedString localizedString)
    {
        if (questPanel == null || questText == null) yield break;

        // Iniciar carga asíncrona
        AsyncOperationHandle<string> handle = localizedString.GetLocalizedStringAsync();

        // Esperar a que la carga termine
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            questText.text = handle.Result; // Asigna el texto cargado
            questPanel.SetActive(true);

            yield return new WaitForSeconds(questDisplayDuration);

            questPanel.SetActive(false);
        }
        else
        {
            Debug.LogError($"Failed to load localized string for key: {localizedString.TableEntryReference}. Error: {handle.OperationException}");
            questText.text = $"[{localizedString.TableEntryReference}]"; // Mostrar clave como fallback
            questPanel.SetActive(true);
            yield return new WaitForSeconds(questDisplayDuration);
            questPanel.SetActive(false);
        }

        // Liberar el handle (importante)
        // Addressables.Release(handle); // Descomenta si usas Addressables directamente
    }

    private IEnumerator ShowCompletionUI()
    {
        if (questCompletedPanel != null)
        {
            questCompletedPanel.SetActive(true);
            Debug.Log("Showing quest completion panel");
            
            yield return new WaitForSeconds(completionDisplayDuration);
            
            questCompletedPanel.SetActive(false);
            Debug.Log("Hiding quest completion panel");
        }
        else
        {
            Debug.LogError("Quest completion panel is null!");
            yield return null;
        }
    }

    // Obtener el índice de la misión actual
    public int GetCurrentQuestIndex()
    {
        return currentQuestIndex;
    }

    // Verificar si una misión está completada
    public bool IsQuestCompleted(string questID)
    {
        foreach (var quest in tutorialQuests)
        {
            if (quest.questID == questID)
            {
                return quest.isCompleted;
            }
        }
        return false;
    }

    // Marcar una misión como completada (para restaurar el estado)
    public void SetQuestCompleted(string questID)
    {
        for (int i = 0; i < tutorialQuests.Count; i++)
        {
            if (tutorialQuests[i].questID == questID)
            {
                tutorialQuests[i].isCompleted = true;
                tutorialQuests[i].isActive = false;
            }
        }
    }

    // Restaurar el índice de misión actual
    public void RestoreQuestIndex(int index)
    {
        if (index >= 0 && index < tutorialQuests.Count)
        {
            currentQuestIndex = index;
            tutorialQuests[index].isActive = true;
        }
    }
    private void ShowRunText()
    {
        StartCoroutine(DisplayRunText());
    }

    private IEnumerator DisplayRunText()
    {
        if (runTextPanel != null)
        {
            runTextPanel.SetActive(true);
            
            // Find text component
            TextMeshProUGUI runText = runTextPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (runText != null)
            {
                // Starting animation - text grows
                float startTime = Time.time;
                float animDuration = 0.5f;
                Vector3 originalScale = runText.transform.localScale;
                
                while (Time.time - startTime < animDuration)
                {
                    float t = (Time.time - startTime) / animDuration;
                    float scale = Mathf.Lerp(0.5f, 1.2f, t);
                    runText.transform.localScale = originalScale * scale;
                    yield return null;
                }
                
                // Pulse animation for the remaining time
                startTime = Time.time;
                float remainingTime = runTextDuration - animDuration;
                
                while (Time.time - startTime < remainingTime)
                {
                    float pulse = 1.0f + 0.2f * Mathf.Sin(Time.time * 5.0f);
                    runText.transform.localScale = originalScale * pulse;
                    yield return null;
                }
                
                // Reset scale
                runText.transform.localScale = originalScale;
            }
            
            runTextPanel.SetActive(false);
        }
    }

    // Métodos públicos para activar mediante eventos
    public void TriggerPlayerMoved()
    {
        if (playerController != null && playerController.hasMovedWithWASD)
        {
            CompleteQuest("TRIGGER_MOVED");
        }
    }

    public void TriggerPlayerJumped()
    {
        CompleteQuest("TRIGGER_JUMPED");
    }

    public void TriggerFlashlightPicked()
    {
        CompleteQuest("TRIGGER_FLASHLIGHT_PICKED");
    }

    public void TriggerRunQuest()
    {
        ActivateQuest("TUT_RUN_AWAY");
    }

    public void TriggerReachedExit()
    {
        CompleteQuest("TRIGGER_REACHED_EXIT_DOOR");
    }
}