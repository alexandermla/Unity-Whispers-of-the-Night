using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Runtime.Serialization.Formatters.Binary; // Asegúrate que esté

[Serializable]
public class GameData
{
    // --- Datos Guardados ---
    public bool fireflyFused;
    public bool[] collectedStatues = new bool[4]; // bear, mouse, dog, raccoon
    public bool hasLampBeenPickedUp; // <-- NUEVO

    // Metadata
    public string saveDateTime;
    public int saveVersion = 3; // <-- Incrementar versión
}

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }
    private const string SAVE_KEY = "game_save_v3"; // <-- Cambiar key
    private const string SAVE_FILENAME = "gamesave_v3.json"; // <-- Cambiar nombre
    private const int CURRENT_SAVE_VERSION = 3;
    public const string FIREFLY_FUSED_PREF_KEY = "FireflyFusedState";
    public const string LAMP_PICKED_UP_PREF_KEY = "HasLampBeenPickedUp"; // <-- NUEVA Key

    [SerializeField] private bool usePlayerPrefs = true;
    [SerializeField] private bool useFileSystem = false;
    [SerializeField] private bool encryptData = false;
    [SerializeField] private string encryptionKey = "your-simple-key";

    public event Action OnGameSaved;
    public event Action OnGameLoaded;

    private GameData currentGameData = new GameData();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadGame();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "mainSceneQuest" || scene.name == "TimmyHouse_Tutorial") // Aplicar también en tutorial
        {
            StartCoroutine(ApplyLoadedDataAfterDelay(0.2f));
        }
    }

    public void SaveGame()
    {
        // Permitir guardar en ambas escenas ahora
        if (SceneManager.GetActiveScene().name != "mainSceneQuest" && SceneManager.GetActiveScene().name != "TimmyHouse_Tutorial")
        {
            Debug.Log($"SaveSystem: Solo se puede guardar en mainSceneQuest o TimmyHouse_Tutorial (estamos en {SceneManager.GetActiveScene().name})");
            return;
        }

        try
        {
            GameData data = new GameData();
            data.saveDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            data.saveVersion = CURRENT_SAVE_VERSION;

            CollectCoreData(data); // Recolecta fusión, estatuas y lámpara

            string jsonData = JsonUtility.ToJson(data, true);
            if (encryptData) jsonData = EncryptDecrypt(jsonData);

            if (usePlayerPrefs) { PlayerPrefs.SetString(SAVE_KEY, jsonData); PlayerPrefs.Save(); }
            if (useFileSystem) { string savePath = Path.Combine(Application.persistentDataPath, SAVE_FILENAME); File.WriteAllText(savePath, jsonData); }

            currentGameData = data;
            Debug.Log($"Juego guardado. Fused: {data.fireflyFused}, LampUp: {data.hasLampBeenPickedUp}, Statues: {string.Join(",", data.collectedStatues)}");
            OnGameSaved?.Invoke();
        }
        catch (System.Exception e) { Debug.LogError($"Error al guardar: {e.Message}\n{e.StackTrace}"); }
    }

    private void CollectCoreData(GameData data)
    {
        // 1. Fusión Luciérnaga
        data.fireflyFused = PlayerPrefs.GetInt(FIREFLY_FUSED_PREF_KEY, 0) == 1;
        // 2. Linterna Recogida
        data.hasLampBeenPickedUp = PlayerPrefs.GetInt(LAMP_PICKED_UP_PREF_KEY, 0) == 1; // <-- NUEVO
        // 3. Estatuas
        string[] statueIds = { "BEAR", "MOUSE", "DOG", "RACCOON" };
        for (int i = 0; i < statueIds.Length; i++) {
            if (i < data.collectedStatues.Length) {
                data.collectedStatues[i] = StatueEvents.IsStatueCollected(statueIds[i]);
            }
        }
    }

    public void LoadGame()
    {
        try {
            string jsonData = null;
            if (usePlayerPrefs && PlayerPrefs.HasKey(SAVE_KEY)) { jsonData = PlayerPrefs.GetString(SAVE_KEY); }
            else if (useFileSystem) { string savePath = Path.Combine(Application.persistentDataPath, SAVE_FILENAME); if (File.Exists(savePath)) { jsonData = File.ReadAllText(savePath); if (usePlayerPrefs && !string.IsNullOrEmpty(jsonData)) { PlayerPrefs.SetString(SAVE_KEY, jsonData); PlayerPrefs.Save(); } } }

            if (string.IsNullOrEmpty(jsonData)) {
                Debug.Log("No hay datos guardados (V3). Usando estado por defecto.");
                currentGameData = new GameData();
                UpdatePlayerPrefsState(currentGameData);
                return;
            }

            if (encryptData) jsonData = EncryptDecrypt(jsonData);
            GameData loadedData = JsonUtility.FromJson<GameData>(jsonData);

            if (loadedData.saveVersion != CURRENT_SAVE_VERSION) {
                Debug.LogWarning($"Versión de guardado diferente ({loadedData.saveVersion} vs {CURRENT_SAVE_VERSION}).");
                // Aquí lógica de migración si es necesario
            }

            currentGameData = loadedData;
            Debug.Log($"Datos cargados (V3). Fused: {currentGameData.fireflyFused}, LampUp: {currentGameData.hasLampBeenPickedUp}, Statues: {string.Join(",", currentGameData.collectedStatues)}");
            UpdatePlayerPrefsState(currentGameData); // Actualizar PlayerPrefs inmediatamente

        } catch (System.Exception e) {
            Debug.LogError($"Error al cargar: {e.Message}. Usando estado por defecto.");
            currentGameData = new GameData();
            UpdatePlayerPrefsState(currentGameData);
        }
    }

    private System.Collections.IEnumerator ApplyLoadedDataAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ApplyLoadedData();
    }

    private void ApplyLoadedData()
    {
        if (currentGameData == null) return;
        // Solo aplicar si estamos en una escena relevante
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene != "mainSceneQuest" && currentScene != "TimmyHouse_Tutorial") return;

        try {
            RestoreCoreState(currentGameData);
            Debug.Log($"Estado del juego restaurado en {currentScene}.");
            OnGameLoaded?.Invoke();
        } catch (System.Exception e) { Debug.LogError($"Error al aplicar datos: {e.Message}\n{e.StackTrace}"); }
    }

    // Actualiza PlayerPrefs con el estado en memoria
    private void UpdatePlayerPrefsState(GameData data)
    {
        if (data == null) return;
        PlayerPrefs.SetInt(FIREFLY_FUSED_PREF_KEY, data.fireflyFused ? 1 : 0);
        PlayerPrefs.SetInt(LAMP_PICKED_UP_PREF_KEY, data.hasLampBeenPickedUp ? 1 : 0); // <-- NUEVO
        PlayerPrefs.Save();
        //Debug.Log($"PlayerPrefs actualizado: Fused={PlayerPrefs.GetInt(FIREFLY_FUSED_PREF_KEY)}, LampUp={PlayerPrefs.GetInt(LAMP_PICKED_UP_PREF_KEY)}");
    }

    // Restaura el estado de los sistemas basado en GameData
    private void RestoreCoreState(GameData data)
    {
        if (data == null) return;
        UpdatePlayerPrefsState(data); // Actualiza Prefs para que otros lean
        StatueEvents.LoadState(data.collectedStatues); // Carga estado estático de estatuas
        // Otros scripts (Lamp, Firefly, Tutorial, StatueUI) leerán de PlayerPrefs o reaccionarán a OnGameLoaded
    }

    // Encriptación
    private string EncryptDecrypt(string data) {
        if (string.IsNullOrEmpty(encryptionKey)) return data;
        char[] result = new char[data.Length];
        for (int i = 0; i < data.Length; i++) result[i] = (char)(data[i] ^ encryptionKey[i % encryptionKey.Length]);
        return new string(result);
    }

    public void DeleteSaveData()
    {
        if (usePlayerPrefs && PlayerPrefs.HasKey(SAVE_KEY)) PlayerPrefs.DeleteKey(SAVE_KEY);
        if (useFileSystem) { string savePath = Path.Combine(Application.persistentDataPath, SAVE_FILENAME); if (File.Exists(savePath)) File.Delete(savePath); }

        currentGameData = new GameData(); // Resetear memoria
        // --- Borrar claves específicas de PlayerPrefs ---
        PlayerPrefs.DeleteKey(FIREFLY_FUSED_PREF_KEY);
        PlayerPrefs.DeleteKey(LAMP_PICKED_UP_PREF_KEY); // <-- NUEVO
        // ----------------------------------------------
        PlayerPrefs.Save(); // Guardar borrado de Prefs

        // Resetear también el estado estático de las estatuas
        StatueEvents.ResetStatues();

        Debug.Log("Datos de guardado (V3) y estados relacionados eliminados.");
    }

    public bool HasSaveData()
    {
        if (usePlayerPrefs && PlayerPrefs.HasKey(SAVE_KEY)) return true;
        if (useFileSystem) { string savePath = Path.Combine(Application.persistentDataPath, SAVE_FILENAME); return File.Exists(savePath); }
        return false;
    }

    // Getters (sin cambios, pero podrías añadir uno para hasLampBeenPickedUp si lo necesitas)
    public bool GetLoadedFireflyFusedState() => currentGameData?.fireflyFused ?? false;
    public bool IsStatueCollectedInLoadedData(string statueId) {
        if (currentGameData == null) return false;
        string[] statueIds = { "BEAR", "MOUSE", "DOG", "RACCOON" };
        int index = Array.IndexOf(statueIds, statueId);
        return index >= 0 && index < currentGameData.collectedStatues.Length && currentGameData.collectedStatues[index];
    }

    // Backup (sin cambios)
    public void CreateBackup() {
        if (!useFileSystem) return;
        string savePath = Path.Combine(Application.persistentDataPath, SAVE_FILENAME);
        if (File.Exists(savePath)) { string backupFilename = $"backup_v3_{DateTime.Now:yyyyMMdd_HHmmss}.json"; string backupPath = Path.Combine(Application.persistentDataPath, backupFilename); try { File.Copy(savePath, backupPath); Debug.Log($"Backup creado en: {backupPath}"); } catch (Exception e) { Debug.LogError($"Error al crear backup: {e.Message}"); } } else { Debug.Log("No hay archivo de guardado V3 para hacer backup."); }
    }
}