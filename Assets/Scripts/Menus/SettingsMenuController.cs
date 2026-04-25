using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SettingsMenuController : MonoBehaviour
{
    [Header("UI Elements")]
    public Slider soundSlider;
    public Slider musicSlider;
    
    [Header("Buttons")]
    public Button applyButton;
    public Button backButton;
    
    private AudioManager audioManager;

    private void Start()
    {
        // Mostrar cursor en la pantalla de settings
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Encontrar el AudioManager para aplicar cambios en tiempo real
        audioManager = Object.FindFirstObjectByType<AudioManager>();
        
        // Cargar los valores guardados en los sliders
        LoadSettings();
        
        // Configurar los botones
        SetupButtons();
    }
    
    private void SetupButtons()
    {
        if (applyButton != null)
        {
            applyButton.onClick.AddListener(ApplySettings);
        }
        
        if (backButton != null)
        {
            backButton.onClick.AddListener(GoBack);
        }
    }
    
    private void LoadSettings()
    {
        if (soundSlider != null)
        {
            soundSlider.value = PlayerPrefs.GetFloat("SoundVolume", 1f);
        }
        
        if (musicSlider != null)
        {
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
        }
    }
    
    public void ApplySettings()
    {
        // Guardar valores en PlayerPrefs
        if (soundSlider != null)
        {
            PlayerPrefs.SetFloat("SoundVolume", soundSlider.value);
        }
        
        if (musicSlider != null)
        {
            PlayerPrefs.SetFloat("MusicVolume", musicSlider.value);
        }
        
        PlayerPrefs.Save();
        
        // Aplicar cambios al AudioManager si existe
        if (audioManager != null)
        {
            audioManager.UpdateAudioSettings();
        }
        
        Debug.Log("Settings applied");
    }
    
    public void GoBack()
    {
        // Determinar a qué escena volver
        string lastScene = PlayerPrefs.GetString("LastSceneName", "MainMenu");
        
        // Si no hay escena anterior, volver al menú principal
        if (string.IsNullOrEmpty(lastScene))
        {
            lastScene = "MainMenu";
        }
        
        Debug.Log($"Returning to scene: {lastScene}");
        SceneManager.LoadScene(lastScene);
    }
}