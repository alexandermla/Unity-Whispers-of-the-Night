using UnityEngine;
using UnityEngine.UI; // Necesario para controlar la Image del fundido
using UnityEngine.SceneManagement; // Necesario para cambiar de escena
using System.Collections; // Necesario para las Coroutines (IEnumerator)

// Puedes renombrar esta clase si lo deseas, por ejemplo, a SceneTransitionTrigger
public class GodDialogueTriggerTransition : MonoBehaviour
{
    [Header("Configuración de Transición")]
    [Tooltip("Arrastra aquí un componente UI Image que cubra toda la pantalla y sea de color negro.")]
    [SerializeField] private Image fadeImage;

    [Tooltip("Nombre exacto de la escena a cargar.")]
    [SerializeField] private string sceneToLoad = "VoidTalk";

    [Tooltip("Duración del fundido a negro en segundos.")]
    [SerializeField] private float fadeDuration = 1.0f; // Puedes ajustar cuánto tarda el fundido

    [Tooltip("Tiempo de espera en negro antes de cargar la escena.")]
    [SerializeField] private float waitDuration = 2.0f; // Los 2 segundos que pediste

    [Tooltip("Tag del objeto jugador para activar la transición.")]
    [SerializeField] private string playerTag = "Player"; // Asegúrate de que tu jugador tenga este Tag

    private bool isLoading = false; // Para evitar que se active múltiples veces

    void Start()
    {
        // Asegúrate de que la imagen de fundido exista y esté configurada correctamente
        if (fadeImage == null)
        {
            Debug.LogError("¡Error! No se ha asignado la Image para el fundido en el Inspector.", this);
            // Desactivamos el script si no hay imagen para evitar errores
            enabled = false;
            fadeImage.gameObject.SetActive(false);
            return;
        }

        // Configura la imagen de fundido para que sea inicialmente transparente
        // y esté lista para el fundido.
        Color initialColor = fadeImage.color;
        initialColor.a = 0f; // Alpha = 0 (transparente)
        fadeImage.color = initialColor;
        // Aseguramos que el GameObject de la imagen esté activo,
        // aunque la imagen en sí sea transparente.
        fadeImage.gameObject.SetActive(true);
    }

    // Esta función se llama automáticamente cuando otro Collider entra en nuestro Trigger
    private void OnTriggerEnter(Collider other)
    {
        // Comprobamos si ya estamos cargando la escena Y si el objeto que entró tiene el Tag correcto
        if (!isLoading && other.CompareTag(playerTag))
        {
            fadeImage.gameObject.SetActive(true); // Aseguramos que la imagen de fundido esté activa
            // ¡El jugador ha entrado!
            Debug.Log($"El jugador ({other.name}) ha entrado en el trigger. Iniciando transición a '{sceneToLoad}'.");
            isLoading = true; // Marcamos que estamos cargando para evitar re-activación

            // Iniciamos la Coroutine que hará el fundido, la espera y la carga
            StartCoroutine(FadeAndLoadScene());
        }
    }

    // Coroutine para manejar la secuencia de fundido, espera y carga de escena
    private IEnumerator FadeAndLoadScene()
    {
        Debug.Log("Iniciando fundido a negro...");
        float timer = 0f;
        Color startColor = fadeImage.color; // Color actual (debería ser transparente)
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 1f); // Mismo color, pero opaco (alpha=1)

        // Bucle para el fundido (aumentar el alpha gradualmente)
        while (timer < fadeDuration)
        {
            // Incrementamos el temporizador basado en el tiempo real
            timer += Time.deltaTime;
            // Calculamos el progreso del fundido (0 a 1)
            float alphaProgress = Mathf.Clamp01(timer / fadeDuration);
            // Interpolamos el color (específicamente el alpha)
            fadeImage.color = Color.Lerp(startColor, endColor, alphaProgress);
            // Esperamos al siguiente frame antes de continuar el bucle
            yield return null;
        }

        // Aseguramos que el color final sea completamente opaco
        fadeImage.color = endColor;
        Debug.Log("Fundido a negro completado.");

        // Esperamos los segundos especificados
        Debug.Log($"Esperando {waitDuration} segundos...");
        yield return new WaitForSeconds(waitDuration);

        // Cargamos la escena
        Debug.Log($"Cargando escena: {sceneToLoad}");
        SceneManager.LoadScene(sceneToLoad);
    }

    // Es buena idea añadir esto por si necesitas desactivar el trigger visualmente
    // o asegurarte de que sea un trigger.
    void Reset()
    {
        // Intenta encontrar o añadir un Collider y marcarlo como Trigger por defecto
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
           #if UNITY_EDITOR // Solo sugerir en el editor
            Debug.LogWarning("Este script necesita un Collider en el mismo GameObject. Considera añadir un BoxCollider o SphereCollider y marcar 'Is Trigger' como verdadero.", this);
           #endif
        }
        else
        {
            if (!col.isTrigger)
            {
               #if UNITY_EDITOR
                Debug.LogWarning($"El Collider '{col.GetType().Name}' en este GameObject no está marcado como 'Is Trigger'. Márcalo para que OnTriggerEnter funcione.", this);
               #endif
            }
        }
    }
}