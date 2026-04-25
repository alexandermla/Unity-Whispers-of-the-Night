using UnityEngine;
using UnityEngine.Video; // Necesario para interactuar con VideoPlayer
using UnityEngine.SceneManagement; // Necesario para gestionar escenas

// Asegura que este script se añada a un GameObject que tenga un VideoPlayer
[RequireComponent(typeof(VideoPlayer))]
public class VideoEndHandler : MonoBehaviour
{
    private VideoPlayer videoPlayer;

    void Awake()
    {
        // Obtiene la referencia al componente VideoPlayer en el mismo GameObject
        videoPlayer = GetComponent<VideoPlayer>();
    }

    void Start()
    {
        // Suscribe el método OnVideoEnd al evento loopPointReached del VideoPlayer.
        // Asegúrate de que la opción "Loop" NO esté marcada en tu VideoPlayer.
        videoPlayer.loopPointReached += OnVideoEnd;
    }

    // Método que se ejecuta cuando el video termina
    void OnVideoEnd(VideoPlayer vp)
    {
        Debug.Log("Video terminado. Borrando datos guardados y cargando MainMenu...");

        // --- MODIFICADO: Llamar a SaveSystem para borrar datos ---
        // Intentar encontrar la instancia del SaveSystem
        SaveSystem saveSystemInstance = SaveSystem.Instance;
        if (saveSystemInstance != null)
        {
            saveSystemInstance.DeleteSaveData(); // Llama al método específico para borrar
            Debug.Log("Datos guardados específicos del juego borrados por SaveSystem.");
        }
        else
        {
            // Fallback (menos ideal, pero como estaba antes si SaveSystem no existe)
            Debug.LogWarning("SaveSystem.Instance no encontrado. Usando PlayerPrefs.DeleteAll() como fallback.");
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
        // ----------------------------------------------------------

        // Hacer visible el cursor antes de ir al menú
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Carga la escena del menú principal
        SceneManager.LoadScene("MainMenu");
    }

    // Es buena práctica desuscribirse de los eventos cuando el objeto se destruye
    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
        }
    }
}