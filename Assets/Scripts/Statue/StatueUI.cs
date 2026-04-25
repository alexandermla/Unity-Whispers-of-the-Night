using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Necesario si usas TextMeshPro para el feedback

public class StatueUI : MonoBehaviour
{
    [System.Serializable]
    public class StatueUISlot
    {
        public string statueId; // "BEAR", "MOUSE", "DOG", "RACCOON"

        [Header("Filled Mode Settings")]
        public Image fillImage;
        public float fillDuration = 1.0f;
        public Color normalColor = Color.gray;
        public Color illuminatedColor = Color.yellow;
        public GameObject highlightEffect; // Efecto visual opcional

        [HideInInspector] public bool isIlluminated = false;
        [HideInInspector] public Coroutine fillCoroutine = null; // Para controlar animación
    }

    [Header("UI References")]
    [SerializeField] private StatueUISlot[] statueSlots;
    [SerializeField] private AudioClip statueCollectedSound;
    [SerializeField] private float slotAnimationDuration = 0.5f; // Para el pulso

    [Header("Panel Settings")]
    [SerializeField] private GameObject statuePanel;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private float panelShowDelay = 0.5f;
    [SerializeField] private float panelFadeTime = 1.0f;

    [Header("Feedback Settings")]
    [SerializeField] private GameObject statueFeedbackPanel;
    [SerializeField] private TMP_Text feedbackText; // Cambiado a TMP_Text si usas TextMeshPro
    [SerializeField] private float feedbackDuration = 3.0f;

    private Dictionary<string, StatueUISlot> slotLookup = new Dictionary<string, StatueUISlot>();
    private AudioSource audioSource;
    private Coroutine feedbackCoroutine;

    private void Awake()
    {
        // Inicializar diccionario y estado inicial
        foreach (var slot in statueSlots)
        {
            if (!string.IsNullOrEmpty(slot.statueId))
            {
                slotLookup[slot.statueId] = slot;
                InitializeSlotVisuals(slot); // Poner en estado inicial
            }
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (statuePanel != null)
        {
            if (panelCanvasGroup == null) panelCanvasGroup = statuePanel.GetComponent<CanvasGroup>() ?? statuePanel.AddComponent<CanvasGroup>();
            panelCanvasGroup.alpha = 0f; // Empezar oculto
            // statuePanel.SetActive(false); // No desactivar el objeto, solo controlar alpha
        }

        if (statueFeedbackPanel != null) statueFeedbackPanel.SetActive(false);

        // --- Suscribirse al evento de carga ---
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnGameLoaded += RefreshUI; // Llama a RefreshUI cuando se cargan datos
        }
        // ------------------------------------
    }

