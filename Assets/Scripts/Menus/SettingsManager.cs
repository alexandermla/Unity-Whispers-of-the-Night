// Archivo: Scripts/Menus/SettingsManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering; // Necesario para Volume si usas URP/HDRP
#if UNITY_PIPELINE_HDRP
using UnityEngine.Rendering.HighDefinition; // Solo si usas HDRP y Volume
#elif UNITY_PIPELINE_URP
using UnityEngine.Rendering.Universal; // Solo si usas URP y Volume
#endif
using System.Collections.Generic;
using System.Linq; // Necesario para resoluciones
using System.Collections;
using UnityEngine.Audio; // Necesario para AudioMixer
using UnityEngine.Localization.Settings;

public class SettingsManager : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject pausePanel;

    // --- AUDIO (Como estaba antes) ---
    [Header("Audio Settings")]
    [SerializeField] private AudioMixer mainMixerRef;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider soundVolumeSlider; // SFX
    [SerializeField] private Slider dialogueVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterValueText;
    [SerializeField] private TextMeshProUGUI musicValueText;
    [SerializeField] private TextMeshProUGUI soundValueText; // SFX
    [SerializeField] private TextMeshProUGUI dialogueValueText;

    // --- GRAPHICS ---
    [Header("Graphics Settings UI References")] // Referencias a los elementos UI que creaste
    [SerializeField] private TMP_Dropdown displayModeDropdown; // Para Ventana, Fullscreen, etc.
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown qualityPresetDropdown; // Renombrado desde qualityDropdown para claridad
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private TMP_Dropdown fpsLimitDropdown;
    [SerializeField] private TMP_Dropdown textureQualityDropdown;
    [SerializeField] private TMP_Dropdown shadowQualityDropdown;
    [SerializeField] private TMP_Dropdown antiAliasingDropdown; // Depende del pipeline
    [SerializeField] private TMP_Dropdown ambientOcclusionDropdown; // Depende del pipeline
    [SerializeField] private Toggle bloomToggle; // Depende del pipeline
    [SerializeField] private Toggle motionBlurToggle; // Depende del pipeline
    [SerializeField] private Slider fovSlider;
    [SerializeField] private TextMeshProUGUI fovValueText; // Para mostrar valor de FOV

    [Header("Post-Processing References")] // <--- NUEVA SECCIÓN
    [Tooltip("Arrastra aquí el GameObject que tiene el componente Volume global para los efectos.")]
    [SerializeField] private Volume globalVolume;


    // --- CONTROLS (Como estaba antes) ---
    [Header("Control Settings")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityValueText;
    [SerializeField] private Toggle invertMouseYToggle; // Añadido Toggle para Invertir Y

    [Header("Buttons")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button backButton;

    // --- FEEDBACK (Como estaba antes) ---
    [Header("Save Feedback")]
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip confirmationSound;
    [SerializeField] private float confirmationDuration = 1.5f;
    private Coroutine confirmationCoroutine;

    // Referencias internas y estado
    private Resolution[] resolutions;
    private List<string> fpsOptions = new List<string> { "30", "60", "120", "144", "Unlimited" };
    private int[] fpsValues = { 30, 60, 120, 144, -1 }; // -1 para Sin Límite
    private bool _active = false;

    // Referencias a otros componentes
    private ThirdPersonCamera thirdPersonCamera;
    private Camera mainCameraComponent; // Para FOV

    // Claves para PlayerPrefs (buena práctica definirlas como constantes)
    private const string MASTER_VOL_KEY = AudioManager.MASTER_VOL_PARAM; // Reutilizar de AudioManager
    private const string MUSIC_VOL_KEY = AudioManager.MUSIC_VOL_PARAM;
    private const string SFX_VOL_KEY = AudioManager.SFX_VOL_PARAM;
    private const string DIALOGUE_VOL_KEY = AudioManager.DIALOGUE_VOL_PARAM;
    private const string DISPLAY_MODE_KEY = "DisplayMode";
    private const string RESOLUTION_WIDTH_KEY = "ResolutionWidth";
    private const string RESOLUTION_HEIGHT_KEY = "ResolutionHeight";
    private const string QUALITY_KEY = "QualityLevel";
    private const string VSYNC_KEY = "VSync";
    private const string FPS_LIMIT_KEY = "FPSLimit";
    private const string TEXTURE_QUALITY_KEY = "TextureQuality";
    private const string SHADOW_QUALITY_KEY = "ShadowQuality";
    private const string AA_KEY = "AntiAliasing";
    private const string AO_KEY = "AmbientOcclusion";
    private const string BLOOM_KEY = "BloomEnabled";
    private const string MOTION_BLUR_KEY = "MotionBlurEnabled";
    private const string FOV_KEY = "FieldOfView";
    private const string SENSITIVITY_KEY = "Sensitivity";
    private const string INVERT_Y_KEY = "InvertMouseY";


    private void Awake()
    {
        // Obtener referencias
        thirdPersonCamera = FindAnyObjectByType<ThirdPersonCamera>(); // Para sensibilidad y FOV
        mainCameraComponent = Camera.main; // Para FOV
        if (uiAudioSource == null) { uiAudioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>(); uiAudioSource.playOnAwake = false; }

        // Validaciones básicas
        if (settingsPanel == null) Debug.LogError("Settings Panel no asignado!", this);
        if (globalVolume == null)
        {
            Debug.LogWarning("SettingsManager: Global Volume no asignado. Efectos como Bloom no funcionarán.", this);
            // Podrías intentar buscarlo: globalVolume = FindAnyObjectByType<Volume>();
        }
        else if (globalVolume.profile == null)
        {
            Debug.LogWarning($"SettingsManager: El Volume asignado ('{globalVolume.gameObject.name}') no tiene un Volume Profile. Efectos como Bloom no funcionarán.", this);
        }
        // Añadir más validaciones para los nuevos elementos UI...
        if (displayModeDropdown == null) Debug.LogWarning("Display Mode Dropdown no asignado.", this);
        if (resolutionDropdown == null) Debug.LogWarning("Resolution Dropdown no asignado.", this);
        if (qualityPresetDropdown == null) Debug.LogWarning("Quality Preset Dropdown no asignado.", this);
        if (vsyncToggle == null) Debug.LogWarning("VSync Toggle no asignado.", this);
        if (fpsLimitDropdown == null) Debug.LogWarning("FPS Limit Dropdown no asignado.", this);
        if (textureQualityDropdown == null) Debug.LogWarning("Texture Quality Dropdown no asignado.", this);
        if (shadowQualityDropdown == null) Debug.LogWarning("Shadow Quality Dropdown no asignado.", this);
        if (antiAliasingDropdown == null) Debug.LogWarning("Anti Aliasing Dropdown no asignado.", this);
        if (ambientOcclusionDropdown == null) Debug.LogWarning("Ambient Occlusion Dropdown no asignado.", this);
        if (bloomToggle == null) Debug.LogWarning("Bloom Toggle no asignado.", this);
        if (motionBlurToggle == null) Debug.LogWarning("Motion Blur Toggle no asignado.", this);
        //if (fovSlider == null) Debug.LogWarning("FOV Slider no asignado.", this);
        if (invertMouseYToggle == null) Debug.LogWarning("Invert Mouse Y Toggle no asignado.", this);
        
    }

    private void Start()
    {
        if (applyButton != null) applyButton.onClick.AddListener(ApplySettings);
        if (backButton != null) backButton.onClick.AddListener(CloseSettings);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        int ID = PlayerPrefs.GetInt("LocaleKey", 0);
        ChangeLocale(ID);
        

        InitializeUI();
        LoadSettings();
    }

    public void OpenSettings()
    {
        if (settingsPanel == null) return;
        settingsPanel.SetActive(true);
        //if (pausePanel != null) pausePanel.SetActive(false);
        LoadSettings();
    }

    private void CloseSettings()
    {
        if (settingsPanel == null) return;
        settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void ChangeLocale(int localeID)
    {
        if (_active)
        {
            return;
        }
        StartCoroutine(SetLocale(localeID));
    }   

    private IEnumerator SetLocale(int localeID)
    {
        _active = true;
        yield return LocalizationSettings.InitializationOperation;
        
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[localeID];
        PlayerPrefs.SetInt("LocalKey", localeID);
        _active = false;
    }

    private void InitializeUI()
    {
        // --- GRÁFICOS ---
        InitializeDisplayModeDropdown();
        InitializeResolutionDropdown();
        InitializeQualityDropdown();
        InitializeFpsLimitDropdown();
        InitializeTextureQualityDropdown();
        InitializeShadowQualityDropdown();
        InitializeAADropdown();
        InitializeAODropdown();
        // Añadir listener para FOV
        //if (fovSlider != null && fovValueText != null) fovSlider.onValueChanged.AddListener(UpdateFovValueText);

        // --- AUDIO ---
        if (masterVolumeSlider != null) { if (masterValueText != null) masterVolumeSlider.onValueChanged.AddListener(UpdateMasterValueText); else masterVolumeSlider.onValueChanged.AddListener(ApplyMasterVolumePreview); }
        if (musicVolumeSlider != null) { if (musicValueText != null) musicVolumeSlider.onValueChanged.AddListener(UpdateMusicValueText); else musicVolumeSlider.onValueChanged.AddListener(ApplyMusicVolumePreview); }
        if (soundVolumeSlider != null) { if (soundValueText != null) soundVolumeSlider.onValueChanged.AddListener(UpdateSoundValueText); else soundVolumeSlider.onValueChanged.AddListener(ApplySFXVolumePreview); } // SFX
        if (dialogueVolumeSlider != null) { if (dialogueValueText != null) dialogueVolumeSlider.onValueChanged.AddListener(UpdateDialogueValueText); else dialogueVolumeSlider.onValueChanged.AddListener(ApplyDialogueVolumePreview); }

        // --- CONTROLES ---
        if (sensitivitySlider != null && sensitivityValueText != null) sensitivitySlider.onValueChanged.AddListener(UpdateSensitivityValueText);
        // No se necesita listener para el toggle de invertir Y, se aplica al presionar "Apply"
    }

    // --- Métodos de Inicialización de Dropdowns Gráficos ---

    private void InitializeDisplayModeDropdown() {
        if (displayModeDropdown == null) return;
        displayModeDropdown.ClearOptions();
        // Añadir opciones estándar
        displayModeDropdown.AddOptions(new List<string> { "Fullscreen", "Borderless", "Windowed" });
        displayModeDropdown.RefreshShownValue();
    }

    private void InitializeResolutionDropdown() {
         if (resolutionDropdown == null) return;
         // --- Lógica de filtrado y población de resoluciones (como antes) ---
         resolutions = Screen.resolutions.Where(res => res.refreshRateRatio.value > 59).Distinct().ToArray(); // Filtrar >= 60Hz y duplicados
         resolutionDropdown.ClearOptions();
         List<string> options = new List<string>();
         int currentResolutionIndex = -1;
         for (int i = 0; i < resolutions.Length; i++)
         {
             string option = resolutions[i].width + " x " + resolutions[i].height; // + " @ " + resolutions[i].refreshRateRatio.value.ToString("F0") + " Hz"; // Opcional: mostrar Hz
             options.Add(option);
             if (resolutions[i].width == Screen.currentResolution.width &&
                 resolutions[i].height == Screen.currentResolution.height)
             {
                 currentResolutionIndex = i;
             }
         }
         resolutionDropdown.AddOptions(options);
         // Si no se encontró la resolución actual, seleccionar la más alta
         if (currentResolutionIndex == -1) currentResolutionIndex = resolutions.Length -1;
          resolutionDropdown.value = currentResolutionIndex;
         resolutionDropdown.RefreshShownValue();
         // --- Fin lógica resoluciones ---
    }

     private void InitializeQualityDropdown() {
        if (qualityPresetDropdown == null) return;
        //qualityPresetDropdown.ClearOptions();
        //qualityPresetDropdown.AddOptions(new List<string>(QualitySettings.names)); // Usar nombres de Quality Settings de Unity
        qualityPresetDropdown.RefreshShownValue();
    }

     private void InitializeFpsLimitDropdown() {
         if (fpsLimitDropdown == null) return;
         fpsLimitDropdown.ClearOptions();
         fpsLimitDropdown.AddOptions(fpsOptions);
         fpsLimitDropdown.RefreshShownValue();
     }

     private void InitializeTextureQualityDropdown() {
         if (textureQualityDropdown == null) return;
         textureQualityDropdown.ClearOptions();
         textureQualityDropdown.AddOptions(new List<string> { "High", "Mid", "Low", "Very low" }); // Los números son mipmapBias
         textureQualityDropdown.RefreshShownValue();
     }

     private void InitializeShadowQualityDropdown() {
         if (shadowQualityDropdown == null) return;
         shadowQualityDropdown.ClearOptions();
         // Estas opciones dependen de cómo quieras mapearlas a QualitySettings.shadowResolution, etc.
         shadowQualityDropdown.AddOptions(new List<string> { "Low", "Mid", "High", "Epic" });
         shadowQualityDropdown.RefreshShownValue();
     }

     private void InitializeAADropdown() {
        if (antiAliasingDropdown == null) return;
        antiAliasingDropdown.ClearOptions();
        
        #if UNITY_PIPELINE_HDRP
        // Opciones para HDRP
        antiAliasingDropdown.AddOptions(new List<string> { "Off", "FXAA", "TAA", "SMAA" });
        #elif UNITY_PIPELINE_URP
        // Opciones para URP
        antiAliasingDropdown.AddOptions(new List<string> { "Off", "FXAA", "SMAA" });
        #else
        // Opciones para Built-in pipeline
        antiAliasingDropdown.AddOptions(new List<string> { "Off", "2x MSAA", "4x MSAA", "8x MSAA" });
        #endif
        
        antiAliasingDropdown.RefreshShownValue();
    }

     private void InitializeAODropdown() {
         if (ambientOcclusionDropdown == null) return;
         ambientOcclusionDropdown.ClearOptions();
         // Ejemplo simple on/off o con niveles
         ambientOcclusionDropdown.AddOptions(new List<string> { "Off", "Low", "Medium", "High" });
         ambientOcclusionDropdown.RefreshShownValue();
     }


    // --- Cargar Configuración ---
    private void LoadSettings()
    {
        // Audio
        if (masterVolumeSlider != null) masterVolumeSlider.value = PlayerPrefs.GetFloat(MASTER_VOL_KEY, 0.8f);
        if (musicVolumeSlider != null) musicVolumeSlider.value = PlayerPrefs.GetFloat(MUSIC_VOL_KEY, 0.7f);
        if (soundVolumeSlider != null) soundVolumeSlider.value = PlayerPrefs.GetFloat(SFX_VOL_KEY, 0.8f);
        if (dialogueVolumeSlider != null) dialogueVolumeSlider.value = PlayerPrefs.GetFloat(DIALOGUE_VOL_KEY, 0.9f);

        // Gráficos
        if (displayModeDropdown != null) displayModeDropdown.value = PlayerPrefs.GetInt(DISPLAY_MODE_KEY, (int)FullScreenMode.FullScreenWindow); // Default Borderless
        if (resolutionDropdown != null && resolutions != null && resolutions.Length > 0) { /* ... Cargar resolución guardada como antes ... */ int savedWidth = PlayerPrefs.GetInt(RESOLUTION_WIDTH_KEY, Screen.currentResolution.width); int savedHeight = PlayerPrefs.GetInt(RESOLUTION_HEIGHT_KEY, Screen.currentResolution.height); int foundIndex = -1; for(int i=0; i < resolutions.Length; i++) { if(resolutions[i].width == savedWidth && resolutions[i].height == savedHeight) { foundIndex = i; break; } } resolutionDropdown.value = (foundIndex != -1) ? foundIndex : resolutions.Length -1; resolutionDropdown.RefreshShownValue(); }
        if (qualityPresetDropdown != null) { qualityPresetDropdown.value = PlayerPrefs.GetInt(QUALITY_KEY, QualitySettings.GetQualityLevel()); qualityPresetDropdown.RefreshShownValue(); }
        if (vsyncToggle != null) vsyncToggle.isOn = PlayerPrefs.GetInt(VSYNC_KEY, 0) == 1; // VSync off por defecto
        if (fpsLimitDropdown != null) fpsLimitDropdown.value = PlayerPrefs.GetInt(FPS_LIMIT_KEY, fpsOptions.Count - 1); // Default Sin Límite
        if (textureQualityDropdown != null) textureQualityDropdown.value = PlayerPrefs.GetInt(TEXTURE_QUALITY_KEY, 0); // Default Alta (0)
        if (shadowQualityDropdown != null) shadowQualityDropdown.value = PlayerPrefs.GetInt(SHADOW_QUALITY_KEY, 2); // Default Alta (2)
        if (antiAliasingDropdown != null) antiAliasingDropdown.value = PlayerPrefs.GetInt(AA_KEY, 1); // Default FXAA (1) o ajusta
        if (ambientOcclusionDropdown != null) ambientOcclusionDropdown.value = PlayerPrefs.GetInt(AO_KEY, 1); // Default Low (1) o ajusta
        if (bloomToggle != null) bloomToggle.isOn = PlayerPrefs.GetInt(BLOOM_KEY, 1) == 1; // <-- Cargar Bloom
        if (motionBlurToggle != null) motionBlurToggle.isOn = PlayerPrefs.GetInt(MOTION_BLUR_KEY, 0) == 1; // <-- Cargar Motion Blur
        //if (fovSlider != null) fovSlider.value = PlayerPrefs.GetFloat(FOV_KEY, 90f); // Default FOV 90


        // Controles
        if (sensitivitySlider != null) sensitivitySlider.value = PlayerPrefs.GetFloat(SENSITIVITY_KEY, 5f); // Sensibilidad Default 5
        if (invertMouseYToggle != null) invertMouseYToggle.isOn = PlayerPrefs.GetInt(INVERT_Y_KEY, 0) == 1; // Invertir Off por defecto


        // Actualizar textos y previews
        UpdateAllValueTexts();
        ApplyAudioPreviews(); // Aplica volumen al mixer para preview
        ApplyVolumeEffectsPreview();
        //ApplyFovPreview(fovSlider != null ? fovSlider.value : 90f); // Aplica FOV para preview
    }

    // --- Aplicar Configuración ---
    public void ApplySettings()
    {
        // --- AUDIO ---
        if (masterVolumeSlider != null) PlayerPrefs.SetFloat(MASTER_VOL_KEY, masterVolumeSlider.value);
        if (musicVolumeSlider != null) PlayerPrefs.SetFloat(MUSIC_VOL_KEY, musicVolumeSlider.value);
        if (soundVolumeSlider != null) PlayerPrefs.SetFloat(SFX_VOL_KEY, soundVolumeSlider.value);
        if (dialogueVolumeSlider != null) PlayerPrefs.SetFloat(DIALOGUE_VOL_KEY, dialogueVolumeSlider.value);
        if (AudioManager.Instance != null) AudioManager.Instance.UpdateAudioSettings();

        // --- GRÁFICOS ---
        // Calidad General
        if (qualityPresetDropdown != null) {
            int qualityIndex = qualityPresetDropdown.value;
            QualitySettings.SetQualityLevel(qualityIndex, true); // true para aplicar inmediatamente
            PlayerPrefs.SetInt(QUALITY_KEY, qualityIndex);
        }
        // Modo Pantalla y Resolución
        FullScreenMode screenMode = FullScreenMode.FullScreenWindow; // Default
        if (displayModeDropdown != null) {
            screenMode = (FullScreenMode)displayModeDropdown.value; // Asumiendo 0: Fullscreen, 1: Borderless, 2: Windowed
            PlayerPrefs.SetInt(DISPLAY_MODE_KEY, (int)screenMode);
        }
        if (resolutionDropdown != null && resolutions != null && resolutionDropdown.value < resolutions.Length) {
             Resolution selectedResolution = resolutions[resolutionDropdown.value];
             Screen.SetResolution(selectedResolution.width, selectedResolution.height, screenMode);
             PlayerPrefs.SetInt(RESOLUTION_WIDTH_KEY, selectedResolution.width);
             PlayerPrefs.SetInt(RESOLUTION_HEIGHT_KEY, selectedResolution.height);
             Debug.Log($"Resolution set to: {selectedResolution.width}x{selectedResolution.height}, Mode: {screenMode}");
        }
        // VSync
        if (vsyncToggle != null) {
            QualitySettings.vSyncCount = vsyncToggle.isOn ? 1 : 0;
            PlayerPrefs.SetInt(VSYNC_KEY, vsyncToggle.isOn ? 1 : 0);
        }
        // Límite FPS
        if (fpsLimitDropdown != null) {
            int selectedFpsValue = fpsValues[fpsLimitDropdown.value];
            Application.targetFrameRate = selectedFpsValue;
            PlayerPrefs.SetInt(FPS_LIMIT_KEY, fpsLimitDropdown.value);
        }
        // Calidad Texturas (Mipmap Bias: 0=Full, 1=Half, 2=Quarter, 3=Eighth)
        if (textureQualityDropdown != null) {
            QualitySettings.globalTextureMipmapLimit = textureQualityDropdown.value;
            PlayerPrefs.SetInt(TEXTURE_QUALITY_KEY, textureQualityDropdown.value);
        }
         // Calidad Sombras (Esto es más complejo, depende de varios settings)
         if (shadowQualityDropdown != null) {
             // Mapea el índice del dropdown a configuraciones específicas
             SetShadowQuality(shadowQualityDropdown.value);
             PlayerPrefs.SetInt(SHADOW_QUALITY_KEY, shadowQualityDropdown.value);
         }
         // Anti-Aliasing (Depende del pipeline)
         if (antiAliasingDropdown != null) {
             SetAntiAliasing(antiAliasingDropdown.value);
             PlayerPrefs.SetInt(AA_KEY, antiAliasingDropdown.value);
         }
         // AO, Bloom, Motion Blur (Dependen del pipeline - Volumenes URP/HDRP)
        if (ambientOcclusionDropdown != null) { SetVolumeEffect(AO_KEY, ambientOcclusionDropdown.value); PlayerPrefs.SetInt(AO_KEY, ambientOcclusionDropdown.value); }
        if (bloomToggle != null) { SetVolumeEffect(BLOOM_KEY, bloomToggle.isOn ? 1 : 0); PlayerPrefs.SetInt(BLOOM_KEY, bloomToggle.isOn ? 1 : 0); } // <-- Aplica Bloom
        if (motionBlurToggle != null) { SetVolumeEffect(MOTION_BLUR_KEY, motionBlurToggle.isOn ? 1 : 0); PlayerPrefs.SetInt(MOTION_BLUR_KEY, motionBlurToggle.isOn ? 1 : 0); } // <-- Aplica Motion Blur
         // FOV
         if (fovSlider != null) {
             ApplyFov(fovSlider.value); // Aplica directamente
             PlayerPrefs.SetFloat(FOV_KEY, fovSlider.value);
         }

        // --- CONTROLES ---
        if (sensitivitySlider != null) {
            ApplySensitivity(sensitivitySlider.value); // Aplica directamente
            PlayerPrefs.SetFloat(SENSITIVITY_KEY, sensitivitySlider.value);
        }
         if (invertMouseYToggle != null) {
             ApplyInvertY(invertMouseYToggle.isOn); // Aplica directamente
             PlayerPrefs.SetInt(INVERT_Y_KEY, invertMouseYToggle.isOn ? 1 : 0);
         }

        // Guardar todos los cambios de PlayerPrefs
        PlayerPrefs.Save();
        Debug.Log("Settings applied and saved successfully.");
        ShowConfirmationFeedback();
    }

    // --- Métodos Helper para Aplicar Settings Específicos ---

    private void SetShadowQuality(int level) {
        // Ejemplo de mapeo (ajusta según tus necesidades)
        switch (level) {
            case 0: // Baja
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.shadowDistance = 40f;
                QualitySettings.shadowCascades = 0;
                break;
            case 1: // Media
                QualitySettings.shadowResolution = ShadowResolution.Medium;
                QualitySettings.shadowDistance = 70f;
                QualitySettings.shadowCascades = 2;
                break;
            case 2: // Alta
                QualitySettings.shadowResolution = ShadowResolution.High;
                QualitySettings.shadowDistance = 100f;
                QualitySettings.shadowCascades = 4;
                break;
            case 3: // Muy Alta
                QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                QualitySettings.shadowDistance = 150f;
                QualitySettings.shadowCascades = 4;
                break;
        }
    }

    private void SetAntiAliasing(int level) {
        // --- Built-in Pipeline Example ---
         QualitySettings.antiAliasing = level == 0 ? 0 : (int)Mathf.Pow(2, level); // 0=Off, 1=2x, 2=4x, 3=8x

        // --- URP/HDRP Example (requiere referencia al Asset del Pipeline) ---
        // var pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; // O HDRenderPipelineAsset
        // if (pipelineAsset != null) {
        //     if (level == 0) pipelineAsset.msaaSampleCount = 1; // Off
        //     else pipelineAsset.msaaSampleCount = (int)Mathf.Pow(2, level); // 2x, 4x, 8x (si están soportados)
        //     // O ajustar propiedades de TAA, FXAA, SMAA si usas post-processing AA
        // }
         Debug.LogWarning("SetAntiAliasing: Implementación depende de tu Render Pipeline.");
    }

    // Método de ejemplo para efectos de Volumen (necesita adaptarse a tu pipeline URP/HDRP)
    private void SetVolumeEffect(string effectKey, int value)
    {

        // Intentar obtener y activar/desactivar el override
        switch (effectKey)
        {
            case BLOOM_KEY:
                 // Intentar obtener el componente Bloom del perfil
                 if (globalVolume.profile.TryGet<Bloom>(out var bloom))
                 {
                     bloom.active = (value == 1); // Activar si value es 1, desactivar si es 0
                     //Debug.Log($"Bloom active set to: {bloom.active}");
                 }
                 else Debug.LogWarning("Override de Bloom no encontrado en el Volume Profile asignado.");
                break;

            case MOTION_BLUR_KEY:
                 if (globalVolume.profile.TryGet<MotionBlur>(out var motionBlur))
                 {
                     motionBlur.active = (value == 1);
                      //Debug.Log($"Motion Blur active set to: {motionBlur.active}");
                 }
                 else Debug.LogWarning("Override de MotionBlur no encontrado en el Volume Profile asignado.");
                 break;

             case AO_KEY:
                 if (globalVolume.profile.TryGet<ScreenSpaceAmbientOcclusion>(out var ao))
                 {
                     ao.active = (value > 0); // Activar si value es > 0 (Off es 0)
                     // Opcional: Mapear value (1, 2, 3) a calidad si el dropdown tiene niveles
                     // ao.quality.value = Mathf.Clamp(value - 1, (int)AmbientOcclusionQuality.Low, (int)AmbientOcclusionQuality.High); // Ejemplo URP
                      //Debug.Log($"Ambient Occlusion active set to: {ao.active}");
                 }
                 else Debug.LogWarning("Override de AmbientOcclusion no encontrado en el Volume Profile asignado.");
                 break;
        }
    }

    private void ApplyFov(float value) {
        if (mainCameraComponent != null) {
            mainCameraComponent.fieldOfView = value;
        }
        // Si usas Cinemachine, también podrías necesitar ajustar la Virtual Camera activa
        // if (thirdPersonCamera != null) {
        //    // Acceder a la vcam activa y ajustar su FOV
        // }
    }

    private void ApplySensitivity(float value) {
        if (thirdPersonCamera != null) {
            thirdPersonCamera.sensitivity = value; // Ajusta según cómo uses la sensibilidad en tu cámara
        }
    }

     private void ApplyInvertY(bool isInverted) {
         if (thirdPersonCamera != null) {
             // Necesitarías añadir una variable bool `invertY` en ThirdPersonCamera
             // y usarla al calcular el pitch:
             // currentPitch = Mathf.Clamp(currentPitch + (invertY ? lookInput.y : -lookInput.y) * sensitivity, pitchLimits.x, pitchLimits.y);
              Debug.LogWarning("ApplyInvertY: Necesita implementación en ThirdPersonCamera.");
         }
     }


    // --- Métodos para actualizar textos y previews (sin cambios lógicos, solo añadidos) ---
    private void UpdateAllValueTexts() {
        if(masterValueText != null && masterVolumeSlider != null) UpdateMasterValueText(masterVolumeSlider.value);
        if(musicValueText != null && musicVolumeSlider != null) UpdateMusicValueText(musicVolumeSlider.value);
        if(soundValueText != null && soundVolumeSlider != null) UpdateSoundValueText(soundVolumeSlider.value);
        if(dialogueValueText != null && dialogueVolumeSlider != null) UpdateDialogueValueText(dialogueVolumeSlider.value);
        if(sensitivityValueText != null && sensitivitySlider != null) UpdateSensitivityValueText(sensitivitySlider.value);
        //if(fovValueText != null && fovSlider != null) UpdateFovValueText(fovSlider.value);
    }
    private void UpdateMasterValueText(float value) { if (masterValueText != null) masterValueText.text = $"{Mathf.RoundToInt(value * 100)}%"; ApplyMasterVolumePreview(value); }
    private void UpdateMusicValueText(float value) { if (musicValueText != null) musicValueText.text = $"{Mathf.RoundToInt(value * 100)}%"; ApplyMusicVolumePreview(value); }
    private void UpdateSoundValueText(float value) { if (soundValueText != null) soundValueText.text = $"{Mathf.RoundToInt(value * 100)}%"; ApplySFXVolumePreview(value); }
    private void UpdateDialogueValueText(float value) { if (dialogueValueText != null) dialogueValueText.text = $"{Mathf.RoundToInt(value * 100)}%"; ApplyDialogueVolumePreview(value); }
    private void UpdateSensitivityValueText(float value) { if (sensitivityValueText != null) sensitivityValueText.text = value.ToString("F1"); }
    //private void UpdateFovValueText(float value) { if (fovValueText != null) fovValueText.text = value.ToString("F0"); ApplyFovPreview(value); } // Mostrar FOV sin decimales

    // --- Métodos para aplicar previews (sin cambios lógicos, solo añadidos) ---
    private void ApplyMasterVolumePreview(float value) { if (AudioManager.Instance != null) AudioManager.Instance.SetGroupVolume(AudioManager.MASTER_VOL_PARAM, value); }
    private void ApplyMusicVolumePreview(float value) { if (AudioManager.Instance != null) AudioManager.Instance.SetGroupVolume(AudioManager.MUSIC_VOL_PARAM, value); }
    private void ApplySFXVolumePreview(float value) { if (AudioManager.Instance != null) AudioManager.Instance.SetGroupVolume(AudioManager.SFX_VOL_PARAM, value); }
    private void ApplyDialogueVolumePreview(float value) { if (AudioManager.Instance != null) AudioManager.Instance.SetGroupVolume(AudioManager.DIALOGUE_VOL_PARAM, value); }
    private void ApplyFovPreview(float value) { ApplyFov(value); } // Llama directamente al método de aplicar
    private void ApplyVolumeEffectsPreview() {
         if (bloomToggle != null) SetVolumeEffect(BLOOM_KEY, bloomToggle.isOn ? 1 : 0);
         if (motionBlurToggle != null) SetVolumeEffect(MOTION_BLUR_KEY, motionBlurToggle.isOn ? 1 : 0);
         if (ambientOcclusionDropdown != null) SetVolumeEffect(AO_KEY, ambientOcclusionDropdown.value);
    }

    // Aplica todos los previews de audio al cargar el menú
    private void ApplyAudioPreviews() {
        if (masterVolumeSlider != null) ApplyMasterVolumePreview(masterVolumeSlider.value);
        if (musicVolumeSlider != null) ApplyMusicVolumePreview(musicVolumeSlider.value);
        if (soundVolumeSlider != null) ApplySFXVolumePreview(soundVolumeSlider.value);
        if (dialogueVolumeSlider != null) ApplyDialogueVolumePreview(dialogueVolumeSlider.value);
    }

    private void ShowConfirmationFeedback()
    {
        if (confirmationPanel != null)
        {
            if (confirmationCoroutine != null) StopCoroutine(confirmationCoroutine);
            confirmationCoroutine = StartCoroutine(ShowConfirmationPanel());
        }
        if (uiAudioSource != null && confirmationSound != null)
        {
            uiAudioSource.PlayOneShot(confirmationSound);
        }
    }

    private IEnumerator ShowConfirmationPanel()
    {
        confirmationPanel.SetActive(true);
        yield return new WaitForSecondsRealtime(confirmationDuration); // Usa tiempo real por si el juego está pausado
        confirmationPanel.SetActive(false);
        confirmationCoroutine = null;
    }
}