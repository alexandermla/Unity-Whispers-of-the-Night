// GameInitializer.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameInitializer : MonoBehaviour
{
    [SerializeField] private string firstSceneToLoad = "MainMenu";
    
    private void Start()
    {
        // 1. Inicializar sistemas globales
        InitializeGlobalSystems();
        
        // 2. Cargar primera escena real
        LoadFirstGameScene();
    }
    
    private void InitializeGlobalSystems()
    {
        // Crear objeto para los managers
        GameObject globalManagers = new GameObject("GameManagers");
        
        // Añadir los managers necesarios
        GameManager gameManager = globalManagers.AddComponent<GameManager>();
        GameStateManager stateManager = globalManagers.AddComponent<GameStateManager>();
        
        // Configurar referencias de los prefabs del jugador y la cámara
        if (Resources.Load<GameObject>("Prefabs/Player") != null)
        {
            stateManager.SetPlayerPrefab(Resources.Load<GameObject>("Prefabs/Player"));
        }
        
        if (Resources.Load<GameObject>("Prefabs/MainCamera") != null)
        {
            stateManager.SetCameraPrefab(Resources.Load<GameObject>("Prefabs/MainCamera"));
        }
        
        DontDestroyOnLoad(globalManagers);
        Debug.Log("Global systems initialized successfully");
    }
    
    private void LoadFirstGameScene()
    {
        // Cargar la primera escena jugable (asíncrono para permitir transición)
        SceneManager.LoadSceneAsync(firstSceneToLoad);
    }
}