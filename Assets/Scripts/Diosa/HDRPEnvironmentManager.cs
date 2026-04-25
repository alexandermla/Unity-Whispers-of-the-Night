using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[ExecuteInEditMode]
public class AtmosphereManager : MonoBehaviour
{
    [Header("Referencias")]
    public Volume globalVolume;
    public Light directionalLight;
    
    [Header("Configuración General")]
    [Range(0, 1)]
    public float darknessLevel = 0.85f;
    [Range(0, 3)]
    public float ambientIntensity = 0.1f;
    
    [Header("Niebla")]
    public bool enableFog = true;
    [Range(0, 500)]
    public float fogDistance = 50f;
    public Color fogColor = new Color(0.05f, 0.05f, 0.1f, 1f);
    [Range(0, 1)]
    public float fogDensity = 0.7f;
    
    [Header("Ambiente")]
    public Color ambientColor = new Color(0.05f, 0.05f, 0.12f, 1f);
    public Color skyColor = new Color(0.02f, 0.02f, 0.05f, 1f);
    
    [Header("Efectos Visuales")]
    [Range(0, 1)]
    public float vignetteIntensity = 0.4f;
    [Range(-100, 0)]
    public float exposureCompensation = -2.0f;
    [Range(0, 2)]
    public float contrast = 1.1f;
    
    // Para modificar en tiempo de ejecución
    private Fog fogVolume;
    private PhysicallyBasedSky pbsSky;
    private GradientSky gradientSky;
    private Vignette vignette;
    private Exposure exposure;
    private ColorAdjustments colorAdjustments;
    private HDAdditionalLightData hdLight;
    
    void OnEnable()
    {
        UpdateAtmosphere();
    }
    
    void Update()
    {
        // Si está en modo edición, actualizar constantemente 
        if (!Application.isPlaying)
        {
            UpdateAtmosphere();
        }
    }
    
    // Botón para aplicar cambios en el editor
    [ContextMenu("Update Atmosphere")]
    public void UpdateAtmosphere()
    {
        if (globalVolume == null)
        {
            Debug.LogWarning("No se ha asignado un Volume. Por favor, asigna uno.");
            return;
        }
        
        // Obtener o crear componentes del volumen
        GetOrCreateVolumeComponents();
        
        // Configurar luz direccional
        SetupDirectionalLight();
        
        // Configurar niebla
        SetupFog();
        
        // Configurar cielo
        SetupSky();
        
        // Configurar efectos de post-procesado
        SetupPostProcessing();
    }
    
    void GetOrCreateVolumeComponents()
    {
        VolumeProfile profile = globalVolume.sharedProfile;
        if (profile == null)
        {
            Debug.LogWarning("El Volume no tiene un Profile asignado.");
            return;
        }
        
        // Obtener o añadir los componentes necesarios
        if (!profile.TryGet(out fogVolume))
            fogVolume = profile.Add<Fog>(false);
            
        if (!profile.TryGet(out pbsSky))
            pbsSky = profile.Add<PhysicallyBasedSky>(false);
            
        if (!profile.TryGet(out gradientSky))
            gradientSky = profile.Add<GradientSky>(false);
            
        if (!profile.TryGet(out vignette))
            vignette = profile.Add<Vignette>(false);
            
        if (!profile.TryGet(out exposure))
            exposure = profile.Add<Exposure>(false);
            
        if (!profile.TryGet(out colorAdjustments))
            colorAdjustments = profile.Add<ColorAdjustments>(false);
    }
    
    void SetupDirectionalLight()
    {
        if (directionalLight == null)
            return;
            
        hdLight = directionalLight.GetComponent<HDAdditionalLightData>();
        if (hdLight == null)
            hdLight = directionalLight.gameObject.AddComponent<HDAdditionalLightData>();
            
        // Configurar luz principal
        float lightIntensity = 1000f * (1f - darknessLevel);
        hdLight.intensity = lightIntensity;
        hdLight.color = ambientColor;
        
        // Sombras
        hdLight.EnableShadows(true);
        hdLight.SetShadowResolution(512);
        hdLight.shadowDimmer = 0.6f;
        hdLight.volumetricShadowDimmer = 0.6f;
    }
    
    void SetupFog()
    {
        if (fogVolume == null)
            return;
            
        fogVolume.active = enableFog;
        fogVolume.enableVolumetricFog.Override(true);
        
        // Convertir el valor de distancia inversa a meanFreePath
        float meanFreePath = Mathf.Max(1f, fogDistance * (1f - fogDensity));
        fogVolume.meanFreePath.Override(meanFreePath);
        
        fogVolume.albedo.Override(fogColor);
        fogVolume.anisotropy.Override(0.6f);
        fogVolume.globalLightProbeDimmer.Override(0.5f * (1f - darknessLevel));
        fogVolume.depthExtent.Override(50f);
    }
    
    void SetupSky()
    {
        // Desactivamos PhysicallyBasedSky y activamos GradientSky para un ambiente más estilizado
        if (pbsSky != null && gradientSky != null)
        {
            pbsSky.active = false;
            gradientSky.active = true;
            
            // Configurar gradiente
            gradientSky.top.Override(skyColor * 0.5f);  // Color más oscuro arriba
            gradientSky.middle.Override(skyColor);       // Color base en medio
            gradientSky.bottom.Override(fogColor);       // Color de la niebla abajo
            
            // Ajustar exposición del cielo
            float skyExposure = -2f * darknessLevel;
            gradientSky.exposure.Override(skyExposure);
            gradientSky.gradientDiffusion.Override(1f);
        }
    }
    
    void SetupPostProcessing()
    {
        // Viñeta (oscurece los bordes)
        if (vignette != null)
        {
            vignette.active = true;
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(0.4f);
            vignette.color.Override(Color.black);
        }
        
        // Exposición (controla el brillo general)
        if (exposure != null)
        {
            exposure.active = true;
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(exposureCompensation);
        }
        
        // Ajustes de color (contraste, saturación, etc.)
        if (colorAdjustments != null)
        {
            colorAdjustments.active = true;
            colorAdjustments.contrast.Override(contrast * 10f);
            colorAdjustments.saturation.Override(-30f + (1f - darknessLevel) * 10f);
            colorAdjustments.postExposure.Override(-1f * darknessLevel);
        }
    }
}