using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverController : MonoBehaviour
{
    [SerializeField] private Button restartButton;
    [SerializeField] private Button exitButton;
    
    private void Start()
    {
        // Ocultar el panel al iniciar
        gameObject.SetActive(false);
        
        // Configurar botones
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }
        
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(ExitToMenu);
        }
    }
    
    public void Show()
    {
        Debug.Log("Game Over: Mostrando panel de Game Over.");
        gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0; // Pausar el juego
        
    }
    
    private void RestartGame()
    {
        // Limpiar PlayerPrefs para evitar problemas
        PlayerPrefs.DeleteKey("HasLampBeenPickedUp");
        PlayerPrefs.DeleteKey("PlayerPosX");
        PlayerPrefs.DeleteKey("PlayerPosY");
        PlayerPrefs.DeleteKey("PlayerPosZ");
        PlayerPrefs.Save();
        
        // Restaurar la escala de tiempo
        Time.timeScale = 1;
        
        // Cargar la escena de inicio
        if (SceneManager.GetActiveScene().name == "mainSceneQuest")
        {
            SceneManager.LoadScene("mainSceneQuest");
        }
        else if (SceneManager.GetActiveScene().name == "TimmyHouse_Tutorial")
        {
            SceneManager.LoadScene("TimmyHouse_Tutorial");
        }
        //SceneManager.LoadScene("mainSceneQuest");
    }
    
    private void ExitToMenu()
    {
        // Restaurar la escala de tiempo
        Time.timeScale = 1;
        
        // Volver al menú principal
        SceneManager.LoadScene("MainMenu");
    }
}