using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

// Asegúrate que el nombre del archivo sea DialogueManager.cs si cambiaste el nombre de la clase
[RequireComponent(typeof(AudioSource))] // Asegura que haya un AudioSource
public class DialogueManager : MonoBehaviour
{
    [Header("Referencias UI")]
    public Canvas dialogueCanvas;
    public RectTransform mainPanel;
    public RectTransform dialoguePanel;
    public Image backgroundImage;
    public Image goddessImage;
    public TextMeshProUGUI dialogueText;

    [Header("Diálogo")]
    [SerializeField] private string[] dialogueLines = new string[] {
        "Approach, little ember in the perpetual night. You have gathered the echoes of this valley, the petrified memories that weep in silence.",
        "You feel the weight of the void, don't you? The unnatural stillness that chokes the laughter and whispers of those you call home. Umbra... a hungry shadow, has devoured their will, leaving only husks wandering in the penumbra.",
        "But you... you shine differently. A spark of the ancient light endures within you, the same that wove life into this forest before the darkness claimed its dominion. That is why the shadow did not completely break you.",
        "The human heart, what a complex tapestry! It weaves the purest joy and the deepest sorrow with the same threads. I know that mourning tightens your throat, that every shadow reminds you of a loss. It is the price of loving in an ephemeral world.",
        "But do not succumb to despair. You are the fulcrum of the balance, child of light and shadow. Within you, courage must dance alongside fear, determination against doubt. For Umbra feeds on imbalance, on the extremes that consume.",
        "The light you carried, the little guide... it was a fragment of my essence, entrusted to this world. Now, fused with you, it is yours to wield. Not as a weapon of wrath, but as a beacon of temperance.",
        "Your path begins now. You must face the root of this corruption, the pulsating heart of Umbra. It will not be easy. The darkness will try to seduce you, to reflect your own fears.",
        "Seek balance in every step. Remember the lost, not with the bitterness of the end, but with the warmth of their existence. That memory is your strongest shield.",
        "Go now. Head to the Hidden Forest Clearing, where the trees whisper ancestral secrets. There, the path will become clearer. I... I shall await you where light and shadow converge."
    };

    // --- NUEVO: Audio ---
    [Header("Audio")]
    [Tooltip("AudioSource para reproducir los diálogos.")]
    [SerializeField] private AudioSource dialogueAudioSource;
    [Tooltip("Clips de audio correspondientes a cada línea de diálogo (deben estar en el mismo orden y tener la misma cantidad que dialogueLines).")]
    [SerializeField] private AudioClip[] dialogueAudioClips;
    // --------------------

    [Header("Configuración")]
    [Tooltip("Velocidad del efecto máquina de escribir (segundos por letra).")]
    public float typingSpeed = 0.05f;
    // public float dialogueDuration = 8f; // <-- Ya no se usa, la duración la marca el audio
    public bool showDialogueOnStart = true;

    // Variables privadas
    private int currentDialogueIndex = 0;
    private Coroutine displayCoroutine;
    private bool isDialogueActive = false;

    void Awake() // Cambiado a Awake para asegurar que el AudioSource se obtenga antes de Start
    {
        // Obtener AudioSource si no está asignado
        if (dialogueAudioSource == null)
        {
            dialogueAudioSource = GetComponent<AudioSource>();
        }
         // Validar AudioSource
        if (dialogueAudioSource == null)
        {
            Debug.LogError("DialogueManager: No se encontró o asignó un AudioSource!", this);
        }
         // Validar arrays
         if (dialogueAudioClips == null || dialogueLines == null || dialogueAudioClips.Length != dialogueLines.Length) {
             Debug.LogError("DialogueManager: ¡El array 'dialogueAudioClips' debe tener el mismo tamaño que 'dialogueLines' y ambos deben estar asignados!", this);
         }

        // Configuración inicial UI
        if (dialogueCanvas != null) dialogueCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        if (mainPanel != null) mainPanel.gameObject.SetActive(true);
    }

    void Start()
    {
        // Mostrar el primer diálogo si está configurado (se movió de Awake a Start)
        if (showDialogueOnStart)
            Invoke(nameof(ShowNextDialogue), 2.0f);
    }

    // Muestra la siguiente línea de diálogo disponible
    public void ShowNextDialogue()
    {
        // Detener audio anterior si aún sonaba (importante si se salta rápido)
        if (dialogueAudioSource != null && dialogueAudioSource.isPlaying)
        {
            dialogueAudioSource.Stop();
        }

        // Detener corutina de tipeo anterior
        if (displayCoroutine != null)
            StopCoroutine(displayCoroutine);

        // Verificar si quedan líneas
        if (currentDialogueIndex < dialogueLines.Length)
        {
            if (mainPanel != null) mainPanel.gameObject.SetActive(true);

            // --- NUEVO: Reproducir Audio ---
            AudioClip clipToPlay = null;
            if (dialogueAudioClips != null && currentDialogueIndex < dialogueAudioClips.Length)
            {
                 clipToPlay = dialogueAudioClips[currentDialogueIndex];
            }

            if (dialogueAudioSource != null && clipToPlay != null)
            {
                dialogueAudioSource.clip = clipToPlay;
                dialogueAudioSource.Play();
                 //Debug.Log($"Playing audio: {clipToPlay.name}");
            }
            else if (clipToPlay == null)
            {
                 Debug.LogWarning($"No hay AudioClip asignado para la línea de diálogo {currentDialogueIndex}.");
            }
            // -----------------------------

            // Iniciar tipeo
            displayCoroutine = StartCoroutine(TypeDialogue(dialogueLines[currentDialogueIndex]));
            isDialogueActive = true;
            currentDialogueIndex++;
        }
        else
        {
            EndDialogueSequence(); // Terminar si no hay más líneas
        }
    }

