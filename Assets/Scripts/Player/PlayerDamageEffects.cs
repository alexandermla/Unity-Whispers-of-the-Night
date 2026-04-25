using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class PlayerDamageEffects : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Arrastra aquí el Material ('FullScreen_Mat') que usa tu Custom Pass de daño.")]
    [SerializeField] private Material damageEffectMaterial;
    [Tooltip("Arrastra aquí el GameObject del Panel UI de Game Over.")]
    [SerializeField] private GameObject gameOverPanel;
    [Tooltip("Opcional: AudioSource en el jugador.")]
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] private AudioClip playerHitSound;
    [SerializeField] private AudioClip heartbeatSound;
    private AudioSource heartbeatAudioSource;

    [Header("Configuración del Efecto")]
    [Tooltip("Duración (segundos) que el efecto visual permanece activo tras un golpe.")]
    [SerializeField] private float effectActiveDuration = 4.0f;
    [Tooltip("Tiempo (segundos) que tarda el efecto en aparecer (fade-in).")]
    [SerializeField] private float fadeInDuration = 0.15f;
    [Tooltip("Tiempo (segundos) que tarda el efecto en desaparecer (fade-out).")]
    [SerializeField] private float fadeOutDuration = 0.8f;

    [Header("Valores del Shader al Activar Efecto")]
    [Tooltip("Valor máximo para la intensidad de la viñeta durante el efecto.")]
    [SerializeField] private float targetVignetteIntensity = 0.8f; // Ajusta según tu shader
    // Eliminado: [SerializeField] private float targetNoiseIntensity = 0.5f;

    // Estado interno
    private bool isEffectActive = false;
    private float effectTimer = 0f;
    private Coroutine fadeCoroutine;

    // IDs de Propiedades del Shader (¡Solo las que existen!)
    // Asegúrate que el nombre "_VignetteIntensity" coincide con la Referencia en tu Shader Graph
    private static readonly int VignetteIntensityID = Shader.PropertyToID("_VignetteIntensity");
    // Eliminado: private static readonly int NoiseIntensityID = Shader.PropertyToID("_NoiseIntensity");


    void Start()
    {
        // Crear AudioSource para el latido del corazón
        heartbeatAudioSource = gameObject.AddComponent<AudioSource>();
        heartbeatAudioSource.loop = true;
        heartbeatAudioSource.playOnAwake = false;
        if (heartbeatSound != null)
        {
            heartbeatAudioSource.clip = heartbeatSound;
        }

        if (damageEffectMaterial != null)
        {
            // Poner la intensidad de la viñeta a 0 al inicio
            SetMaterialProperties(0f);
        }
        else
        {
            Debug.LogError("PlayerDamageEffects: ¡Material de efecto (damageEffectMaterial) no asignado! Desactivando script.", this);
            this.enabled = false;
            return;
        }

        

        if (playerAudioSource == null) playerAudioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (isEffectActive)
        {
            effectTimer -= Time.deltaTime;
            if (effectTimer <= 0)
            {
                isEffectActive = false;
                StartFade(false); // Iniciar fade out
                // Detener el sonido del corazón
                if (heartbeatAudioSource != null)
                {
                    heartbeatAudioSource.Stop();
                }
            }
        }
    }

    public void TakeHit()
    {
        if (isEffectActive)
        {
            TriggerGameOver();
        }
        else
        {
            isEffectActive = true;
            effectTimer = effectActiveDuration;
            if (playerAudioSource != null && playerHitSound != null)
            {
                playerAudioSource.PlayOneShot(playerHitSound);
            }
            // Iniciar el sonido del corazón en bucle después de un pequeño delay
            if (heartbeatAudioSource != null && heartbeatSound != null)
            {
                StartCoroutine(StartHeartbeatLoop(0.2f));
            }
            StartFade(true); // Iniciar fade in
        }
    }

    private void StartFade(bool fadeIn)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeMaterialProperties(fadeIn));
    }

    // Corutina para animar la propiedad del MATERIAL
    private IEnumerator FadeMaterialProperties(bool fadeIn)
    {
        if (damageEffectMaterial == null || !damageEffectMaterial.HasProperty(VignetteIntensityID))
        {
            // Si no hay material o no tiene la propiedad, no hacer nada
             Debug.LogWarning("FadeMaterialProperties: Material nulo o no tiene _VignetteIntensity");
             yield break;
        }

        float duration = fadeIn ? fadeInDuration : fadeOutDuration;
        float elapsed = 0f;

        // Obtener valor inicial actual
        float startVignette = damageEffectMaterial.GetFloat(VignetteIntensityID);
        // Establecer valor final objetivo
        float endVignette = fadeIn ? targetVignetteIntensity : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Interpolar y aplicar solo la viñeta
            damageEffectMaterial.SetFloat(VignetteIntensityID, Mathf.Lerp(startVignette, endVignette, t));
            yield return null;
        }

        // Asegurar valor final
        damageEffectMaterial.SetFloat(VignetteIntensityID, endVignette);
        fadeCoroutine = null;
    }

    // Helper para establecer las propiedades (ahora solo viñeta)
    private void SetMaterialProperties(float intensityRatio)
    {
        if (damageEffectMaterial == null) return;
        intensityRatio = Mathf.Clamp01(intensityRatio);

        if (damageEffectMaterial.HasProperty(VignetteIntensityID))
            damageEffectMaterial.SetFloat(VignetteIntensityID, Mathf.Lerp(0f, targetVignetteIntensity, intensityRatio));
    }

    // Activa la pantalla de Game Over
    private void TriggerGameOver()
    {
         if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
         // Detener el sonido del corazón
         if (heartbeatAudioSource != null)
         {
             heartbeatAudioSource.Stop();
         }
         // Asegurar que el efecto esté al máximo al morir
         SetMaterialProperties(1f);

        //Time.timeScale = 0f; // Pausar

        if (gameOverPanel != null)
        {
            //gameOverPanel.SetActive(true);
            //Debug.Log("Game Over Panel Activado");
            GameOverController goc = gameOverPanel.GetComponent<GameOverController>();
            if (goc != null) goc.Show();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        this.enabled = false;
         Debug.Log("Game Over Triggered");
    }

    private IEnumerator StartHeartbeatLoop(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (heartbeatAudioSource != null)
        {
            heartbeatAudioSource.Play();
        }
    }
}