using System.Collections;
using UnityEngine;
using UnityEngine.UI; // Necesario para Image
using TMPro;        // Necesario para TextMeshProUGUI

public class StatueUIManager : MonoBehaviour
{
    // --- Singleton ---
    private static StatueUIManager _instance;
    public static StatueUIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // Intenta encontrar una instancia existente en la escena
                _instance = Object.FindAnyObjectByType<StatueUIManager>(); // Cambiado a FindObjectOfType para buscar específicamente este tipo

                // Si no existe, crea una nueva (aunque Awake debería manejar esto si está en la escena inicial)
                if (_instance == null)
                {
                    GameObject managerObj = new GameObject("StatueUIManager_AutoCreated");
                    _instance = managerObj.AddComponent<StatueUIManager>();
                    // Initialize se llamará en su propio Awake
                    Debug.LogWarning("StatueUIManager: Instance not found, auto-creating. It's better to have it in the initial scene.");
                }
            }
            // Asegurar que está inicializado si se accede tarde
            if (!_instance.isInitialized)
            {
                 _instance.Initialize();
            }
            return _instance;
        }
    }

    [Header("Panel de Historia (Asignar en Inspector)")]
    [Tooltip("El GameObject raíz del panel de la historia.")]
    [SerializeField] private GameObject storyPanel;
    [Tooltip("El componente TextMeshProUGUI para el título.")]
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("El componente TextMeshProUGUI para el texto de la historia.")]
    [SerializeField] private TextMeshProUGUI storyText;
    [Tooltip("El componente Image para la imagen de la historia (opcional).")]
    [SerializeField] private Image storyImage;
    [Tooltip("El CanvasGroup del panel para controlar el fundido (fade).")]
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Animación")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.3f;

    [Header("Audio (Opcional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    // Estado interno
    private StatueStory currentStory;
    private Coroutine fadeCoroutine;
    private bool isInitialized = false;

    // Propiedad para saber si el panel está visible (más fiable que activeSelf solo)
    public bool IsStoryPanelVisible => storyPanel != null && panelCanvasGroup != null && panelCanvasGroup.alpha > 0.9f && storyPanel.activeSelf;

    private void Awake()
    {
        // Configuración Singleton
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize(); // Inicializar en Awake
        }
        else if (_instance != this)
        {
            // Si ya existe una instancia (y no soy yo), me destruyo
            Debug.LogWarning("StatueUIManager: Destruyendo instancia duplicada.");
            Destroy(gameObject);
        }
    }

    private void Initialize()
    {
        if (isInitialized) return;

        // --- Verificar Referencias del Inspector ---
        bool refsOk = true;
        if (storyPanel == null) { Debug.LogError("StatueUIManager: Reference to 'storyPanel' not assigned in Inspector!", this); refsOk = false; }
        if (titleText == null) { Debug.LogError("StatueUIManager: Reference to 'titleText' not assigned in Inspector!", this); refsOk = false; }
        if (storyText == null) { Debug.LogError("StatueUIManager: Reference to 'storyText' not assigned in Inspector!", this); refsOk = false; }
        // storyImage es opcional
        if (panelCanvasGroup == null)
        {
             // Intentar obtenerlo del storyPanel si no está asignado
             if(storyPanel != null) panelCanvasGroup = storyPanel.GetComponent<CanvasGroup>();
             // Si sigue siendo null, añadirlo o mostrar error
            if (panelCanvasGroup == null && storyPanel != null) {
                panelCanvasGroup = storyPanel.AddComponent<CanvasGroup>();
                 Debug.LogWarning("StatueUIManager: CanvasGroup not assigned, automatically added to 'storyPanel'.", this);
            } else if (storyPanel != null) {
                 // Encontrado en el panel
            }
             else {
                 Debug.LogError("StatueUIManager: Reference to 'panelCanvasGroup' not assigned and 'storyPanel' is null!", this);
                 refsOk = false;
            }
        }

        if (!refsOk)
        {
             Debug.LogError("StatueUIManager: Critical references missing in Inspector. Manager might not work correctly.", this);
             // Podrías decidir desactivar el componente si faltan referencias:
             // this.enabled = false;
             // return;
        }
        // --- Fin Verificación Referencias ---


        // Configurar AudioSource si no está asignado
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        // Asegurar estado inicial oculto
        if (storyPanel != null)
        {
            storyPanel.SetActive(false);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        }

        isInitialized = true;
        Debug.Log("StatueUIManager: Successfully initialized.");
    }

    // --- Eliminado FindOrCreateStoryPanel, ahora confiamos en las refs del Inspector ---

    public void ShowStoryPanel(StatueStory story)
    {
        if (!isInitialized) Initialize(); // Doble check por si se llama antes de Awake

        if (story == null) { Debug.LogWarning("StatueUIManager: Attempting to show null story."); return; }
        if (!CheckEssentialReferences()) return; // Verificar si tenemos lo mínimo para funcionar

        // Detener corutina de fade anterior si existe
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        // Actualizar contenido UI
        currentStory = story;
        titleText.text = story.localizedTitle.GetLocalizedString();
        titleText.color = story.titleColor;
        if (story.customFont != null) titleText.font = story.customFont; // Asignar fuente TMP

        storyText.text = story.localizedStoryText.GetLocalizedString();
        storyText.color = story.textColor;
        if (story.customFont != null) storyText.font = story.customFont; // Asignar fuente TMP

        // Gestionar imagen opcional
        if (storyImage != null)
        {
            if (story.storyImage != null)
            {
                storyImage.sprite = story.storyImage;
                storyImage.gameObject.SetActive(true); // Mostrar si hay sprite
            }
            else
            {
                storyImage.gameObject.SetActive(false); // Ocultar si no hay sprite
            }
        }

        // Reproducir sonido de apertura
        if (audioSource != null && openSound != null) audioSource.PlayOneShot(openSound);

        // Iniciar animación de Fade In
        fadeCoroutine = StartCoroutine(FadePanel(true));
        //Debug.Log($"StatueUIManager: Mostrando panel para {story.statueId}");
    }

    public void HideStoryPanel()
    {
        if (!isInitialized) Initialize();
        if (!CheckEssentialReferences()) return;

        // No hacer nada si ya está oculto o en proceso de ocultarse
        if (!storyPanel.activeSelf || (fadeCoroutine != null && panelCanvasGroup.alpha < 1f)) return;

        // Detener corutina de fade anterior (si la hubiera, ej: si se llama Hide justo después de Show)
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        // Reproducir sonido de cierre
        if (audioSource != null && closeSound != null) audioSource.PlayOneShot(closeSound);

        // Iniciar animación de Fade Out
        fadeCoroutine = StartCoroutine(FadePanel(false));
        //Debug.Log("StatueUIManager: Ocultando panel.");
    }

    // Corutina para el fundido (fade) del panel
    private IEnumerator FadePanel(bool fadeIn)
    {
        if (panelCanvasGroup == null) yield break; // Salir si no hay CanvasGroup

        // Asegurar que el panel está activo para la animación
        // (SetActive(true) puede causar un parpadeo si el alpha es 0, pero es necesario para que la corutina se ejecute si estaba inactivo)
        if (!storyPanel.activeSelf) storyPanel.SetActive(true);

        float startAlpha = panelCanvasGroup.alpha;
        float endAlpha = fadeIn ? 1f : 0f;
        float duration = fadeIn ? fadeInDuration : fadeOutDuration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // Usar unscaledDeltaTime para funcionar en pausa
            float t = Mathf.Clamp01(elapsed / duration);
            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        panelCanvasGroup.alpha = endAlpha; // Asegurar valor final

        // Desactivar el GameObject si es un fade out para optimizar
        if (!fadeIn)
        {
            storyPanel.SetActive(false);
            currentStory = null; // Limpiar referencia
            // Debug.Log("StatueUIManager: Panel desactivado después de fade out.");
        }
        // else { Debug.Log("StatueUIManager: Panel fade in completo."); }

        fadeCoroutine = null; // Indicar que la corutina terminó
    }

    // Helper para verificar referencias esenciales
    private bool CheckEssentialReferences()
    {
        if (storyPanel == null || panelCanvasGroup == null || titleText == null || storyText == null)
        {
             Debug.LogError("StatueUIManager: Essential UI references missing (Panel, CanvasGroup, TitleText, StoryText). Assign all in Inspector.", this);
             return false;
        }
        return true;
    }
}