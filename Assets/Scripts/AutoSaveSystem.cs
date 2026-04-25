using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Sistema de auto-guardado que complementa al SaveSystem principal.
/// Permite configurar guardados automáticos basados en tiempo o eventos.
/// </summary>
public class AutoSaveSystem : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private bool enableAutoSave = true;
    [SerializeField] private float autoSaveInterval = 300f; // 5 minutos por defecto
    [SerializeField] private bool saveOnLevelComplete = true;
    [SerializeField] private bool saveOnStatueCollected = true;
    [SerializeField] private bool saveOnQuestComplete = true;
    [SerializeField] private float minTimeBetweenSaves = 30f; // Evitar guardados demasiado frecuentes
    
    [Header("UI")]
    [SerializeField] private GameObject autoSaveIndicator;
    [SerializeField] private float indicatorDuration = 2f;
    
    // Referencias
    private SaveSystem saveSystem;
    private VillageQuestManager questManager;
    
    // Control interno
    private float lastSaveTime;
    private Coroutine autoSaveCoroutine;
    private Coroutine indicatorCoroutine;

    private void Awake()
    {
        // Inicializar
        lastSaveTime = Time.time;
    }

    [Obsolete]
    private void Start()
    {
        // Buscar referencias necesarias
        FindReferences();
        
        // Configurar listeners de eventos
        SetupEventListeners();
        
        // Iniciar rutina de auto-guardado
        if (enableAutoSave && autoSaveInterval > 0)
        {
            autoSaveCoroutine = StartCoroutine(AutoSaveRoutine());
        }
        
        // Ocultar indicador al inicio
        if (autoSaveIndicator != null)
        {
            autoSaveIndicator.SetActive(false);
        }
    }

    [Obsolete]
    private void FindReferences()
    {
        // Encontrar el sistema principal de guardado
        saveSystem = FindObjectOfType<SaveSystem>();
        if (saveSystem == null)
        {
            Debug.LogError("AutoSaveSystem: No se encontró SaveSystem en la escena");
            enableAutoSave = false;
            return;
        }
        
        // Encontrar el gestor de misiones
        questManager = FindObjectOfType<VillageQuestManager>();
    }

    [Obsolete]
    private void SetupEventListeners()
    {
        if (questManager != null && saveOnQuestComplete)
        {
            // Suscribirse al evento de cambio de estado de misiones
            questManager.OnQuestStatusChanged += OnQuestStatusChanged;
        }
    }

    [Obsolete]
    private void OnQuestStatusChanged(string questId, bool isActive, bool isCompleted)
    {
        // Si una misión se completó y está habilitado el guardado en este evento
        if (isCompleted && saveOnQuestComplete)
        {
            // Intentar guardar
            TrySave("completar misión: " + questId);
        }
        
        // Si se recolectó una estatua
        if (questId.StartsWith("STATUE_") && questId.EndsWith("_COLLECTED") && saveOnStatueCollected)
        {
            TrySave("recolectar estatua");
        }
    }

    [Obsolete]
    private IEnumerator AutoSaveRoutine()
    {
        // Esperar un tiempo inicial para evitar guardar justo al inicio
        yield return new WaitForSeconds(autoSaveInterval / 2);
        
        while (enabled && enableAutoSave)
        {
            // Solo guardar si estamos en la escena correcta y ha pasado suficiente tiempo
            if (CanSaveNow())
            {
                if (saveSystem != null)
                {
                    SaveGame("rutina automática");
                }
            }
            
            // Esperar hasta el próximo guardado
            yield return new WaitForSeconds(autoSaveInterval);
        }
    }
    
    private bool CanSaveNow()
    {
        // Verificar que estamos en la escena principal
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "mainSceneQuest")
        {
            return false;
        }
        
        // Verificar tiempo mínimo entre guardados
        if (Time.time - lastSaveTime < minTimeBetweenSaves)
        {
            return false;
        }
        
        // Verificar que no está pausado
        if (Time.timeScale <= 0.01f)
        {
            return false;
        }
        
        return true;
    }

    [Obsolete]
    private void TrySave(string reason)
    {
        if (CanSaveNow())
        {
            SaveGame(reason);
        }
    }

    [Obsolete]
    private void SaveGame(string reason)
    {
        saveSystem.SaveGame();
        lastSaveTime = Time.time;
        
        ShowSaveIndicator();
        
        Debug.Log($"AutoSaveSystem: Juego guardado automáticamente ({reason})");
    }
    
    private void ShowSaveIndicator()
    {
        if (autoSaveIndicator == null) return;
        
        // Detener indicador anterior si existe
        if (indicatorCoroutine != null)
        {
            StopCoroutine(indicatorCoroutine);
        }
        
        indicatorCoroutine = StartCoroutine(ShowSaveIndicatorTemporarily());
    }
    
    private IEnumerator ShowSaveIndicatorTemporarily()
    {
        autoSaveIndicator.SetActive(true);
        
        // Animación de fade si tiene CanvasGroup
        CanvasGroup canvasGroup = autoSaveIndicator.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            // Fade in
            canvasGroup.alpha = 0;
            float fadeTime = 0.3f;
            float elapsed = 0;
            
            while (elapsed < fadeTime)
            {
                canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / fadeTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            canvasGroup.alpha = 1;
            
            // Mantener visible
            yield return new WaitForSeconds(indicatorDuration);
            
            // Fade out
            elapsed = 0;
            while (elapsed < fadeTime)
            {
                canvasGroup.alpha = Mathf.Lerp(1, 0, elapsed / fadeTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            // Sin animación
            yield return new WaitForSeconds(indicatorDuration);
        }
        
        autoSaveIndicator.SetActive(false);
    }

    [Obsolete]
    public void ForceAutoSave()
    {
        // Método público para forzar un guardado desde otro script
        if (saveSystem != null)
        {
            TrySave("guardado forzado");
        }
    }

    [Obsolete]
    private void OnDestroy()
    {
        // Limpiar suscripciones a eventos
        if (questManager != null)
        {
            questManager.OnQuestStatusChanged -= OnQuestStatusChanged;
        }
    }
}