using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // Singleton instance
    public static GameManager Instance { get; private set; }
    
    [Header("Managers de Sistema")]
    [SerializeField] private GameStateManager stateManager;
    [SerializeField] private AudioManager audioManager;
    
    [Header("Escenas")]
    [SerializeField] private string tutorialSceneName = "TimmyHouse_Tutorial";
    [SerializeField] private string mainSceneName = "mainSceneQuest";
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    
    [Header("Configuración")]
    [SerializeField] private bool debugMode = false;
    
    // Estado global
    private Dictionary<string, bool> globalFlags = new Dictionary<string, bool>();
    private Dictionary<string, int> globalCounters = new Dictionary<string, int>();
    private Dictionary<string, string> globalStrings = new Dictionary<string, string>();
    
    // Evento para notificar cambios en el estado global
    public delegate void GameStateChangedHandler(string key, object value);
    public static event GameStateChangedHandler OnGameStateChanged;

    [System.Obsolete]
    private void Awake()
    {
        // Implementación de singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Inicializar valores por defecto
            InitializeDefaultState();
            
            // Inicializar sistemas
            InitializeManagers();
            
            // Registrar para eventos de cambio de escena
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            LogInfo("GameManager inicializado correctamente");
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeDefaultState()
    {
        // Flags iniciales
        globalFlags["tutorialCompleted"] = false;
        globalFlags["fountainActivated"] = false;
        globalFlags["finalSequenceTriggered"] = false;
        
        // Contadores iniciales
        globalCounters["statuesCollected"] = 0;
        
        // Strings iniciales
        globalStrings["lastLoadedScene"] = "";
    }

    [System.Obsolete]
    private void InitializeManagers()
    {
        // Crear GameStateManager si no existe
        if (stateManager == null)
        {
            GameObject stateObj = new GameObject("GameStateManager");
            stateManager = stateObj.AddComponent<GameStateManager>();
            stateObj.transform.SetParent(transform);
        }
        
        // Crear AudioManager si no existe
        if (audioManager == null)
        {
            GameObject audioObj = GameObject.Find("AudioManager");
            if (audioObj == null)
            {
                audioObj = new GameObject("AudioManager");
                audioManager = audioObj.AddComponent<AudioManager>();
                audioObj.transform.SetParent(transform);
            }
            else
            {
                audioManager = audioObj.GetComponent<AudioManager>();
            }
        }
        
        // Suscribirse a eventos globales
        StatueEvents.OnStatueCollected += OnStatueCollected;
        StatueEvents.OnAllStatuesCollected += OnAllStatuesCollected;
    }

    [System.Obsolete]
    private void OnDestroy()
    {
        // Desuscribirse de eventos
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StatueEvents.OnStatueCollected -= OnStatueCollected;
        StatueEvents.OnAllStatuesCollected -= OnAllStatuesCollected;
    }

    [System.Obsolete]
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LogInfo($"Escena cargada: {scene.name}");
        
        // Guardar nombre de última escena cargada
        globalStrings["lastLoadedScene"] = scene.name;
        
        // Realizar configuraciones específicas por escena
        if (scene.name == mainSceneName)
        {
            InitializeMainScene();
        }
        else if (scene.name == tutorialSceneName)
        {
            InitializeTutorialScene();
        }
    }

    [System.Obsolete]
    private void InitializeMainScene()
    {
        LogInfo("Inicializando mainSceneQuest");
        
        // Buscar y activar el controlador de secuencia tutorial
        StartCoroutine(InitializeWithDelay());
    }

    [System.Obsolete]
    private IEnumerator InitializeWithDelay()
    {
        // Esperar a que todos los objetos estén inicializados
        yield return new WaitForSeconds(0.5f);
        
        // Iniciar tutorial de lock-on si no se ha completado
        TutorialSequenceController tutorialController = FindObjectOfType<TutorialSequenceController>();
        if (tutorialController != null && !GetGlobalFlag("tutorialLockOnCompleted"))
        {
            tutorialController.StartTutorialSequence();
            LogInfo("Iniciando secuencia tutorial de lock-on");
        }
        
        // Actualizar UI de estatuas
        UpdateStatueUI();
    }
    
    private void InitializeTutorialScene()
    {
        LogInfo("Inicializando TimmyHouse_Tutorial");
        
        // Implementar inicialización específica del tutorial
    }

    // Métodos para estatuas
    [System.Obsolete]
    private void OnStatueCollected(string statueId, Vector3 position)
    {
        // Incrementar contador global
        IncrementGlobalCounter("statuesCollected");
        
        // Actualizar UI
        UpdateStatueUI();
        
        LogInfo($"Estatua recolectada: {statueId}, Total: {GetGlobalCounter("statuesCollected")}/4");
    }
    
    private void OnAllStatuesCollected()
    {
        // Marcar flag global
        SetGlobalFlag("allStatuesCollected", true);
        
        LogInfo("Todas las estatuas han sido recolectadas");
    }

    [System.Obsolete]
    private void UpdateStatueUI()
    {
        StatueUI statueUI = FindObjectOfType<StatueUI>();
        if (statueUI != null)
        {
            // La UI se actualizará basada en el estado de StatueEvents
            // que mantiene su propio registro de estatuas recolectadas
        }
    }
    
    // Métodos para gestión del estado global
    public void SetGlobalFlag(string key, bool value)
    {
        globalFlags[key] = value;
        OnGameStateChanged?.Invoke(key, value);
    }
    
    public bool GetGlobalFlag(string key)
    {
        if (globalFlags.TryGetValue(key, out bool value))
        {
            return value;
        }
        return false;
    }
    
    public void SetGlobalCounter(string key, int value)
    {
        globalCounters[key] = value;
        OnGameStateChanged?.Invoke(key, value);
    }
    
    public int GetGlobalCounter(string key)
    {
        if (globalCounters.TryGetValue(key, out int value))
        {
            return value;
        }
        return 0;
    }
    
    public void IncrementGlobalCounter(string key, int amount = 1)
    {
        if (!globalCounters.ContainsKey(key))
        {
            globalCounters[key] = 0;
        }
        
        globalCounters[key] += amount;
        OnGameStateChanged?.Invoke(key, globalCounters[key]);
    }
    
    public void SetGlobalString(string key, string value)
    {
        globalStrings[key] = value;
        OnGameStateChanged?.Invoke(key, value);
    }
    
    public string GetGlobalString(string key)
    {
        if (globalStrings.TryGetValue(key, out string value))
        {
            return value;
        }
        return string.Empty;
    }
    
    // Métodos para cargar escenas
    public void LoadScene(string sceneName, float delay = 1.0f)
    {
        StartCoroutine(LoadSceneWithDelay(sceneName, delay));
    }
    
    private IEnumerator LoadSceneWithDelay(string sceneName, float delay)
    {
        // Guardar estado actual
        SaveGameState();
        
        // Esperar el tiempo especificado
        yield return new WaitForSeconds(delay);
        
        // Cargar la nueva escena
        SceneManager.LoadScene(sceneName);
    }
    
    // Guardar y cargar el estado del juego
    public void SaveGameState()
    {
        if (stateManager != null)
        {
            stateManager.SaveGameState();
            LogInfo("Estado del juego guardado");
        }
    }
    
    public void LoadGameState()
    {
        if (stateManager != null)
        {
            stateManager.RestoreGameState();
            LogInfo("Estado del juego cargado");
        }
    }
    
    // Utilidad para logging
    private void LogInfo(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[GameManager] {message}");
        }
    }
}