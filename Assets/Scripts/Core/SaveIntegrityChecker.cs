using UnityEngine;
using System.Collections.Generic;

// Esta clase verificará la integridad del guardado/carga
public class SaveIntegrityChecker : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private bool logVerificationResults = true;
    [SerializeField] private bool autoVerifyOnSave = true;
    
    // Componentes que se deben verificar
    private List<string> requiredComponents = new List<string>
    {
        "Player",
        "MainCamera",
        "PlayerFlashlight",
        "StatueEvents",
        "LampSystem"
    };
    
    // Evento a suscribirse al iniciar
    private void Start()
    {
        if (GameStateManager.Instance != null && autoVerifyOnSave)
        {
            // Suscribirse a evento de guardado (tendrías que implementar esto en GameStateManager)
            // GameStateManager.Instance.OnGameSaved += VerifySaveIntegrity;
        }
    }
    
    // Verificar que todos los componentes críticos existen y están listos para ser guardados
    public bool VerifySaveIntegrity()
    {
        bool allComponentsFound = true;
        List<string> missingComponents = new List<string>();
        
        // Verificar jugador
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            allComponentsFound = false;
            missingComponents.Add("Player");
        }
        
        // Verificar cámara
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            allComponentsFound = false;
            missingComponents.Add("MainCamera");
        }
        
        // Verificar linterna
        GameObject flashlight = GameObject.FindGameObjectWithTag("PlayerFlashlight");
        if (flashlight == null || !flashlight.activeInHierarchy)
        {
            if (PlayerPrefs.GetInt("HasLampBeenPickedUp", 0) != 1)
            {
                allComponentsFound = false;
                missingComponents.Add("PlayerFlashlight");
            }
        }
        
        // Verificar StatueUI
        StatueUI statueUI = Object.FindAnyObjectByType<StatueUI>();
        if (statueUI == null)
        {
            allComponentsFound = false;
            missingComponents.Add("StatueUI");
        }
        
        // Verificar sistema de misiones
        VillageQuestManager questManager = Object.FindAnyObjectByType<VillageQuestManager>();
        if (questManager == null)
        {
            allComponentsFound = false;
            missingComponents.Add("QuestManager");
        }
        
        // Reportar resultados
        if (logVerificationResults)
        {
            if (allComponentsFound)
            {
                Debug.Log("[SaveIntegrityChecker] Todos los componentes críticos encontrados. Guardado seguro.");
            }
            else
            {
                Debug.LogWarning($"[SaveIntegrityChecker] Faltan componentes críticos: {string.Join(", ", missingComponents)}");
            }
        }
        
        return allComponentsFound;
    }
    
    // Verificar que la carga se realizó correctamente
    public bool VerifyLoadIntegrity()
    {
        // Implementación similar a VerifySaveIntegrity pero verificando 
        // que los datos cargados son coherentes
        return true;
    }
}