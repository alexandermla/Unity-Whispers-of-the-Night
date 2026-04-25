using System.Collections;
using UnityEngine;

public class StatueCollectable : MonoBehaviour, IInteractable
{
    [Header("Identificación")]
    [SerializeField] private string statueId; // "BEAR", "MOUSE", "DOG", "RACCOON"
    [SerializeField] private string displayName; // Nombre para mostrar
    
    [Header("Visuales")]
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material highlightMaterial;
    [SerializeField] private float highlightIntensity = 1.5f;
    [SerializeField] private float pulseSpeed = 2.0f;
    [SerializeField] private float rotationSpeed = 30f;
    [SerializeField] private float bobSpeed = 1f;
    [SerializeField] private float bobHeight = 0.2f;
    
    [Header("Efectos de Recolección")]
    [SerializeField] private float collectionAnimationDuration = 2.0f;
    [SerializeField] private ParticleSystem collectionParticles;
    [SerializeField] private AudioClip collectionSound;
    [SerializeField] private Light statueLight;
    
    // Estado
    private bool isCollected = false;
    private bool isPlayerNearby = false;
    private AudioSource audioSource;
    private Vector3 startPosition;
    private Renderer statueRenderer;
    private float bobTime = 0f;
    
    private void Awake()
    {
        // Obtener componentes
        statueRenderer = GetComponent<Renderer>();
        audioSource = GetComponent<AudioSource>();
        
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Guardar posición inicial
        startPosition = transform.position;
    }
    
    private void Start()
    {
        // Verificar si ya fue recolectada
        if (StatueEvents.IsStatueCollected(statueId))
        {
            isCollected = true;
            gameObject.SetActive(false);
            return;
        }
        
        // Inicializar material
        if (statueRenderer != null && normalMaterial != null)
        {
            statueRenderer.material = normalMaterial;
        }
    }
    
    private void Update()
    {
        if (isCollected) return;
        
        // Rotación
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        
        // Efecto de flotar
        bobTime += Time.deltaTime;
        float newY = startPosition.y + Mathf.Sin(bobTime * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        
        // Efecto de highlight cuando el jugador está cerca
        if (isPlayerNearby && statueRenderer != null && highlightMaterial != null)
        {
            // Si no tiene ya el material de highlight, asignarlo
            if (statueRenderer.material != highlightMaterial)
            {
                statueRenderer.material = highlightMaterial;
            }
            
            // Efecto de pulso en emisivo
            float pulseValue = Mathf.PingPong(Time.time * pulseSpeed, 1f);
            float emission = Mathf.Lerp(0.5f, highlightIntensity, pulseValue);
            
            if (highlightMaterial.HasProperty("_EmissionColor"))
            {
                Color baseColor = highlightMaterial.GetColor("_EmissionColor");
                statueRenderer.material.SetColor("_EmissionColor", baseColor * emission);
            }
        }
    }
    
    // Implementación IInteractable
    public string GetPromptMessage()
    {
        if (isCollected) return string.Empty;
        return $"Presiona E para recoger la estatua de {displayName}";
    }
    
    public void Interact()
    {
        if (isCollected) return;
        
        // Iniciar secuencia de recolección
        StartCoroutine(CollectSequence());
    }
    
    private IEnumerator CollectSequence()
    {
        isCollected = true;
        
        // Reproducir sonido
        if (audioSource != null && collectionSound != null)
        {
            audioSource.PlayOneShot(collectionSound);
        }
        
        // Activar partículas
        if (collectionParticles != null)
        {
            collectionParticles.Play();
        }
        
        // Animación
        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        
        while (elapsed < collectionAnimationDuration)
        {
            float t = elapsed / collectionAnimationDuration;
            
            // Subir y rotar
            transform.position = new Vector3(
                startPos.x,
                startPos.y + t * 1.5f,
                startPos.z
            );
            
            // Reducir tamaño
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            
            // Rotar más rápido
            transform.Rotate(Vector3.up, Time.deltaTime * 180f);
            
            // Ajustar intensidad de luz
            if (statueLight != null)
            {
                statueLight.intensity = Mathf.Lerp(2f, 0f, t);
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Notificar recolección mediante el sistema de eventos
        StatueEvents.StatueCollected(statueId, startPos);
        
        // Desactivar gameObject
        gameObject.SetActive(false);
    }
    
    // Métodos para detección de proximidad del jugador
    public void OnPlayerEnter()
    {
        isPlayerNearby = true;
        
        // Cambiar a material highlight
        if (statueRenderer != null && highlightMaterial != null)
        {
            statueRenderer.material = highlightMaterial;
        }
    }
    
    public void OnPlayerExit()
    {
        isPlayerNearby = false;
        
        // Regresar a material normal
        if (statueRenderer != null && normalMaterial != null)
        {
            statueRenderer.material = normalMaterial;
        }
    }
    
    // Este método se usará para detección de proximidad
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            OnPlayerEnter();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            OnPlayerExit();
        }
    }
}