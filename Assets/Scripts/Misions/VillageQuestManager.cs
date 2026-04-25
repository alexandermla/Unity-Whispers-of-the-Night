using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine.Localization; // <-- AÑADIR
using UnityEngine.Localization.Settings; // <-- AÑADIR
using UnityEngine.ResourceManagement.AsyncOperations; // <-- AÑADIR para AsyncOperationHandle

public class VillageQuestManager : MonoBehaviour
{
    [System.Serializable]
    public class VillageQuest
    {
        public string questID;
        // public string questText; // <-- ELIMINADO
        public LocalizedString questLocalizedText; // <-- NUEVO: Para UI normal
        public string completionTrigger;
        public bool isActive = false;
        public bool isCompleted = false;
        public bool autoActivate = false;
        public Color textColor = Color.white; // Se sigue usando para el color
        public string[] requiredEvents;
        public VillageQuest nextQuest;
        public bool isTutorialPopup = false;
        // [TextArea(5,10)] // El TextArea ya no aplica directamente aquí
        // public string tutorialPopupDescription; // <-- ELIMINADO
        public LocalizedString tutorialPopupLocalizedDescription; // <-- NUEVO: Para Popups
    }

    [Header("Quest Settings")]
    [SerializeField] private List<VillageQuest> availableQuests = new List<VillageQuest>();
    [SerializeField] private VillageQuest currentQuest;
    // Nombre de la tabla de strings (asegúrate que coincida con tu tabla)
    [SerializeField] private string questStringTable = "Tabla1";

    [Header("Normal Quest UI References")]
    [SerializeField] private GameObject questPanel;
    [SerializeField] private TextMeshProUGUI questTextUI;
    // [SerializeField] private float questDisplayDuration = 5f; // Ya no se usa

    [Header("Tutorial Pop-up UI")]
    [SerializeField] private GameObject tutorialPopupPanel;
    [SerializeField] private TextMeshProUGUI tutorialPopupText;

    [Header("Statue UI")]
    [SerializeField] private GameObject statueUIPanel;

    [Header("References")]
    [SerializeField] private FireflyGuide fireflyGuide;
    [SerializeField] private Transform enemySpawnPoint;
    [SerializeField] private Transform centralFountainTransform;
    [SerializeField] private GameObject centralSpotlight;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip questCompletedSound;
    [SerializeField] private AudioClip tutorialPopupSound;

    private Dictionary<string, bool> triggeredEvents = new Dictionary<string, bool>();
    private Coroutine normalQuestDisplayCoroutine; // Renombrado para claridad
    private Coroutine popupLoadCoroutine; // Para cargar texto del popup
    private bool isTutorialPopupActive = false;
    private InputSystem_Actions inputActions;

    public delegate void QuestStatusChangedHandler(string questId, bool isActive, bool isCompleted);
    public event QuestStatusChangedHandler OnQuestStatusChanged;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        if (fireflyGuide == null) fireflyGuide = Object.FindAnyObjectByType<FireflyGuide>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        if (questPanel != null) questPanel.SetActive(false);
        if (tutorialPopupPanel != null) tutorialPopupPanel.SetActive(false);
        else Debug.LogError("¡TutorialPopupPanel no asignado!", this);
        if (tutorialPopupText == null && tutorialPopupPanel != null) tutorialPopupText = tutorialPopupPanel.GetComponentInChildren<TextMeshProUGUI>();
        if (tutorialPopupText == null) Debug.LogError("¡tutorialPopupText no asignado!", this);

        if (statueUIPanel != null) statueUIPanel.SetActive(true);
        if (centralSpotlight != null) centralSpotlight.SetActive(false);