     private void OnDestroy() // Asegurarse de desuscribirse
    {
        // Desuscribirse de eventos
        StatueEvents.OnStatueCollected -= OnStatueCollected;
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnGameLoaded -= RefreshUI;
        }
    }


    private void OnEnable()
    {
        // Suscribirse a eventos de recolección
        StatueEvents.OnStatueCollected += OnStatueCollected;

        // Actualizar UI con estado actual al activarse (útil si se carga después de Start)
         RefreshUI(); // Llama al método que actualiza basado en StatueEvents

        // Mostrar panel con animación
        StartCoroutine(ShowPanelDelayed());
    }

    private void OnDisable()
    {
        // Desuscribirse para evitar errores
        StatueEvents.OnStatueCollected -= OnStatueCollected;
         // No necesitamos desuscribir de OnGameLoaded aquí si lo hacemos en OnDestroy
    }

    // --- MÉTODO LLAMADO POR SaveSystem.OnGameLoaded ---
    public void RefreshUI()
    {
         Debug.Log("StatueUI: Refreshing UI based on loaded StatueEvents state.");
        foreach (var kvp in slotLookup)
        {
            string id = kvp.Key;
            StatueUISlot slot = kvp.Value;

            bool isCollected = StatueEvents.IsStatueCollected(id);

            // Detener animación previa si la hubiera
             if (slot.fillCoroutine != null)
             {
                 StopCoroutine(slot.fillCoroutine);
                 slot.fillCoroutine = null;
             }

            // Aplicar estado visual SIN animación
            if (slot.fillImage != null)
            {
                slot.fillImage.fillAmount = isCollected ? 1f : 0f;
                slot.fillImage.color = isCollected ? slot.illuminatedColor : slot.normalColor;
                 // Resetear escala por si se interrumpió una animación de pulso
                slot.fillImage.transform.localScale = Vector3.one;
            }
            if (slot.highlightEffect != null)
            {
                slot.highlightEffect.SetActive(isCollected);
            }
            slot.isIlluminated = isCollected;
        }
    }
    // ----------------------------------------------

    private void InitializeSlotVisuals(StatueUISlot slot)
    {
         if (slot.fillImage != null)
        {
            slot.fillImage.type = Image.Type.Filled;
            slot.fillImage.fillMethod = Image.FillMethod.Vertical;
            slot.fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            slot.fillImage.fillAmount = 0f;
            slot.fillImage.color = slot.normalColor;
            slot.fillImage.transform.localScale = Vector3.one; // Asegurar escala inicial
        }
        if (slot.highlightEffect != null) slot.highlightEffect.SetActive(false);
        slot.isIlluminated = false;
        if (slot.fillCoroutine != null) { StopCoroutine(slot.fillCoroutine); slot.fillCoroutine = null; } // Detener corutina
    }


    private IEnumerator ShowPanelDelayed()
    {
        // Si el panel ya es visible, no hacer nada
        if (panelCanvasGroup != null && panelCanvasGroup.alpha >= 1f) yield break;

        yield return new WaitForSeconds(panelShowDelay);

        if (statuePanel != null && panelCanvasGroup != null)
        {
            statuePanel.SetActive(true); // Asegurar que esté activo para la corrutina
            float elapsedTime = 0f;
            float startAlpha = panelCanvasGroup.alpha; // Empezar desde alpha actual

            while (elapsedTime < panelFadeTime)
            {
                elapsedTime += Time.deltaTime;
                panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsedTime / panelFadeTime);
                yield return null;
            }
            panelCanvasGroup.alpha = 1f;
        }
    }

    private void OnStatueCollected(string statueId, Vector3 position)
    {
        // Actualizar slot específico CON animación y sonido
        MarkStatueCollected(statueId, true, true);
    }

    // Método sobrecargado que permite especificar el modo de actualización
    private void MarkStatueCollected(string statueId, bool withAnimation = true, bool playSound = true)
    {
        if (string.IsNullOrEmpty(statueId)) return;

        if (slotLookup.TryGetValue(statueId, out StatueUISlot slot))
        {
            // Si ya está iluminada, no hacer nada más
            if (slot.isIlluminated) return;

             // Detener animación anterior si existe
             if (slot.fillCoroutine != null)
             {
                 StopCoroutine(slot.fillCoroutine);
                 // Resetear visuales por si se interrumpió a medio camino
                 InitializeSlotVisuals(slot);
             }


            if (withAnimation)
            {
                slot.fillCoroutine = StartCoroutine(AnimateSlotFill(slot)); // Guardar referencia a corutina
                ShowStatueFeedback(GetStatueDisplayName(statueId));
                if (playSound && audioSource != null && statueCollectedSound != null)
                {
                    audioSource.PlayOneShot(statueCollectedSound);
                }
            }
            else // Actualización silenciosa (ej. al cargar)
            {
                 if (slot.fillImage != null)
                 {
                     slot.fillImage.fillAmount = 1f;
                     slot.fillImage.color = slot.illuminatedColor;
                 }
                 if (slot.highlightEffect != null) slot.highlightEffect.SetActive(true);
                 slot.isIlluminated = true; // Marcar como iluminada inmediatamente
            }
        }
    }

    private IEnumerator AnimateSlotFill(StatueUISlot slot)
    {
        if (slot.fillImage == null) yield break;

        // --- Animación de Pulso (opcional pero da buen feedback) ---
        Vector3 originalScale = slot.fillImage.transform.localScale;
        Vector3 targetScale = originalScale * 1.3f;
        float pulseElapsedTime = 0f;
        while (pulseElapsedTime < slotAnimationDuration)
        {
            pulseElapsedTime += Time.deltaTime;
            float tPulse = Mathf.Clamp01(pulseElapsedTime / slotAnimationDuration);
            float scaleFactor = 1.0f + 0.3f * Mathf.Sin(tPulse * Mathf.PI); // Sube y baja
            slot.fillImage.transform.localScale = originalScale * scaleFactor;
            yield return null;
        }
        slot.fillImage.transform.localScale = originalScale; // Resetear escala
        // --- Fin Pulso ---


        // --- Animación de Llenado ---
        float fillElapsedTime = 0f;
        float startFill = slot.fillImage.fillAmount; // Empezar desde el valor actual
        Color startColor = slot.fillImage.color;
        Color endColor = slot.illuminatedColor;

        while (fillElapsedTime < slot.fillDuration)
        {
            fillElapsedTime += Time.deltaTime;
            float tFill = Mathf.Clamp01(fillElapsedTime / slot.fillDuration);
            float smoothT = tFill * tFill * (3f - 2f * tFill); // Ease in-out suave

            slot.fillImage.fillAmount = Mathf.Lerp(startFill, 1f, smoothT);
            slot.fillImage.color = Color.Lerp(startColor, endColor, smoothT);
            yield return null;
        }
        slot.fillImage.fillAmount = 1f;
        slot.fillImage.color = endColor;
        // --- Fin Llenado ---

        if (slot.highlightEffect != null) slot.highlightEffect.SetActive(true);
        slot.isIlluminated = true; // Marcar como iluminada al final
        slot.fillCoroutine = null; // Limpiar referencia a corutina
    }


    private void ShowStatueFeedback(string statueName)
    {
        if (statueFeedbackPanel == null || feedbackText == null) return;

        feedbackText.text = $"{statueName} Statue Collected!";

        if (feedbackCoroutine != null) StopCoroutine(feedbackCoroutine);
        feedbackCoroutine = StartCoroutine(ShowFeedbackTemporarily());
    }

    private IEnumerator ShowFeedbackTemporarily()
    {
        if (statueFeedbackPanel == null) yield break;

        statueFeedbackPanel.SetActive(true);
        CanvasGroup feedbackCanvasGroup = statueFeedbackPanel.GetComponent<CanvasGroup>() ?? statueFeedbackPanel.AddComponent<CanvasGroup>();

        // Fade in
        feedbackCanvasGroup.alpha = 0f;
        float fadeInTime = 0.3f;
        float elapsed = 0f;
        while (elapsed < fadeInTime)
        {
            elapsed += Time.deltaTime;
            feedbackCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInTime);
            yield return null;
        }
        feedbackCanvasGroup.alpha = 1f;

        yield return new WaitForSeconds(feedbackDuration);

        // Fade out
        elapsed = 0f;
        float fadeOutTime = 0.5f;
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            feedbackCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutTime);
            yield return null;
        }

        statueFeedbackPanel.SetActive(false);
        feedbackCoroutine = null;
    }

    private string GetStatueDisplayName(string statueId)
    {
        switch (statueId)
        {
            case "BEAR": return "Bear";
            case "MOUSE": return "Mouse";
            case "DOG": return "Dog";
            case "RACCOON": return "Raccoon";
            default: return statueId;
        }
    }

    // Public method to reset UI (for testing)
    public void ResetUI()
    {
        foreach (var slot in statueSlots)
        {
            InitializeSlotVisuals(slot);
        }
        Debug.Log("StatueUI: UI Reseteada");
    }
}