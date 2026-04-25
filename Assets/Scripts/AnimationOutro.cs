using UnityEngine;
using UnityEngine.SceneManagement; // Necesario para gestionar escenas

public class AnimationOutro : MonoBehaviour
{
    // Tag del objeto jugador para identificarlo en la colisión
    [SerializeField] private string playerTag = "Player";

    // Este método se llama cuando otro Collider entra en el trigger
    private void OnTriggerEnter(Collider other)
    {
        // Comprueba si el objeto que ha entrado tiene el tag correcto
        if (other.CompareTag(playerTag))
        {
            // Obtiene el nombre de este GameObject. Asumimos que la escena
            // a cargar tiene el mismo nombre que este objeto.
            string sceneNameToLoad = gameObject.name;

            // Carga la escena especificada
            Debug.Log($"Jugador entró en el trigger. Cargando escena: {sceneNameToLoad}");
            SceneManager.LoadScene(sceneNameToLoad);
        }
    }
}
