using UnityEngine;
using WoN.Interfaces;
using System.Collections;

public class LightInteractiveVines : MonoBehaviour, ILightReactive
{
    [SerializeField] private float timeToDestroy = 2.0f; // Tiempo de exposición necesario
    [SerializeField] private GameObject visualObject; // El objeto visual de las enredaderas
    [SerializeField] private Collider obstacleCollider; // El collider que bloquea el paso
    [SerializeField] private ParticleSystem burnParticles; // Partículas opcionales al quemarse
    [SerializeField] private AudioClip burnSound; // Sonido opcional
    [SerializeField] private Material dissolveMaterial; // Opcional: Material para efecto de disolución

    private float currentExposure = 0f;
    private bool isDestroyed = false;
    private AudioSource audioSource;
    private Renderer vinesRenderer;
    private Material materialInstance; // Para efecto dissolve

    private static readonly int DissolveAmountID = Shader.PropertyToID("_DissolveAmount");

    void Awake()
    {
        if (visualObject == null) visualObject = gameObject;
        if (obstacleCollider == null) obstacleCollider = GetComponent<Collider>();
        if (burnParticles != null) burnParticles.Stop();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && burnSound != null) audioSource = gameObject.AddComponent<AudioSource>();
        if (audioSource != null) audioSource.playOnAwake = false;

        vinesRenderer = visualObject.GetComponent<Renderer>();
        if(vinesRenderer != null && dissolveMaterial != null) {
            materialInstance = new Material(dissolveMaterial);
            vinesRenderer.material = materialInstance;
            materialInstance.SetFloat(DissolveAmountID, 0f); // Empezar sin disolver
        }
    }

    public void OnLightExposure(Transform lightSource = null, float intensity = 1.0f)
    {
        if (isDestroyed) return;

        currentExposure += intensity * Time.deltaTime;
        // Opcional: Añadir feedback visual mientras se expone (ej. brillo, humo leve)
        if(materialInstance != null) {
             // Podrías mapear currentExposure a alguna propiedad del shader aquí
        }


        if (currentExposure >= timeToDestroy)
        {
            StartCoroutine(DestroyVines());
        }
    }

    private IEnumerator DestroyVines()
    {
        isDestroyed = true;
        if (obstacleCollider != null) obstacleCollider.enabled = false; // Dejar de bloquear
        if (audioSource != null && burnSound != null) audioSource.PlayOneShot(burnSound);
        if (burnParticles != null) burnParticles.Play();

        // Animación de destrucción (ej. disolución)
        if (materialInstance != null) {
             float dissolveDuration = 0.8f;
             float elapsed = 0f;
             while(elapsed < dissolveDuration) {
                 elapsed += Time.deltaTime;
                 materialInstance.SetFloat(DissolveAmountID, Mathf.Lerp(0f, 1f, elapsed/dissolveDuration));
                 yield return null;
             }
             visualObject.SetActive(false); // Ocultar al final
        } else {
             // Si no hay disolución, simplemente desactivar después de un delay
             yield return new WaitForSeconds(0.5f);
             visualObject.SetActive(false);
        }
         // Opcional: Desactivar este script o el GameObject completo
         // gameObject.SetActive(false);
    }
}