using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video; // Necesario para VideoPlayer
using TMPro; // Necesario si usas TextMeshPro para el texto
using System.Collections; // Necesario para Coroutines

[RequireComponent(typeof(VideoPlayer))] // Asegura que haya un VideoPlayer
public class AnimationsMenu : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Arrastra aquí el Panel UI que contiene el texto 'Press Esc to skip'. Debe estar desactivado por defecto.")]
    [SerializeField] private GameObject skipPromptPanel;
    [Tooltip("El componente VideoPlayer que reproduce la animación/video.")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Configuración")]
    [Tooltip("Nombre exacto de la escena a cargar después del video/skip.")]
    [SerializeField] private string sceneToLoadAfterVideo = "TimmyHouse_Tutorial";
    [Tooltip("Tiempo (en segundos) antes de que aparezca el aviso para saltar.")]
    [SerializeField] private float promptDelay = 5.0f;
    [Tooltip("Tiempo (en segundos) que el aviso permanece visible si no se presiona Esc.")]
    [SerializeField] private float promptDuration = 4.0f;

    // Estado interno
    private bool canSkip = false; // Indica si el panel está activo y se puede saltar
    private Coroutine promptCoroutine; // Para controlar la visibilidad del panel

    void Awake()
    {
        // Obtener VideoPlayer si no está asignado
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        // Asegurar que el panel esté oculto al inicio
        if (skipPromptPanel != null)
        {
            skipPromptPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("AnimationsMenu: ¡El Panel para saltar (skipPromptPanel) no está asignado en el Inspector!", this);
        }
    }

    void Start()
    {
        // Suscribirse al evento de finalización del video
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoEnd;
            // Iniciar la corutina para mostrar el prompt después de un retraso
            if (skipPromptPanel != null)
            {
                promptCoroutine = StartCoroutine(ShowPromptAfterDelay());
            }
        }
        else
        {
            Debug.LogError("AnimationsMenu: ¡VideoPlayer no encontrado o asignado! El script no funcionará correctamente.", this);
            enabled = false; // Desactivar script si no hay video player
        }
    }

    void Update()
    {
        // Detectar la tecla Esc
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Si el panel está visible (canSkip es true) -> Saltar video
            if (canSkip && skipPromptPanel != null && skipPromptPanel.activeSelf)
            {
                Debug.Log("Esc presionado mientras el prompt estaba visible. Saltando video...");
                SkipVideo();
            }
            // Si el panel NO está visible -> Mostrarlo inmediatamente
            else if (skipPromptPanel != null)
            {
                Debug.Log("Esc presionado. Mostrando prompt para saltar...");
                ShowSkipPrompt(); // Muestra el prompt y reinicia su temporizador
            }
        }
    }

    // Corutina para mostrar el prompt después de un tiempo
    private IEnumerator ShowPromptAfterDelay()
    {
        yield return new WaitForSeconds(promptDelay);
        ShowSkipPrompt();
    }

    // Muestra el prompt y empieza la cuenta atrás para ocultarlo
    private void ShowSkipPrompt()
    {
        if (skipPromptPanel == null || videoPlayer == null || !videoPlayer.isPlaying) return; // No mostrar si no hay panel o el video ya terminó

        // Detener corutina anterior si existía (para resetear el timer)
        if (promptCoroutine != null)
        {
            StopCoroutine(promptCoroutine);
        }

        skipPromptPanel.SetActive(true);
        canSkip = true; // Ahora se puede saltar
        promptCoroutine = StartCoroutine(HidePromptAfterDuration()); // Iniciar timer para ocultar
    }

    // Corutina para ocultar el prompt después de un tiempo
    private IEnumerator HidePromptAfterDuration()
    {
        yield return new WaitForSeconds(promptDuration);
        if (skipPromptPanel != null)
        {
            skipPromptPanel.SetActive(false);
        }
        canSkip = false; // Ya no se puede saltar (hasta que se presione Esc de nuevo)
        promptCoroutine = null; // Limpiar referencia
    }

    // Salta el video y carga la siguiente escena
    private void SkipVideo()
    {
        // Detener la corutina del prompt si está activa
        if (promptCoroutine != null)
        {
            StopCoroutine(promptCoroutine);
            promptCoroutine = null;
        }

        // Ocultar el panel por si acaso
        if (skipPromptPanel != null)
        {
            skipPromptPanel.SetActive(false);
        }
        canSkip = false;

        // Detener el video si se está reproduciendo
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        // Cargar la siguiente escena
        LoadNextScene();
    }

    // Se llama automáticamente cuando el video termina
    void OnVideoEnd(VideoPlayer vp)
    {
        Debug.Log("Video finalizado automáticamente.");
        LoadNextScene();
    }

    // Carga la escena definida
    void LoadNextScene()
    {
        // Asegurarse de desuscribirse del evento antes de cargar la nueva escena
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
        }

        // Cargar la escena
        Debug.Log($"Cargando escena: {sceneToLoadAfterVideo}");
        SceneManager.LoadScene(sceneToLoadAfterVideo);
    }

    // Limpieza al destruir el objeto
    void OnDestroy()
    {
        // Desuscribirse del evento para evitar errores si el objeto se destruye
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
        }
        // Detener corutinas si aún existen
        if (promptCoroutine != null)
        {
            StopCoroutine(promptCoroutine);
        }
    }
}