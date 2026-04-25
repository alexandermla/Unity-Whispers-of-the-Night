using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class FireflyFusion : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private FireflyGuide firefly;
    [SerializeField] private LampSystem playerLamp;
    
    [Header("Efectos de Fusión")]
    [SerializeField] private ParticleSystem fusionParticles;
    [SerializeField] private AudioClip fusionSound;
    [SerializeField] private Light fusionLight;
    [SerializeField] private Color fusionColor = new Color(0.5f, 0.9f, 1f);
    [SerializeField] private float fusionDuration = 3.0f;
    
    [Header("Animación")]
    [SerializeField] private AnimationCurve intensityCurve;
    [SerializeField] private AnimationCurve sizeCurve;
    
    [Header("Eventos")]
    [SerializeField] private UnityEvent onFusionStarted;
    [SerializeField] private UnityEvent onFusionCompleted;
    
    private AudioSource audioSource;
    private bool fusionInProgress = false;
    
    private void Awake()
    {
        // Inicializar audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Encontrar referencias si no están asignadas
        if (firefly == null)
        {
            firefly = Object.FindAnyObjectByType<FireflyGuide>();
        }
        
        if (playerLamp == null)
        {
            GameObject lampObject = GameObject.FindWithTag("PlayerFlashlight");
            if (lampObject != null)
            {
                playerLamp = lampObject.GetComponent<LampSystem>();
            }
        }
    }
    
    public void StartFusion()
    {
        if (fusionInProgress)
        {
            Debug.LogWarning("FireflyFusion: Ya hay una fusión en progreso");
            return;
        }
        
        if (firefly == null)
        {
            Debug.LogError("FireflyFusion: No hay referencia a la luciérnaga");
            return;
        }
        
        if (playerLamp == null)
        {
            Debug.LogError("FireflyFusion: No hay referencia al sistema de lámpara");
            return;
        }
        
        fusionInProgress = true;
        StartCoroutine(FusionSequence());
    }
    
    private IEnumerator FusionSequence()
    {
        Debug.Log("Iniciando secuencia de fusión de luciérnaga");
        
        // Invocar evento de inicio
        onFusionStarted?.Invoke();
        
        // Posición de la lámpara como destino
        Vector3 startPosition = firefly.transform.position;
        Vector3 lampPosition = playerLamp.transform.position;
        
        // Reproducir sonido
        if (audioSource != null && fusionSound != null)
        {
            audioSource.PlayOneShot(fusionSound);
        }
        
        // Activar partículas
        if (fusionParticles != null)
        {
            fusionParticles.Play();
        }
        
        // Activar luz de fusión
        if (fusionLight != null)
        {
            fusionLight.color = fusionColor;
            fusionLight.enabled = true;
        }
        
        // Animación de fusión
        float elapsed = 0f;
        while (elapsed < fusionDuration)
        {
            float t = elapsed / fusionDuration;
            
            // Posición con easing
            firefly.transform.position = Vector3.Lerp(startPosition, lampPosition, Mathf.SmoothStep(0, 1, t));
            
            // Tamaño
            float size = sizeCurve.Evaluate(t);
            firefly.transform.localScale = Vector3.one * size;
            
            // Intensidad de luz
            if (fusionLight != null)
            {
                fusionLight.intensity = intensityCurve.Evaluate(t);
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (playerLamp != null)
        {
            playerLamp.RechargeEnergy();
            Debug.Log("Lámpara recargada al 100%");
        }
        
        // Notificar al sistema de quest
        VillageQuestManager questManager = Object.FindAnyObjectByType<VillageQuestManager>();
        if (questManager != null)
        {
            questManager.TriggerEvent("FIREFLY_FUSION_COMPLETE");
        }
        
        // Invocar evento de completado
        onFusionCompleted?.Invoke();
        
        // Limpiar
        fusionInProgress = false;
        
        // Desactivar luciérnaga
        firefly.gameObject.SetActive(false);
        
        // Recargar lámpara
        
        
        Debug.Log("Secuencia de fusión completada");
    }
}