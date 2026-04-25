// Archivo: Scripts/Menus/AudioManager.cs
using UnityEngine;
using UnityEngine.Audio; // Necesario para AudioMixer

public class AudioManager : MonoBehaviour
{
    // --- Singleton Pattern (más robusto) ---
    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<AudioManager>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject("AudioManager_AutoCreated");
                    _instance = singletonObject.AddComponent<AudioManager>();
                     Debug.LogWarning("AudioManager instance was not found, auto-creating one.");
                }
            }
            return _instance;
        }
    }
    // --------------------------------------

    [Header("Audio Mixer")]
    [Tooltip("Arrastra aquí tu Asset de Audio Mixer (ej. MainMixer).")]
    [SerializeField] private AudioMixer mainMixer;

    // Ya no necesitas referencias directas a los AudioSource aquí
    // public AudioSource musicSource;
    // public AudioSource soundSource;

    // Nombres de los parámetros expuestos en el Mixer (deben coincidir EXACTAMENTE)
    public const string MASTER_VOL_PARAM = "MasterVolume";
    public const string MUSIC_VOL_PARAM = "MusicVolume";
    public const string SFX_VOL_PARAM = "SFXVolume";
    public const string DIALOGUE_VOL_PARAM = "DialogueVolume";
    // Añade más si creaste otros grupos (ej. UI_VOL_PARAM)

    private void Awake()
    {
        // --- Gestión del Singleton ---
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("Destruyendo instancia duplicada de AudioManager.");
            Destroy(this.gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(this.gameObject);
        // -----------------------------

        // Validación inicial
        if (mainMixer == null)
        {
            Debug.LogError("AudioManager: ¡Main Audio Mixer no asignado en el Inspector!", this);
            // Podrías intentar buscarlo si no está asignado:
            // mainMixer = Resources.FindObjectsOfTypeAll<AudioMixer>().FirstOrDefault(m => m.name == "MainMixer");
            // if (mainMixer == null) Debug.LogError("No se pudo encontrar 'MainMixer'.");
        }
    }

    private void Start()
    {
        // Cargar y aplicar volúmenes guardados al iniciar el juego
        LoadAndApplyVolumes();
    }

    /// <summary>
    /// Carga los valores de volumen desde PlayerPrefs y los aplica al Audio Mixer.
    /// </summary>
    public void LoadAndApplyVolumes()
    {
        if (mainMixer == null) return;

        // Cargar cada valor desde PlayerPrefs (con un valor por defecto razonable)
        // Los sliders suelen ir de 0 a 1, pero el mixer usa decibelios (dB).
        SetGroupVolume(MASTER_VOL_PARAM, PlayerPrefs.GetFloat(MASTER_VOL_PARAM, 0.8f)); // Default 80%
        SetGroupVolume(MUSIC_VOL_PARAM, PlayerPrefs.GetFloat(MUSIC_VOL_PARAM, 0.7f)); // Default 70%
        SetGroupVolume(SFX_VOL_PARAM, PlayerPrefs.GetFloat(SFX_VOL_PARAM, 0.8f));   // Default 80%
        SetGroupVolume(DIALOGUE_VOL_PARAM, PlayerPrefs.GetFloat(DIALOGUE_VOL_PARAM, 0.9f)); // Default 90%

        Debug.Log("AudioManager: Volúmenes cargados y aplicados al Mixer.");
    }

    /// <summary>
    /// Establece el volumen de un grupo específico del Audio Mixer.
    /// </summary>
    /// <param name="exposedParameterName">El nombre EXACTO del parámetro expuesto en el Mixer.</param>
    /// <param name="linearVolume">El volumen deseado en escala lineal (0.0 a 1.0).</param>
    public void SetGroupVolume(string exposedParameterName, float linearVolume)
    {
        if (mainMixer == null) return;

        // Asegurar que el volumen lineal esté entre 0.0001 y 1.0 para evitar errores con Log10(0)
        linearVolume = Mathf.Clamp(linearVolume, 0.0001f, 1.0f);

        // Convertir volumen lineal (0-1) a Decibelios (-80 a 0 dB)
        // -80dB es silencio efectivo en Unity.
        float dbVolume = Mathf.Log10(linearVolume) * 20f;

        // Establecer el valor en el Audio Mixer
        mainMixer.SetFloat(exposedParameterName, dbVolume);
    }

    // Método que puede ser llamado por SettingsManager cuando se aplican los cambios
    // Simplemente vuelve a cargar y aplicar.
    public void UpdateAudioSettings()
    {
        LoadAndApplyVolumes();
    }
}