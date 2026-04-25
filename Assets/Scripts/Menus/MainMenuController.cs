using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Necesario para Slider
using TMPro;        // Necesario para TextMeshProUGUI
using System.Collections; // Necesario para IEnumerator

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject continueButton; // Asegúrate de arrastrar tu botón "Continue" aquí
    [SerializeField] private GameObject loadingPanel;   // Panel de carga (opcional)
    [SerializeField] private Slider loadingSlider;      // Slider de carga (opcional)
    [SerializeField] private TextMeshProUGUI loadingText; // Texto de carga (opcional)

    [Header("Configuración")]
    [SerializeField] private string mainGameScene = "mainSceneQuest";
    [SerializeField] private string introScene = "AnimationStartGame"; // Escena de intro si es diferente a mainGameScene
    [SerializeField] private GameObject settingsPanel; // Escena de configuración
    [SerializeField] private float minimumLoadingTime = 1.0f; // Tiempo mínimo de pantalla de carga

    [Header("Opciones de Guardado")]
    [SerializeField] private bool createBackupOnNewGame = true;

    // Referencias privadas
    private SaveSystem saveSystem;

    // Awake se usa para inicializar antes que Start
    void Awake()
    {
        InitializeReferences();
        // Asegurar que el cursor sea visible en el menú principal
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void Start()
    {
        // Comprobar datos guardados en Start, después de que SaveSystem se haya inicializado
        CheckSaveData();
    }

    private void InitializeReferences()
    {
        // Intentar encontrar el SaveSystem (debería existir si usas la escena Boot)
        saveSystem = SaveSystem.Instance; // Acceder a la instancia Singleton

        if (saveSystem == null)
        {
            Debug.LogWarning("MainMenuController: SaveSystem.Instance no encontrado. Las funciones de guardado/carga no funcionarán correctamente.");
        }

        // Configurar panel de carga
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
        else
        {
             Debug.LogWarning("MainMenuController: Loading Panel no asignado.");
        }
    }

    private void CheckSaveData()
    {
        bool hasSaveData = false;

        // Usar el SaveSystem para verificar si hay datos
        if (saveSystem != null)
        {
            hasSaveData = saveSystem.HasSaveData();
             Debug.Log($"CheckSaveData: SaveSystem.HasSaveData() devolvió {hasSaveData}");
        }
        else
        {
            // Fallback si SaveSystem no está disponible (no debería pasar con el Singleton)
            Debug.LogWarning("CheckSaveData: SaveSystem no disponible, no se puede verificar el estado guardado.");
        }

        // Activar/desactivar el botón "Continue"
        if (continueButton != null)
        {
            continueButton.SetActive(hasSaveData);
            Debug.Log($"Botón Continue {(hasSaveData ? "activado" : "desactivado")}.");
        }
        else {
             Debug.LogError("MainMenuController: Botón Continue no asignado en el Inspector!");
        }
    }

    public void StartGame()
    {
        // Si hay datos guardados y la opción está activa, hacer backup
        if (createBackupOnNewGame && saveSystem != null && saveSystem.HasSaveData())
        {
            Debug.Log("Creando backup antes de iniciar nuevo juego...");
            saveSystem.CreateBackup();
        }

        // Eliminar datos guardados existentes usando el SaveSystem
        if (saveSystem != null)
        {
            Debug.Log("Borrando datos guardados para iniciar nuevo juego...");
            saveSystem.DeleteSaveData();
        }
        else
        {
            Debug.LogWarning("SaveSystem no disponible, no se pudieron borrar los datos guardados.");
            // Considera añadir un fallback a PlayerPrefs.DeleteAll() si es crítico
            // PlayerPrefs.DeleteAll(); PlayerPrefs.Save();
        }

        // Comenzar la carga de la escena de introducción (o la principal si no hay intro)
        Debug.Log($"Cargando escena de inicio: {introScene}");
        StartCoroutine(LoadSceneAsync(introScene));
    }

    public void ContinueGame()
    {
        // Solo continuar si hay datos guardados
        if (saveSystem != null && saveSystem.HasSaveData())
        {
             // Cargar directamente la escena principal con transición
             Debug.Log($"Continuando juego. Cargando escena: {mainGameScene}");
            StartCoroutine(LoadSceneAsync(mainGameScene));
        }
        else {
             Debug.LogWarning("Intento de continuar sin datos guardados.");
             // Opcional: Mostrar un mensaje al usuario
        }

    }

    public void OpenSettings()
    {
        // Mostrar el panel de configuración (asegúrate de que esté activo en el Inspector)
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            Debug.Log("Abriendo panel de configuración.");
        }
        else
        {
             Debug.LogWarning("MainMenuController: Panel de configuración no asignado.");
        }
    }

    public void ExitGame()
    {
        // Opcionalmente hacer un autoguardado de respaldo antes de salir (si hay datos)
        if (saveSystem != null && saveSystem.HasSaveData())
        {
            Debug.Log("Creando backup antes de salir...");
            saveSystem.CreateBackup();
        }

        Debug.Log("Saliendo del juego...");
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Detener Play Mode en el editor
        #endif
    }

    // Corutina para cargar escena con feedback visual (sin cambios)
    private System.Collections.IEnumerator LoadSceneAsync(string sceneName)
    {
        if (loadingPanel != null) loadingPanel.SetActive(true);

        float startTime = Time.realtimeSinceStartup; // Usar tiempo real para la duración mínima

        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(sceneName);
        asyncOperation.allowSceneActivation = false;

        while (!asyncOperation.isDone)
        {
            // progreso va de 0.0 a 0.9 mientras carga, luego salta a 1.0 al activar
            float progress = Mathf.Clamp01(asyncOperation.progress / 0.9f);

            if (loadingSlider != null) loadingSlider.value = progress;
            if (loadingText != null) loadingText.text = $"Loading... {Mathf.RoundToInt(progress * 100)}%";

            // Permitir activación si la carga terminó Y ha pasado el tiempo mínimo
            if (asyncOperation.progress >= 0.9f && (Time.realtimeSinceStartup - startTime) >= minimumLoadingTime)
            {
                asyncOperation.allowSceneActivation = true;
            }

            yield return null; // Esperar al siguiente frame
        }
         // Opcional: Ocultar panel de carga después de cargar (aunque la escena cambiará)
         // if (loadingPanel != null) loadingPanel.SetActive(false);
    }
}