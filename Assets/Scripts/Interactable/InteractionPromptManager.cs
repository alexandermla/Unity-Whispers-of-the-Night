using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class InteractionPromptManager : MonoBehaviour
{
    // Referencias a componentes UI
    private GameObject promptPanel;
    private TextMeshProUGUI promptText;
    
    [SerializeField] private float checkRadius = 2f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private Transform playerCamera;
    
    // Nombres de objetos UI para buscar en la escena
    [SerializeField] private string interactPanelName = "InteractPanel";
    [SerializeField] private string interactTextName = "InteractText";
    
    private IInteractable currentInteractable;

    [System.Obsolete]
    private void Awake()
    {
        // Registrarse al evento de cambio de escena
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Inicializar referencias
        FindUIReferences();
    }

    [System.Obsolete]
    private void OnDestroy()
    {
        // Deregistrarse para evitar memory leaks
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [System.Obsolete]
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Dar tiempo para que todos los objetos se carguen
        //StartCoroutine(FindUIReferencesDelayed());
        //Debug.Log($"InteractionPromptManager: Actualizando referencias UI en escena {scene.name}");
        FindUIReferences();
    }

    [System.Obsolete]
    private IEnumerator FindUIReferencesDelayed()
    {
        // Esperar un frame para asegurar que todos los objetos estén cargados
        yield return null;
        FindUIReferences();
    }

    [System.Obsolete]
    private void FindUIReferences()
    {
        // Intentar encontrar Canvas en la escena
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        
        foreach (Canvas canvas in canvases)
        {
            // Buscar "InteractPanel" de forma recursiva
            Transform hudTransform = canvas.transform.Find("HUD");
            if (hudTransform != null)
            {
                Transform panelTransform = hudTransform.Find(interactPanelName);
                if (panelTransform != null)
                {
                    promptPanel = panelTransform.gameObject;
                    
                    // Buscar "InteractText" dentro del panel
                    Transform textTransform = panelTransform.Find(interactTextName);
                    if (textTransform != null)
                    {
                        promptText = textTransform.GetComponent<TextMeshProUGUI>();
                    }
                    
                    // Si no se encuentra directamente, buscar en toda la jerarquía
                    if (promptText == null)
                    {
                        promptText = promptPanel.GetComponentInChildren<TextMeshProUGUI>();
                    }
                    
                    // Ocultar el panel inicialmente
                    promptPanel.SetActive(false);
                    
                    Debug.Log($"InteractionPromptManager: Referencias UI encontradas correctamente");
                    return;
                }
            }
            
            // Si no encuentra en la estructura Canvas/HUD, buscar directamente en Canvas
            Transform directPanelTransform = canvas.transform.Find(interactPanelName);
            if (directPanelTransform != null)
            {
                promptPanel = directPanelTransform.gameObject;
                Transform textTransform = directPanelTransform.Find(interactTextName);
                if (textTransform != null)
                {
                    promptText = textTransform.GetComponent<TextMeshProUGUI>();
                }
                
                if (promptText == null)
                {
                    promptText = promptPanel.GetComponentInChildren<TextMeshProUGUI>();
                }
                
                promptPanel.SetActive(false);
                Debug.Log($"InteractionPromptManager: Referencias UI encontradas directamente en Canvas");
                return;
            }
        }
        
        // Método alternativo: buscar recursivamente todos los GameObjects con el nombre
        promptPanel = FindGameObjectRecursively(interactPanelName);
        
        if (promptPanel != null)
        {
            // Encontrar el texto
            Transform textTransform = promptPanel.transform.Find(interactTextName);
            if (textTransform != null)
            {
                promptText = textTransform.GetComponent<TextMeshProUGUI>();
            }
            
            if (promptText == null)
            {
                promptText = promptPanel.GetComponentInChildren<TextMeshProUGUI>();
            }
            
            promptPanel.SetActive(false);
            Debug.Log($"InteractionPromptManager: Referencias UI encontradas mediante búsqueda recursiva");
            return;
        }
        
        Debug.LogWarning($"InteractionPromptManager: No se encontró el panel '{interactPanelName}' en la escena actual");
        
        // Si no hay cámara del jugador asignada, intentar encontrarla
        if (playerCamera == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                playerCamera = mainCamera.transform;
            }
        }
    }
    
    // Método para encontrar un GameObject por nombre de forma recursiva
    private GameObject FindGameObjectRecursively(string name)
    {
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        
        foreach (GameObject rootObject in rootObjects)
        {
            // Buscar en este objeto
            if (rootObject.name.Equals(name))
                return rootObject;
                
            // Buscar en los hijos
            Transform found = FindRecursiveChild(rootObject.transform, name);
            if (found != null)
                return found.gameObject;
        }
        
        return null;
    }
    
    // Método auxiliar para búsqueda recursiva
    private Transform FindRecursiveChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(name))
                return child;
                
            Transform found = FindRecursiveChild(child, name);
            if (found != null)
                return found;
        }
        
        return null;
    }
    
    private void Update()
    {
        CheckForInteractables();
    }
    
    private void CheckForInteractables()
    {
        if (playerCamera == null) return;
        
        // Primero, buscar todos los interactuables cercanos con OverlapSphere
        Collider[] colliders = Physics.OverlapSphere(playerCamera.position, checkRadius, interactableLayer);
        
        // Si encontramos algún interactuable, organicemos por cercanía
        if (colliders.Length > 0)
        {
            IInteractable closestInteractable = null;
            float closestDistance = checkRadius;
            
            foreach (var collider in colliders)
            {
                IInteractable interactable = collider.GetComponent<IInteractable>();
                
                if (interactable != null)
                {
                    float distance = Vector3.Distance(playerCamera.position, collider.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestInteractable = interactable;
                    }
                }
            }
            
            // Si encontramos un interactuable cercano, mostrarlo
            if (closestInteractable != null)
            {
                ShowPrompt(closestInteractable.GetPromptMessage());
                currentInteractable = closestInteractable;
                return;
            }
        }
        
        // Si llegamos aquí, no hay interactables cerca
        HidePrompt();
        currentInteractable = null;
    }
    
    public void ShowPrompt(string message)
    {
        if (promptPanel != null && promptText != null)
        {
            promptText.text = message;
            promptPanel.SetActive(true);
        }
    }
    
    public void HidePrompt()
    {
        if (promptPanel != null)
        {
            promptPanel.SetActive(false);
        }
    }
    
    public void ShowTemporaryMessage(string message, float duration)
    {
        StartCoroutine(ShowMessageForDuration(message, duration));
    }

    private IEnumerator ShowMessageForDuration(string message, float duration)
    {
        ShowPrompt(message);
        yield return new WaitForSeconds(duration);
        HidePrompt();
    }
    
    // Método para obtener el interactable actual (útil para otros sistemas)
    public IInteractable GetCurrentInteractable()
    {
        return currentInteractable;
    }
    
    // Método para verificar si hay un interactable activo
    public bool HasActiveInteractable()
    {
        return currentInteractable != null;
    }
    
    // Método para interactuar con el objeto actual (puede ser llamado desde botones UI o input)
    public void InteractWithCurrent()
    {
        if (currentInteractable != null)
        {
            currentInteractable.Interact();
        }
    }
    
    // Dibujar gizmos para visualizar el radio de detección
    private void OnDrawGizmosSelected()
    {
        if (playerCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerCamera.position, checkRadius);
        }
    }
}