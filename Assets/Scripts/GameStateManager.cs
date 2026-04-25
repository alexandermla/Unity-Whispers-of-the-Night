using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary; // Asegúrate que esté
using System.Linq; // Necesario para convertir Dictionary a List para serialización si se usa el método alternativo


public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private bool showDebugMessages = true;
    [SerializeField] private string[] validGameScenes = { "TimmyHouse_Tutorial", "mainSceneQuest", "MainMenu" };

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject cameraPrefab;

    // --- Métodos SetPlayerPrefab y SetCameraPrefab (sin cambios) ---
    public void SetPlayerPrefab(GameObject prefab) { /* ... */ }
    public void SetCameraPrefab(GameObject prefab) { /* ... */ }


    // --- Clases SerializableVector3 y SerializableQuaternion (sin cambios) ---
    [System.Serializable] public class SerializableVector3 { /* ... */ public Vector3 ToVector3() { return Vector3.zero; } } // Placeholder
    [System.Serializable] public class SerializableQuaternion { /* ... */ public Quaternion ToQuaternion() { return Quaternion.identity; } } // Placeholder

    // --- Clase SerializableDictionary para el Canvas ---
    // (Puedes reusar la que tienes en SaveSystem.cs si la haces accesible o copiarla aquí)
    [System.Serializable]
    public class SerializableStringBoolDictionary
    {
         // Usaremos una lista de KeyValuePair para que Unity la serialize
        [System.Serializable]
        public struct StringBoolPair
        {
            public string key;
            public bool value;
        }
        public List<StringBoolPair> items = new List<StringBoolPair>();

        public Dictionary<string, bool> ToDictionary()
        {
            return items.ToDictionary(pair => pair.key, pair => pair.value);
        }

        public void FromDictionary(Dictionary<string, bool> dict)
        {
            items.Clear();
            if (dict != null)
            {
                foreach (var kvp in dict)
                {
                    items.Add(new StringBoolPair { key = kvp.Key, value = kvp.Value });
                }
            }
        }
    }


    [System.Serializable]
    public class GameState
    {
        public int version = 1;

        // Datos del jugador
        public SerializableVector3 playerPosition;
        public float playerRotationY;
        public float playerHealth = 100f;

        // Datos de la cámara
        public SerializableVector3 cameraPosition;
        public SerializableQuaternion cameraRotation;

        // Datos de la lámpara
        public bool hasLampBeenPickedUp;
        public float lampEnergy;
        public bool isLampOn;

        // Datos de estatuas
        public List<string> collectedStatues = new List<string>();
        public int totalStatuesCollected;

        // Datos de misiones
        public Dictionary<string, int> questStates = new Dictionary<string, int>();
        public string currentQuestID;
        public Dictionary<string, bool> triggeredEvents = new Dictionary<string, bool>();

        // Datos de fuente central
        public bool fountainActivated;

        // Datos de luciérnaga
        public bool fireflyActive;
        public SerializableVector3 fireflyPosition;
        public string fireflyState;
        public bool fireflyFusionCompleted;

        // *** NUEVO: Datos del Canvas ***
        public SerializableStringBoolDictionary uiCanvasStates = new SerializableStringBoolDictionary();

        // Datos de escena
        public string lastSceneName;
        public bool isInitialized;
    }

    // --- Variables de Auto-guardado (sin cambios, pero probablemente quieras desactivarlo desde el Inspector) ---
    [Header("Auto-guardado (Configurable en AutoSaveManager)")]
    [SerializeField] private bool enableAutoSave = true; // Ahora controlado por AutoSaveManager
    [SerializeField] private float autoSaveInterval = 300.0f;
    [SerializeField] private bool saveOnlyWhenChanged = true;
    [SerializeField] private GameObject savingIndicator;
    [SerializeField] private float indicatorDuration = 2.0f;

    private GameState gameState = new GameState();
    private GameObject activePlayer;
    private GameObject activeCamera;


    private void Awake()
    {
        // Singleton pattern (sin cambios)
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            LoadGameState();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
         // (Sin cambios)
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }


    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
         // (Lógica de restauración sin cambios)
        string sceneName = scene.name;
        if (System.Array.IndexOf(validGameScenes, sceneName) >= 0)
        {
            LogMessage($"Escena de juego cargada: {sceneName}");
            Invoke(nameof(EnsurePlayerAndCameraExist), 0.1f);
            if (gameState.isInitialized && PlayerPrefs.GetInt("GameStateWasSaved", 0) == 1)
            {
                Invoke(nameof(RestoreGameState), 0.3f);
            }
            gameState.lastSceneName = sceneName;
        }
    }

    // Método para guardar el estado actual del juego (sin cambios en la llamada)
    public void SaveGameState()
    {
        LogMessage("Guardando estado del juego...");
        CaptureGameState(); // Captura toda la información

        // --- Serialización y guardado en PlayerPrefs (sin cambios) ---
        try {
            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream memStream = new MemoryStream())
            {
                formatter.Serialize(memStream, gameState);
                string serializedData = System.Convert.ToBase64String(memStream.ToArray());
                PlayerPrefs.SetString("SavedGameState", serializedData);
            }
            PlayerPrefs.SetInt("GameStateWasSaved", 1);
            PlayerPrefs.Save();
            LogMessage("Estado de juego guardado exitosamente en PlayerPrefs.");
        } catch (System.Exception e) {
            LogMessage($"ERROR al serializar/guardar: {e.Message}\n{e.StackTrace}");
        }
    }

    // Método para cargar el estado del juego (sin cambios)
    public void LoadGameState()
    {
        if (PlayerPrefs.HasKey("SavedGameState"))
        {
            // --- Deserialización (sin cambios) ---
             try
            {
                string serializedData = PlayerPrefs.GetString("SavedGameState");
                byte[] data = System.Convert.FromBase64String(serializedData);
                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream memStream = new MemoryStream(data))
                {
                    gameState = (GameState)formatter.Deserialize(memStream);
                }
                gameState.isInitialized = true;
                LogMessage("Estado de juego cargado exitosamente desde PlayerPrefs.");
            }
            catch (System.Exception e)
            {
                LogMessage($"Error cargando estado de juego: {e.Message}");
                gameState = new GameState();
            }
        }
        else
        {
            LogMessage("No hay estado guardado en PlayerPrefs. Usando valores por defecto.");
            gameState = new GameState();
        }
    }

    // Método para capturar el estado actual del juego
    private void CaptureGameState()
    {
        SavePlayerState();
        SaveCameraState();
        SaveLampState();
        SaveStatueStates();
        SaveQuestStates();
        SaveFountainState();
        SaveFireflyState();
        SaveCanvasState(); // *** NUEVA LLAMADA ***
        gameState.isInitialized = true; // Marcar como inicializado después de capturar
    }

    // Método para restaurar el estado del juego en la escena actual
    public void RestoreGameState()
    {
        if (!gameState.isInitialized)
        {
            LogMessage("No hay estado guardado para restaurar");
            return;
        }
        LogMessage("Restaurando estado del juego...");
        RestorePlayerState();
        RestoreCameraState();
        RestoreLampState();
        RestoreStatueStates();
        RestoreQuestStates();
        RestoreFountainState();
        RestoreFireflyState();
        RestoreCanvasState(); // *** NUEVA LLAMADA ***
        LogMessage("Estado del juego restaurado exitosamente");
    }

    // --- Métodos específicos Save/Restore (Jugador, Cámara, Lámpara, Estatuas, Misiones, Fuente, Luciérnaga) ---
    // --- SIN CAMBIOS, excepto por añadir Debug Logs si quieres ---
    private void SavePlayerState() { /* ... */ LogMessage("Player state saved."); }
    private void RestorePlayerState() { /* ... */ LogMessage("Player state restored."); }
    private void SaveCameraState() { /* ... */ LogMessage("Camera state saved."); }
    private void RestoreCameraState() { /* ... */ LogMessage("Camera state restored."); }
    private void SaveLampState() { /* ... */ LogMessage("Lamp state saved."); }
    private void RestoreLampState() { /* ... */ LogMessage("Lamp state restored."); }
    private void SaveStatueStates() { /* ... */ LogMessage("Statue states saved."); }
    private void RestoreStatueStates() { /* ... */ LogMessage("Statue states restored."); }
    private void SaveQuestStates() { /* ... */ LogMessage("Quest states saved."); }
    private void RestoreQuestStates() { /* ... */ LogMessage("Quest states restored."); }
    private void SaveFountainState() { /* ... */ LogMessage("Fountain state saved."); }
    private void RestoreFountainState() { /* ... */ LogMessage("Fountain state restored."); }
    private void SaveFireflyState() { /* ... */ LogMessage("Firefly state saved."); }
    private void RestoreFireflyState() { /* ... */ LogMessage("Firefly state restored."); }

    // *** NUEVOS MÉTODOS PARA GUARDAR/RESTAURAR CANVAS ***
    private void SaveCanvasState()
    {
        UIManager uiManager = Object.FindAnyObjectByType<UIManager>(); // O usa UIManager.Instance si es singleton
        if (uiManager != null)
        {
            // Obtener el diccionario de estados del UIManager
            Dictionary<string, bool> currentCanvasStates = uiManager.GetCanvasStates();
            // Convertir y guardar en gameState
            gameState.uiCanvasStates.FromDictionary(currentCanvasStates);
            LogMessage("Canvas states saved.");
        }
        else
        {
            LogMessage("WARN: UIManager not found, cannot save canvas states.");
        }
    }

    private void RestoreCanvasState()
    {
        UIManager uiManager = Object.FindAnyObjectByType<UIManager>(); // O usa UIManager.Instance
        if (uiManager != null && gameState.uiCanvasStates != null)
        {
            // Convertir de SerializableDictionary y cargar en UIManager
            Dictionary<string, bool> loadedCanvasStates = gameState.uiCanvasStates.ToDictionary();
            uiManager.LoadCanvasStates(loadedCanvasStates);
            LogMessage("Canvas states restored.");
        }
         else
        {
             LogMessage("WARN: UIManager not found or no canvas states saved, cannot restore canvas states.");
        }
    }
    // *** FIN NUEVOS MÉTODOS CANVAS ***


    // --- Métodos EnsurePlayerAndCameraExist, FindSpawnPoint, GetAllQuestIds, SetQuestCompleted (sin cambios) ---
    public void EnsurePlayerAndCameraExist() { /* ... */ }
    private Transform FindSpawnPoint(string tagName) { /* ... */ return null; } // Placeholder
    private string[] GetAllQuestIds(VillageQuestManager questManager) { /* ... */ return new string[0]; } // Placeholder
    private void SetQuestCompleted(VillageQuestManager questManager, string questId) { /* ... */ }

    // --- Método LogMessage (sin cambios) ---
    private void LogMessage(string message) { /* ... */ }
}