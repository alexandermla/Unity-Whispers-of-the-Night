using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class ExitDoorTrigger : MonoBehaviour
{
    [SerializeField] private TutorialQuestManager questManager;
    [SerializeField] private float transitionDelay = 1.5f; // Tiempo antes de cambiar de escena
    [SerializeField] private GameObject fadeOutPanel; // Panel opcional para efecto de fade out
    
    private bool hasTriggered = false;
    private static ExitDoorTrigger instance; // Instancia estática para asegurar que los eventos funcionen
    
    private void Awake()
    {
        // Mantener instancia para asegurar que los eventos funcionen
        instance = this;
        
        Debug.Log("ExitDoorTrigger inicializado y listo para manejar transiciones de escena");
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        
        if (other.CompareTag("Player"))
        {
            hasTriggered = true;
            
            // Notificar al sistema de misiones que se alcanzó la puerta de salida
            if (questManager != null)
            {
                questManager.TriggerReachedExit();
            }
            
            // Iniciar la transición a la siguiente escena
            StartCoroutine(TransitionToNextScene());
        }
    }
    
    private IEnumerator TransitionToNextScene()
    {
        Debug.Log("Reached exit door. Transitioning to mainSceneQuest...");
        
        // Si tenemos un panel de fade out, lo activamos gradualmente
        if (fadeOutPanel != null)
        {
            fadeOutPanel.SetActive(true);
            CanvasGroup canvasGroup = fadeOutPanel.GetComponent<CanvasGroup>();
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0;
                float elapsedTime = 0;
                
                while (elapsedTime < transitionDelay)
                {
                    canvasGroup.alpha = elapsedTime / transitionDelay;
                    elapsedTime += Time.deltaTime;
                    yield return null;
                }
                
                canvasGroup.alpha = 1;
            }
            else
            {
                yield return new WaitForSeconds(transitionDelay);
            }
        }
        else
        {
            // Si no hay panel de fade, solo esperamos
            yield return new WaitForSeconds(transitionDelay);
        }
        
        // Cargar la siguiente escena
        SceneManager.LoadScene("mainSceneQuest");
    }
}