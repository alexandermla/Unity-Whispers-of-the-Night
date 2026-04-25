using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadManager : MonoBehaviour
{
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private Transform cameraSpawnPoint;

    [System.Obsolete]
    void Start()
    {
        PositionPersistentObjects();
    }

    [System.Obsolete]
    private void PositionPersistentObjects()
    {
        // Buscar objetos persistentes que vengan de la escena anterior
        GameObject persistentPlayer = GameObject.FindGameObjectWithTag("Player");
        GameObject persistentCamera = Camera.main?.gameObject;

        // Verificar si hay un jugador persistente y un punto de spawn
        if (persistentPlayer != null && playerSpawnPoint != null)
        {
            CharacterController charController = persistentPlayer.GetComponent<CharacterController>();
            if (charController != null)
            {
                charController.enabled = false;
                persistentPlayer.transform.position = playerSpawnPoint.position;
                persistentPlayer.transform.rotation = playerSpawnPoint.rotation;
                charController.enabled = true;
            }
            else
            {
                persistentPlayer.transform.position = playerSpawnPoint.position;
                persistentPlayer.transform.rotation = playerSpawnPoint.rotation;
            }
            Debug.Log("Positioned persistent player at spawn point");
        }
        
        // Verificar si hay una cámara persistente y un punto de spawn
        if (persistentCamera != null && cameraSpawnPoint != null)
        {
            persistentCamera.transform.position = cameraSpawnPoint.position;
            persistentCamera.transform.rotation = cameraSpawnPoint.rotation;
            Debug.Log("Positioned persistent camera at spawn point");
        }

        // Desactivar cualquier jugador existente en la escena si hay uno persistente
        if (persistentPlayer != null)
        {
            foreach (GameObject playerObj in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (playerObj != persistentPlayer)
                {
                    playerObj.SetActive(false);
                    Debug.Log("Disabled scene's original player");
                }
            }
        }
        
        // Desactivar cualquier cámara existente en la escena si hay una persistente
        if (persistentCamera != null)
        {
            foreach (Camera cameraComp in GameObject.FindObjectsOfType<Camera>())
            {
                if (cameraComp.gameObject != persistentCamera)
                {
                    cameraComp.gameObject.SetActive(false);
                    Debug.Log("Disabled scene's original camera");
                }
            }
        }
    }
}