using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

public class PauseMenuController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private GameObject saveIndicator;
    [SerializeField] private float saveIndicatorDuration = 2f;
    
    [Header("Button References")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    
    [Header("Configuration")]
    [SerializeField] private bool autoSaveOnExit = true;
    [SerializeField] private string mainMenuScene = "MainMenu";
    
    [Header("References")]
    [SerializeField] private SettingsManager settingsManager;
    
    // Estado privado
    private bool isPaused = false;
    private string currentSceneName;
    private SaveSystem saveSystem;
    private Coroutine saveIndicatorCoroutine;

    void Start()
    {
        InitializeComponents();
        SetupButtons();
        
        // Verificar si venimos de la pantalla de configuración
        CheckReturnFromSettings();
    }

    private void InitializeComponents()
    {
        // Guardar el nombre de la escena actual al iniciar
        currentSceneName = SceneManager.GetActiveScene().name;
        
        // Buscar referencias si no están asignadas
        if (settingsManager == null)
        {
            settingsManager = FindAnyObjectByType<SettingsManager>();
        }
        
        saveSystem = FindAnyObjectByType<SaveSystem>();
        if (saveSystem == null)
        {
            Debug.LogWarning("PauseMenuController: No se encontró SaveSystem en la escena");
            
            // Deshabilitar botón de guardado si no hay sistema de guardado
            if (saveButton != null)
            {
                saveButton.interactable = false;
            }
        }
        
        // Inicialmente, asegurarse de que el menú de pausa esté desactivado
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }
        
        // Ocultar el indicador de guardado
        if (saveIndicator != null)
        {
            saveIndicator.SetActive(false);
        }
    }

    private void SetupButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(ResumeGame);
        }
        
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(SaveGame);
            
            // Solo habilitar en la escena principal
            saveButton.interactable = (currentSceneName == "mainSceneQuest" && saveSystem != null);
        }
        
        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OpenSettings);
            Debug.Log("Configuración del botón de ajustes: " + settingsButton.name);
        }
        
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    private void CheckReturnFromSettings()
    {
        // Si venimos de la pantalla de settings, restaurar el estado
        if (PlayerPrefs.HasKey("ReturnFromSettings"))
        {
            PlayerPrefs.DeleteKey("ReturnFromSettings");
            PlayerPrefs.Save();
            
            string lastScene = PlayerPrefs.GetString("LastSceneName", "");
            if (lastScene == currentSceneName && currentSceneName == "mainSceneQuest")
            {
                if (saveSystem != null)
                {
                    saveSystem.LoadGame();
                }
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void ResumeGame()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }
        
        Time.timeScale = 1f;
        isPaused = false;

        // Bloquear y ocultar el cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void PauseGame()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(true);
        }
        
        Time.timeScale = 0f;
        isPaused = true;

        // Desbloquear y mostrar el cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void SaveGame()
    {
        if (saveSystem != null && currentSceneName == "mainSceneQuest")
        {
            saveSystem.SaveGame();
            ShowSaveIndicator();
            Debug.Log("Juego guardado manualmente desde el menú de pausa");
        }
        else
        {
            Debug.LogWarning("No se puede guardar: SaveSystem no disponible o escena incorrecta");
        }
    }

    public void OpenSettings()
    {
        // Guardar información de la escena actual para el regreso
        PlayerPrefs.SetString("LastSceneName", currentSceneName);
        Debug.Log("Abriendo configuración desde: " + currentSceneName);
        
        if (settingsManager != null)
        {
            // Si tenemos un settings manager en escena, usarlo
            settingsManager.OpenSettings();
        }
        else
        {
            // Caso contrario, cargar la escena de settings directamente
            PlayerPrefs.SetInt("ReturnFromSettings", 1);
            PlayerPrefs.Save();
            
            // Restaurar escala de tiempo para evitar problemas
            Time.timeScale = 1f;
            
            SceneManager.LoadScene("SettingsMainMenu");
        }
    }
    public void ReturnToMainMenu()
    {
        // Restaurar el tiempo normal
        Time.timeScale = 1f;
        
        // Limpiar la flag de settings para evitar problemas
        PlayerPrefs.DeleteKey("ReturnFromSettings");
        PlayerPrefs.Save();
        
        // Guardar automáticamente si está configurado
        if (autoSaveOnExit && currentSceneName == "mainSceneQuest")
        {
            if (saveSystem != null)
            {
                saveSystem.SaveGame();
                Debug.Log("Estado del juego guardado automáticamente antes de salir al menú principal");
            }
        }
        
        // Mostrar pantalla de carga si es necesario
        StartCoroutine(LoadMainMenuWithDelay());
    }
    
    private System.Collections.IEnumerator LoadMainMenuWithDelay()
    {
        // Pequeña pausa para permitir que los sistemas terminen sus procesos
        yield return new WaitForSecondsRealtime(0.5f);
        
        // Cargar el menú principal
        SceneManager.LoadScene(mainMenuScene);
    }
    
    private void ShowSaveIndicator()
    {
        if (saveIndicator == null) return;
        
        // Detener coroutine anterior si existe
        if (saveIndicatorCoroutine != null)
        {
            StopCoroutine(saveIndicatorCoroutine);
        }
        
        saveIndicatorCoroutine = StartCoroutine(ShowSaveIndicatorTemporarily());
    }
    
    private System.Collections.IEnumerator ShowSaveIndicatorTemporarily()
    {
        saveIndicator.SetActive(true);
        
        // Efecto de fade in
        CanvasGroup canvasGroup = saveIndicator.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = saveIndicator.AddComponent<CanvasGroup>();
        }
        
        // Fade in
        canvasGroup.alpha = 0;
        float elapsed = 0;
        float fadeDuration = 0.3f;
        
        while (elapsed < fadeDuration)
        {
            canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / fadeDuration);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        
        canvasGroup.alpha = 1;
        
        // Mantener visible
        yield return new WaitForSecondsRealtime(saveIndicatorDuration);
        
        // Fade out
        elapsed = 0;
        while (elapsed < fadeDuration)
        {
            canvasGroup.alpha = Mathf.Lerp(1, 0, elapsed / fadeDuration);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        
        saveIndicator.SetActive(false);
    }
    
    private void OnDestroy()
    {
        // Asegurar que el tiempo de juego está normalizado
        Time.timeScale = 1f;
    }
}