    // Coroutine para mostrar el texto letra por letra
    private IEnumerator TypeDialogue(string line)
    {
        if (dialogueText == null) yield break;

        dialogueText.gameObject.SetActive(true);
        dialogueText.text = "";
        bool skipped = false; // Flag para saber si se saltó el tipeo

        // Tipeo letra por letra
        foreach (char letter in line.ToCharArray())
        {
            // --- Comprobación de Skip durante el tipeo ---
             // Si se presiona espacio (o clic) mientras tipea, completar la línea
             // Puedes cambiar la condición a Input.GetMouseButtonDown(0) para clic
             if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
             {
                 skipped = true;
                 break; // Salir del bucle foreach
             }
            // ---------------------------------------------
            dialogueText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }

        // Si se saltó, mostrar texto completo inmediatamente
        if (skipped) {
            dialogueText.text = line;
             //Debug.Log("Typing skipped.");
        }


        // --- MODIFICADO: Esperar a que termine el audio ---
        // Esperar SOLO si hay un AudioSource y está reproduciendo
        if (dialogueAudioSource != null && dialogueAudioSource.isPlaying)
        {
             //Debug.Log("Typing finished, waiting for audio...");
             // Esperar mientras el audio esté sonando
            yield return new WaitWhile(() => dialogueAudioSource.isPlaying);
             //Debug.Log("Audio finished.");
        }
        else
        {
            // Si no había audio, esperar un tiempo corto por defecto para poder leer
            yield return new WaitForSeconds(1.0f); // Tiempo corto si no hay audio
        }
        // -----------------------------------------------

        // Llamar a la siguiente línea DESPUÉS de esperar al audio (o la pausa corta)
        displayCoroutine = null; // Limpiar corutina antes de llamar a la siguiente
        ShowNextDialogue();
    }

    // Oculta la UI del diálogo
    public void HideDialogue()
    {
        if (displayCoroutine != null) { StopCoroutine(displayCoroutine); displayCoroutine = null; }
        if (dialogueAudioSource != null) dialogueAudioSource.Stop(); // Detener audio
        if (mainPanel != null) mainPanel.gameObject.SetActive(false);
        isDialogueActive = false;
        //Debug.Log("Dialogue UI Hidden.");
    }

    // Se llama cuando se han mostrado todas las líneas
    private void EndDialogueSequence()
    {
        //Debug.Log("Dialogue sequence finished. Loading next scene...");
        if (displayCoroutine != null) { StopCoroutine(displayCoroutine); displayCoroutine = null; }
        if (dialogueAudioSource != null) dialogueAudioSource.Stop(); // Detener audio
        isDialogueActive = false;

        // Cargar la siguiente escena (asegúrate que el nombre es correcto)
        SceneManager.LoadScene("AnimationOutro");
    }

    // Completa la escritura de la línea actual y detiene el audio
    public void CompleteCurrentDialogue()
    {
        if (isDialogueActive && displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine); // Detener tipeo
             displayCoroutine = null; // Limpiar para evitar que llame a ShowNextDialogue desde la corutina detenida

            if (currentDialogueIndex > 0 && currentDialogueIndex <= dialogueLines.Length)
                dialogueText.text = dialogueLines[currentDialogueIndex - 1]; // Mostrar texto completo

            // Detener audio actual
            if (dialogueAudioSource != null) dialogueAudioSource.Stop();

            // Llamar a la siguiente línea inmediatamente
             //Debug.Log("Dialogue skipped, showing next line.");
            ShowNextDialogue(); // Inicia el proceso para la siguiente línea (incluyendo su audio)
        }
    }

    // Eliminada Corutina WaitAndShowNext (ya no necesaria con la nueva lógica de CompleteCurrentDialogue)

    // Permite consultar si el diálogo está activo
    public bool IsDialogueActive() { return isDialogueActive; }

    // Update para saltar con Espacio o Clic (MODIFICADO)
    void Update()
    {
        // Saltar línea actual si se presiona Espacio O Click Izquierdo
        // Y si el diálogo está activo (mostrando texto o audio)
        if (isDialogueActive && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
        {
            // Si la corutina de tipeo aún está activa, la completamos
            if (displayCoroutine != null)
            {
                 //Debug.Log("Input detectado durante tipeo: Completando línea...");
                 // Llamar a CompleteCurrentDialogue que ahora maneja el salto y pasar a la siguiente
                 CompleteCurrentDialogue();
            }
            // Si la corutina de tipeo ya terminó pero el audio sigue sonando
            else if (dialogueAudioSource != null && dialogueAudioSource.isPlaying)
            {
                 //Debug.Log("Input detectado durante espera de audio: Pasando a siguiente línea...");
                 // Detener audio y pasar a la siguiente línea
                 dialogueAudioSource.Stop();
                 ShowNextDialogue();
            }
        }
    }
} // Fin de la clase