        StatueEvents.OnStatueCollected += OnStatueCollected;
        StatueEvents.OnAllStatuesCollected += OnAllStatuesCollected;
    }

    private void OnEnable() { inputActions?.Player.Enable(); }
    private void OnDisable() { inputActions?.Player.Disable(); }

    private void OnDestroy()
    {
        StatueEvents.OnStatueCollected -= OnStatueCollected;
        StatueEvents.OnAllStatuesCollected -= OnAllStatuesCollected;
        inputActions?.Dispose();
    }

    private void Start()
    {
        InitializeVillageQuests();
        ActivateAutoQuests();
    }

    private void Update()
    {
        // Cerrar popup con tecla Interact
        if (isTutorialPopupActive && inputActions.Player.Interact.WasPressedThisFrame())
        {
            HideTutorialPopup();
        }
    }

    // ==== Métodos Guardado/Carga (Sin cambios necesarios aquí si InitializeVillageQuests reconstruye los LocalizedStrings) ====
    public List<VillageQuest> GetAllQuests() { return availableQuests; }
    public VillageQuest GetCurrentQuest() { return currentQuest; }
    public List<string> GetAllTriggeredEvents() { return new List<string>(triggeredEvents.Keys); }
    public int GetCollectedStatuesCount() { return StatueEvents.GetCollectedCount(); }
    public void RestoreQuestStates(Dictionary<string, int> questStates, string currentQuestId)
    {
        if (questStates == null) return;
        VillageQuest questToDisplayOnLoad = null;
        foreach(var quest in availableQuests) {
             if(questStates.TryGetValue(quest.questID, out int state)) {
                 quest.isActive = (state == 1);
                 quest.isCompleted = (state == 2);
                 if(quest.questID == currentQuestId) {
                     currentQuest = quest;
                     // Marcar para mostrar si está activa y NO es popup
                     if(quest.isActive && !quest.isTutorialPopup) {
                         questToDisplayOnLoad = quest;
                     }
                 }
             } else { quest.isActive = false; quest.isCompleted = false; }
         }
         // Mostrar la misión activa al cargar (si aplica)
         if(questToDisplayOnLoad != null) {
             DisplayNormalQuestUI(questToDisplayOnLoad.questLocalizedText, questToDisplayOnLoad.textColor);
         }
         else if (questPanel != null) {
             questPanel.SetActive(false); // Asegurar que esté oculto si no hay misión normal activa
         }
    }
    public void RestoreTriggeredEvents(List<string> events) { triggeredEvents = events?.ToDictionary(e => e, e => true) ?? new Dictionary<string, bool>(); }
    public void RestoreStatuesState(int statuesCount) { /* No necesario, StatueEvents lo maneja */ }
    public void RefreshUI() {
         if (statueUIPanel != null) { bool shouldBeVisible = IsQuestActive("FIND_STATUES") || IsQuestCompleted("FIND_STATUES"); statueUIPanel.SetActive(shouldBeVisible); }
         if (centralSpotlight != null) { bool isFountainQuestRelevant = IsQuestActive("CENTRAL_SPOTLIGHT") || IsQuestCompleted("CENTRAL_SPOTLIGHT") || IsQuestActive("FIND_STATUES") || IsQuestCompleted("FIND_STATUES") || IsQuestActive("RETURN_TO_FOUNTAIN_WARNING") || IsQuestCompleted("RETURN_TO_FOUNTAIN_WARNING"); centralSpotlight.SetActive(isFountainQuestRelevant); }
    }
    // ==============================

    // --- MODIFICADO: Usar LocalizedString ---
    private void InitializeVillageQuests()
    {
        availableQuests = new List<VillageQuest>
        {
            // Tutorial Pop-ups
            new VillageQuest {
                questID = "LOCK_ON_TUTORIAL",
                // questText = "Lock on (Q)", // <-- Antiguo
                questLocalizedText = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_LOCK_ON_TITLE"}, // Título corto para UI normal si se necesitara
                completionTrigger = "ENEMY_LOCKED_ON", autoActivate = false, isTutorialPopup = true,
                // tutorialPopupDescription = "..." // <-- Antiguo
                tutorialPopupLocalizedDescription = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_LOCK_ON_DESC"}
            },
            new VillageQuest {
                questID = "AIM_TUTORIAL",
                // questText = "Aim (Right Click)", // <-- Antiguo
                questLocalizedText = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_AIM_TITLE"},
                completionTrigger = "PLAYER_AIMED", autoActivate = false, isTutorialPopup = true,
                // tutorialPopupDescription = "..." // <-- Antiguo
                tutorialPopupLocalizedDescription = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_AIM_DESC"}
            },
            new VillageQuest { // Misión normal, solo necesita texto normal
                questID = "FIREFLY_FUSION",
                // questText = "Firefly is merging...", // <-- Antiguo
                questLocalizedText = new LocalizedString { TableReference = questStringTable, TableEntryReference = "G080"},
                completionTrigger = "FIREFLY_FUSION_COMPLETE", autoActivate = false, isTutorialPopup = false
            },
             new VillageQuest {
                questID = "FLASHLIGHT_TUTORIAL",
                // questText = "Use Flashlight (Left Click)", // <-- Antiguo
                questLocalizedText = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_FLASHLIGHT_TITLE"},
                completionTrigger = "ENEMY_KILLED_BY_LIGHT", autoActivate = false, isTutorialPopup = true,
                // tutorialPopupDescription = "..." // <-- Antiguo
                tutorialPopupLocalizedDescription = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_FLASHLIGHT_DESC"}
             },
             new VillageQuest {
                questID = "LAMP_COOLDOWN_INFO",
                // questText = "Lamp energy info", // <-- Antiguo
                questLocalizedText = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_LAMP_INFO_TITLE"},
                completionTrigger = "LAMP_INFO_ACKNOWLEDGED", autoActivate = false, isTutorialPopup = true,
                // tutorialPopupDescription = "..." // <-- Antiguo
                tutorialPopupLocalizedDescription = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_LAMP_INFO_DESC"}
             },
             new VillageQuest {
                questID = "CROUCH_TUTORIAL",
                // questText = "Crouch (C)", // <-- Antiguo
                questLocalizedText = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_CROUCH_TITLE"},
                completionTrigger = "PLAYER_CROUCHED", autoActivate = false, isTutorialPopup = true,
                // tutorialPopupDescription = "..." // <-- Antiguo
                tutorialPopupLocalizedDescription = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_CROUCH_DESC"}
            },

            // Misiones Normales (Main quest content)
             new VillageQuest {
                questID = "CENTRAL_SPOTLIGHT",
                // questText = "Go to the central fountain", // <-- Antiguo
                questLocalizedText = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_GOTO_FOUNTAIN_TEXT"},
                completionTrigger = "CENTRAL_LIGHT_ACKNOWLEDGED", // Este trigger necesita ser llamado desde otro script (ej. un trigger collider en la fuente)
                autoActivate = false, // Se activa después del tutorial
                isTutorialPopup = false,
                textColor = new Color(1f, 0.9f, 0.5f)
            },
            new VillageQuest { // Esta misión parece informativa, ¿cómo se completa?
                questID = "HIDING_INFO",
                // questText = "Use the environment to hide", // <-- Antiguo
                questLocalizedText = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_HIDING_INFO_TEXT"},
                completionTrigger = "HIDING_INFO_ACKNOWLEDGED", // Necesita un trigger para completarse
                autoActivate = false, isTutorialPopup = false, textColor = Color.white
            },
            new VillageQuest {
                questID = "FIND_STATUES",
                // questText = "Find the four animal statues", // <-- Antiguo
                questLocalizedText = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_FIND_STATUES_TEXT"},
                completionTrigger = "ALL_STATUES_FOUND", // Este se dispara desde StatueEvents
                autoActivate = false, // Se activa después de ir a la fuente
                isTutorialPopup = false, textColor = new Color(1f, 0.8f, 0.4f),
                // Los requiredEvents siguen igual
                requiredEvents = new string[] { "STATUE_BEAR_COLLECTED", "STATUE_MOUSE_COLLECTED", "STATUE_DOG_COLLECTED", "STATUE_RACCOON_COLLECTED" }
            },
             new VillageQuest {
                questID = "RETURN_TO_FOUNTAIN_WARNING",
                // questText = "Return to the fountain... carefully!", // <-- Antiguo
                questLocalizedText = new LocalizedString { TableReference = questStringTable, TableEntryReference = "VILLAGE_RETURN_FOUNTAIN_TEXT"},
                completionTrigger = "FOUNTAIN_WARNING_ACKNOWLEDGED", // Necesita trigger
                autoActivate = false, isTutorialPopup = false, textColor = new Color(1f, 0.2f, 0.2f)
            }
        };
        LinkQuestsSequence(); // Enlazar las misiones sigue igual
    }
    // --------------------------------------------------

    private void ActivateAutoQuests() {
        foreach (var quest in availableQuests) {
            if (quest.autoActivate && !quest.isCompleted && !quest.isActive) {
                ActivateQuest(quest.questID);
                break;
            }
        }
    }

    private void LinkQuestsSequence() {
        // Tutorial (los IDs no cambian)
        SetNextQuest("LOCK_ON_TUTORIAL", "AIM_TUTORIAL");
        SetNextQuest("AIM_TUTORIAL", "FIREFLY_FUSION");
        SetNextQuest("FIREFLY_FUSION", "FLASHLIGHT_TUTORIAL");
        SetNextQuest("FLASHLIGHT_TUTORIAL", "LAMP_COOLDOWN_INFO");
        SetNextQuest("LAMP_COOLDOWN_INFO", "CENTRAL_SPOTLIGHT"); // La info de la lámpara lleva a ir a la fuente

        // Post-Tutorial
        SetNextQuest("CENTRAL_SPOTLIGHT", "FIND_STATUES");
        SetNextQuest("CROUCH_TUTORIAL", null); // Agacharse no linkea a nada auto
        SetNextQuest("FIND_STATUES", "RETURN_TO_FOUNTAIN_WARNING");
        SetNextQuest("RETURN_TO_FOUNTAIN_WARNING", null);
        SetNextQuest("HIDING_INFO", null);
    }

    private void SetNextQuest(string currentQuestId, string nextQuestId) {
        VillageQuest current = availableQuests.Find(q => q.questID == currentQuestId);
        VillageQuest next = string.IsNullOrEmpty(nextQuestId) ? null : availableQuests.Find(q => q.questID == nextQuestId);
        if (current != null) { current.nextQuest = next; }
        else { Debug.LogWarning($"LinkQuestsSequence: Misión actual '{currentQuestId}' no encontrada."); }
    }

    // --- MODIFICADO: Usa los campos LocalizedString ---
    public void ActivateQuest(string questID) {
        VillageQuest newQuest = availableQuests.Find(q => q.questID == questID);
        if (newQuest == null) { Debug.LogWarning($"Quest '{questID}' no encontrada."); return; }
        // Salir si está completa, o si es popup y ya hay uno activo (a menos que sea el mismo ID, poco probable)
        if (newQuest.isCompleted || (newQuest.isTutorialPopup && isTutorialPopupActive /*&& currentQuest?.questID != questID*/ )) return;

        // Ocultar UI normal anterior SI la nueva NO es popup Y había una misión normal activa
        if (!newQuest.isTutorialPopup && currentQuest != null && !currentQuest.isTutorialPopup && questPanel != null) {
            questPanel.SetActive(false);
            if(normalQuestDisplayCoroutine != null) {
                StopCoroutine(normalQuestDisplayCoroutine);
                normalQuestDisplayCoroutine = null;
            }
        }

        if (currentQuest != null) currentQuest.isActive = false;
        newQuest.isActive = true;
        currentQuest = newQuest; // Establecer como actual ANTES de mostrar UI

        Debug.Log($"Attempting to Activate Quest: {questID} (IsPopup: {newQuest.isTutorialPopup})");

        if (newQuest.isTutorialPopup) {
            ShowTutorialPopup(newQuest); // Mostrar popup
        } else {
            DisplayNormalQuestUI(newQuest.questLocalizedText, newQuest.textColor); // Mostrar UI normal
        }

        HandleSpecificQuestActivation(questID);
        OnQuestStatusChanged?.Invoke(questID, true, false);

        // --- Lógica específica movida aquí ---
        if (questID == "FIREFLY_FUSION")
        {
            StartCoroutine(TriggerFireflyFusionAfterDelay(0.5f)); // Iniciar fusión poco después de activar la misión
        }
    }
    // -----------------------------------------------

    // --- MODIFICADO: Carga texto de LocalizedString asíncronamente ---
    private void ShowTutorialPopup(VillageQuest quest) {
        if (tutorialPopupPanel == null || tutorialPopupText == null || quest == null || quest.tutorialPopupLocalizedDescription.IsEmpty)
        {
            Debug.LogError("No se puede mostrar popup: Referencias UI nulas o descripción localizada no configurada.");
            // Como fallback, intenta mostrar el texto normal si existe
            if(quest != null && !quest.questLocalizedText.IsEmpty)
            {
                DisplayNormalQuestUI(quest.questLocalizedText, quest.textColor);
            }
            return;
        }

        // Detener carga anterior si la hubiera
        if (popupLoadCoroutine != null) StopCoroutine(popupLoadCoroutine);

        // Iniciar carga asíncrona
        popupLoadCoroutine = StartCoroutine(LoadAndShowPopupDescription(quest.tutorialPopupLocalizedDescription));
    }

    private IEnumerator LoadAndShowPopupDescription(LocalizedString localizedDesc)
    {
        tutorialPopupText.text = "Loading..."; // Texto temporal
        tutorialPopupPanel.SetActive(true); // Activar panel antes de cargar

        AsyncOperationHandle<string> handle = localizedDesc.GetLocalizedStringAsync();
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            tutorialPopupText.text = handle.Result; // Mostrar texto cargado
            Time.timeScale = 0f; // Pausar DESPUÉS de mostrar el texto
            isTutorialPopupActive = true;
            if (audioSource != null && tutorialPopupSound != null) audioSource.PlayOneShot(tutorialPopupSound);
        }
        else
        {
            Debug.LogError($"Error al cargar descripción del popup: {handle.OperationException}");
            tutorialPopupText.text = "[Error loading description]";
            // Considera no pausar si hay error
             Time.timeScale = 0f;
             isTutorialPopupActive = true;
        }
        popupLoadCoroutine = null;
    }
    // -------------------------------------------------------------

    private void HideTutorialPopup() {
        if (!isTutorialPopupActive || tutorialPopupPanel == null) return;

        isTutorialPopupActive = false;
        tutorialPopupPanel.SetActive(false);
        Time.timeScale = 1f; // Reanudar juego

        // Detener carga si aún estaba en proceso
        if (popupLoadCoroutine != null) {
            StopCoroutine(popupLoadCoroutine);
            popupLoadCoroutine = null;
        }

        // **YA NO SE COMPLETA LA MISIÓN AQUÍ**
        Debug.Log($"Popup closed for quest: {currentQuest?.questID ?? "None"}. Waiting for completion trigger.");
    }

    private void HandleSpecificQuestActivation(string questID) {
        // La lógica aquí no cambia
        switch (questID) {
            case "LOCK_ON_TUTORIAL": DirectFireflyToSleepingEnemy(); break;
            case "CENTRAL_SPOTLIGHT":
                if (centralSpotlight != null) centralSpotlight.SetActive(true);
                break;
            case "FIND_STATUES": if (statueUIPanel != null && !statueUIPanel.activeSelf) statueUIPanel.SetActive(true); break;
        }
    }

    // Método sin cambios
    public void TriggerEvent(string eventName) {
        if (!triggeredEvents.ContainsKey(eventName)) {
            triggeredEvents[eventName] = true;
            Debug.Log($"Event Triggered: {eventName}");
            CheckEventCompletion(eventName);
            HandleSpecificEvent(eventName);
        }
    }

    // Método sin cambios lógicos, solo verifica triggers o eventos requeridos
    private void CheckEventCompletion(string eventName) {
        // Solo procesar si hay una misión actual activa
        if (currentQuest != null && currentQuest.isActive) {

            // **SE ELIMINA EL IGNORAR POPUPS**
            // Ahora se comprueba si el evento coincide con el trigger de la misión activa (sea popup o no)

            // Comprobar si el trigger de la misión activa coincide con el evento recibido
            if (currentQuest.completionTrigger == eventName)
            {
                Debug.Log($"Quest '{currentQuest.questID}' (IsPopup: {currentQuest.isTutorialPopup}) completed by direct trigger: {eventName}");
                CompleteCurrentQuest(); // Llama a completar la misión actual
                return; // Salir después de completar
            }

            // Comprobar eventos requeridos (para misiones normales como FIND_STATUES)
            if (!currentQuest.isTutorialPopup && currentQuest.requiredEvents != null && currentQuest.requiredEvents.Length > 0) {
                bool allTriggered = currentQuest.requiredEvents.All(reqEvent => triggeredEvents.ContainsKey(reqEvent) && triggeredEvents[reqEvent]);
                if (allTriggered)
                {
                    Debug.Log($"Normal Quest '{currentQuest.questID}' completed by required events.");
                    CompleteCurrentQuest();
                }
            }
        }
    }

    // Método sin cambios
    private void HandleSpecificEvent(string eventName) {
        // Solo iniciar fusión si se completó el AIM_TUTORIAL (evento PLAYER_AIMED lo completa)
    if (eventName == "PLAYER_AIMED") // Este evento completa AIM_TUTORIAL
    {
        // El flujo normal en CompleteCurrentQuest activará FIREFLY_FUSION.
        // Podríamos iniciar la corutina aquí o al activar FIREFLY_FUSION.
        // Hagámoslo al activar FIREFLY_FUSION para mayor claridad.
        // StartCoroutine(TriggerFireflyFusionAfterDelay(1.0f)); // <-- MOVER esta lógica
    }
    else if (eventName == "ALL_STATUES_FOUND")
    {
        // ALL_STATUES_FOUND completa FIND_STATUES.
        // La activación de RETURN_TO_FOUNTAIN_WARNING ya se maneja en LinkQuestsSequence
        // al completar FIND_STATUES. No se necesita corutina aquí.
        // StartCoroutine(ActivateWarningQuestAfterDelay(1.0f)); // <-- REDUNDANTE si LinkQuestsSequence funciona
        StartCoroutine(TriggerFountainSequence()); // Mantener secuencia de fuente
    }
        else if (eventName == "ENEMY_LOCKED_ON" && currentQuest?.questID == "LOCK_ON_TUTORIAL") TriggerEvent(currentQuest.completionTrigger); // Completar lock-on al detectar el evento
        else if (eventName == "ENEMY_KILLED_BY_LIGHT" && currentQuest?.questID == "FLASHLIGHT_TUTORIAL") TriggerEvent(currentQuest.completionTrigger); // Completar tutorial linterna al matar enemigo
    }

    // Métodos de Delay y Lógica Específica (sin cambios)
    private IEnumerator ActivateWarningQuestAfterDelay(float delay) { yield return new WaitForSeconds(delay); ActivateQuest("RETURN_TO_FOUNTAIN_WARNING"); }
    private IEnumerator TriggerFireflyFusionAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay); // Esperar un poco
        if (fireflyGuide != null && fireflyGuide.gameObject.activeSelf)
        {
            if (SceneManager.GetActiveScene().name == "mainSceneQuest")
            {
                Debug.Log("Calling fireflyGuide.FuseWithLamp()");
                fireflyGuide.FuseWithLamp(); // Este método ahora debería disparar el evento "FIREFLY_FUSION_COMPLETE" al finalizar su corutina
            }
        }
        else
        {
            Debug.LogWarning("Firefly not found or inactive, completing fusion event immediately.");
            TriggerEvent("FIREFLY_FUSION_COMPLETE"); // Completar inmediatamente si no hay luciérnaga
        }
    }
    private void StartFireflyFusion() { if (fireflyGuide != null && fireflyGuide.gameObject.activeSelf) { if (SceneManager.GetActiveScene().name == "mainSceneQuest") { fireflyGuide.FuseWithLamp(); } /*StartCoroutine(CompleteFusionAfterDelay(3.0f));*/ } else { TriggerEvent("FIREFLY_FUSION_COMPLETE"); Debug.LogWarning("Firefly not found or inactive, completing fusion event immediately."); } }
    // private IEnumerator CompleteFusionAfterDelay(float delay) { yield return new WaitForSeconds(delay); TriggerEvent("FIREFLY_FUSION_COMPLETE"); RechargePlayerLamp(); } // La fusión ahora la completa el evento
    private void RechargePlayerLamp() { LampSystem lamp = Object.FindAnyObjectByType<LampSystem>(); if (lamp != null) lamp.RechargeEnergy(); }
    private IEnumerator TriggerFountainSequence() { yield return new WaitForSeconds(2.0f); Debug.Log("Fountain sequence triggered."); }

    // --- MODIFICADO: Llama a activar la siguiente misión ---
    private void CompleteCurrentQuest() {
        if (currentQuest == null || !currentQuest.isActive || currentQuest.isCompleted)
        {
            Debug.LogWarning("CompleteCurrentQuest llamado sin una misión válida activa.");
            return; // Salir si no hay misión activa válida
        }


        string completedQuestID = currentQuest.questID;
        bool wasPopup = currentQuest.isTutorialPopup;
        VillageQuest nextQuestToActivate = currentQuest.nextQuest; // Guardar referencia a la siguiente ANTES de limpiar

        Debug.Log($"Attempting to Complete Quest: {completedQuestID}");

        currentQuest.isCompleted = true;
        currentQuest.isActive = false;
        OnQuestStatusChanged?.Invoke(completedQuestID, false, true);

        // Ocultar UI normal si la misión completada era normal
        if (!wasPopup && questPanel != null) {
            questPanel.SetActive(false);
            if (normalQuestDisplayCoroutine != null) {
                StopCoroutine(normalQuestDisplayCoroutine);
                normalQuestDisplayCoroutine = null;
            }
        }

        if (audioSource != null && questCompletedSound != null) audioSource.PlayOneShot(questCompletedSound);

        currentQuest = null; // Limpiar misión actual DESPUÉS de procesarla

        // Activar la siguiente misión enlazada
        if (nextQuestToActivate != null) {
            Debug.Log($"Completing {completedQuestID}. Activating next linked quest: {nextQuestToActivate.questID}");
            // Pequeña pausa opcional antes de activar la siguiente para que el jugador respire
            // StartCoroutine(ActivateNextQuestAfterDelay(nextQuestToActivate.questID, 0.2f));
            ActivateQuest(nextQuestToActivate.questID);
        } else {
            Debug.Log($"Quest {completedQuestID} completed. No next linked quest found.");
            // Asegurarse de ocultar UI normal si no hay siguiente misión
            if (!wasPopup && questPanel != null) questPanel.SetActive(false);
        }
    }
    // ------------------------------------------------------

    // --- MODIFICADO: Mostrar UI normal (asíncrono) ---
    private void DisplayNormalQuestUI(LocalizedString localizedString, Color color) {
        if (questPanel == null || questTextUI == null || localizedString == null || localizedString.IsEmpty)
        {
            Debug.LogWarning("Cannot display normal quest UI: References missing or LocalizedString empty.");
            return;
        }

        // Detener corutina anterior si la hubiera
        if (normalQuestDisplayCoroutine != null) {
            StopCoroutine(normalQuestDisplayCoroutine);
        }

        // Iniciar corutina para cargar y mostrar
        normalQuestDisplayCoroutine = StartCoroutine(ShowNormalQuestUICoroutine(localizedString, color));
    }

    private IEnumerator ShowNormalQuestUICoroutine(LocalizedString localizedString, Color color) {
        questPanel.SetActive(true); // Activar panel para mostrar "Loading..."
        questTextUI.text = "Loading..."; // Texto temporal
        questTextUI.color = color; // Aplicar color

        CanvasGroup cg = questPanel.GetComponent<CanvasGroup>() ?? questPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0; // Empezar transparente

        // Cargar el texto
        AsyncOperationHandle<string> handle = localizedString.GetLocalizedStringAsync();
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            questTextUI.text = handle.Result; // Mostrar texto cargado
        }
        else
        {
            Debug.LogError($"Failed to load normal quest text: {handle.OperationException}");
            questTextUI.text = "[Error Loading Quest]"; // Fallback
        }

        // Hacer Fade-In del panel AHORA que el texto está listo
        float duration = 0.5f;
        float elapsed = 0;
        while (elapsed < duration) {
            cg.alpha = Mathf.Lerp(0, 1, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        cg.alpha = 1;
        normalQuestDisplayCoroutine = null; // Limpiar corutina

        // IMPORTANTE: El panel de misión normal ahora NO SE OCULTA automáticamente.
        // Se ocultará cuando se active OTRA misión normal, o cuando no haya ninguna activa.
    }
    // ---------------------------------------------------


    // Método sin cambios
    private void DirectFireflyToSleepingEnemy() { if (fireflyGuide != null && enemySpawnPoint != null && SceneManager.GetActiveScene().name == "mainSceneQuest" && fireflyGuide.gameObject.activeSelf) { fireflyGuide.GoToTarget(enemySpawnPoint); } }

    // Métodos Públicos (sin cambios)
    public bool IsQuestActive(string questID) { VillageQuest quest = availableQuests.Find(q => q.questID == questID); return quest != null && quest.isActive; }
    public bool IsQuestCompleted(string questID) { VillageQuest quest = availableQuests.Find(q => q.questID == questID); return quest != null && quest.isCompleted; }
    public bool HasEventTriggered(string eventName) { return triggeredEvents.ContainsKey(eventName) && triggeredEvents[eventName]; }

    // Eventos de Input (sin cambios)
    public void OnPlayerLockOn() { if(currentQuest?.questID == "LOCK_ON_TUTORIAL") TriggerEvent("ENEMY_LOCKED_ON"); } // Completar solo si es la misión activa
    public void OnPlayerAim() { if(currentQuest?.questID == "AIM_TUTORIAL") TriggerEvent("PLAYER_AIMED"); } // Completar solo si es la misión activa
    public void OnPlayerCrouch() { if(currentQuest?.questID == "CROUCH_TUTORIAL") TriggerEvent("PLAYER_CROUCHED"); } // Completar solo si es la misión activa

    // Eventos de Estatuas (sin cambios)
    private void OnStatueCollected(string statueId, Vector3 position) {
        TriggerEvent($"STATUE_{statueId}_COLLECTED"); // Disparar evento específico
        if (StatueEvents.GetCollectedCount() >= 1 && !IsQuestActive("FIND_STATUES") && !IsQuestCompleted("FIND_STATUES") ) { ActivateQuest("FIND_STATUES"); }
     }
    private void OnAllStatuesCollected() { TriggerEvent("ALL_STATUES_FOUND"); /* StartCoroutine(TriggerFountainSequence()); // La secuencia de la fuente se dispara desde otro lado */ }

} 