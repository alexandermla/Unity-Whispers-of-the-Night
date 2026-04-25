using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }
    
    [System.Serializable]
    public class Quest
    {
        public string questID;
        public string questText;
        public string completionTrigger;
        public bool isActive = false;
        public bool isCompleted = false;
        public bool autoActivate = false;
        public Color textColor = Color.white;
        public string[] requiredEvents;  // Eventos que deben ocurrir para completar la misión
        public Quest nextQuest;          // Misión que se activa automáticamente al completar esta
    }
    
    [Header("Quest Settings")]
    [SerializeField] private List<Quest> availableQuests = new List<Quest>();
    [SerializeField] private Quest currentQuest;
    
    [Header("UI References")]
    [SerializeField] private GameObject questPanel;
    [SerializeField] private TextMeshProUGUI questText;
    [SerializeField] private GameObject questCompletedPanel;
    [SerializeField] private TextMeshProUGUI completedQuestText;
    [SerializeField] private float questDisplayDuration = 5f;
    [SerializeField] private float completionDisplayDuration = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip questActivatedSound;
    [SerializeField] private AudioClip questCompletedSound;
    
    // Eventos del sistema de misiones
    private Dictionary<string, bool> triggeredEvents = new Dictionary<string, bool>();
    
    private Coroutine questDisplayCoroutine;
    private Coroutine completionDisplayCoroutine;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Ocultar paneles UI
        if (questPanel != null)
            questPanel.SetActive(false);
            
        if (questCompletedPanel != null)
            questCompletedPanel.SetActive(false);
    }
    
    private void Start()
    {
        // Activar misiones auto-activables
        ActivateAutoQuests();
    }
    
    private void ActivateAutoQuests()
    {
        foreach (var quest in availableQuests)
        {
            if (quest.autoActivate && !quest.isCompleted && !quest.isActive)
            {
                ActivateQuest(quest.questID);
                break;  // Activar solo la primera misión auto-activable
            }
        }
    }

    // Métodos para el sistema de guardado
    public List<Quest> GetAvailableQuests()
    {
        return availableQuests;
    }

    public Quest GetCurrentQuest()
    {
        return currentQuest;
    }

    public List<string> GetTriggeredEvents()
    {
        return new List<string>(triggeredEvents.Keys);
    }

    public void RestoreEvent(string eventName)
    {
        if (!triggeredEvents.ContainsKey(eventName))
        {
            triggeredEvents[eventName] = true;
        }
    }

    public void RestoreQuestState(string questId, int state)
    {
        Quest quest = availableQuests.Find(q => q.questID == questId);
        if (quest != null)
        {
            quest.isActive = (state == 1);
            quest.isCompleted = (state == 2);
        }
    }

    public void RestoreCurrentQuest(string questId)
    {
        Quest quest = availableQuests.Find(q => q.questID == questId);
        if (quest != null)
        {
            currentQuest = quest;
            if (quest.isActive)
            {
                DisplayQuestUI(quest.questText, quest.textColor);
            }
        }
    }
    
    public void ActivateQuest(string questID)
    {
        Quest newQuest = availableQuests.Find(q => q.questID == questID);
        
        if (newQuest == null)
        {
            Debug.LogWarning($"Quest {questID} not found!");
            return;
        }
        
        if (newQuest.isCompleted)
        {
            Debug.Log($"Quest {questID} already completed, not activating.");
            return;
        }
        
        if (currentQuest != null)
        {
            currentQuest.isActive = false;
        }
        
        newQuest.isActive = true;
        currentQuest = newQuest;
        
        DisplayQuestUI(newQuest.questText, newQuest.textColor);
        
        if (audioSource != null && questActivatedSound != null)
        {
            audioSource.PlayOneShot(questActivatedSound);
        }
        
        Debug.Log($"Activated quest: {questID}");
    }

    public void ResetQuest(string questID)
    {
        Quest quest = availableQuests.Find(q => q.questID == questID);
        
        if (quest != null)
        {
            quest.isActive = false;
            quest.isCompleted = false;
            
            if (currentQuest != null && currentQuest.questID == questID)
            {
                currentQuest = null;
            }
            
            string[] keysToRemove = new string[triggeredEvents.Count];
            int index = 0;
            
            foreach (var key in triggeredEvents.Keys)
            {
                if (key.Contains(questID))
                {
                    keysToRemove[index] = key;
                    index++;
                }
            }
            
            for (int i = 0; i < index; i++)
            {
                if (triggeredEvents.ContainsKey(keysToRemove[i]))
                {
                    triggeredEvents.Remove(keysToRemove[i]);
                }
            }
            
            Debug.Log($"Misión {questID} reiniciada con éxito");
        }
        else
        {
            Debug.LogWarning($"No se encontró la misión {questID} para reiniciar");
        }
    }
    
    public void TriggerEvent(string eventName)
    {
        if (!triggeredEvents.ContainsKey(eventName))
        {
            triggeredEvents[eventName] = true;
            Debug.Log($"Triggered event: {eventName}");
            
            CheckEventCompletion(eventName);
        }
    }
    
    private void CheckEventCompletion(string eventName)
    {
        if (currentQuest != null && currentQuest.isActive)
        {
            if (currentQuest.completionTrigger == eventName)
            {
                CompleteCurrentQuest();
                return;
            }
            
            if (currentQuest.requiredEvents != null && currentQuest.requiredEvents.Length > 0)
            {
                bool allTriggered = true;
                
                foreach (string reqEvent in currentQuest.requiredEvents)
                {
                    if (!triggeredEvents.ContainsKey(reqEvent) || !triggeredEvents[reqEvent])
                    {
                        allTriggered = false;
                        break;
                    }
                }
                
                if (allTriggered)
                {
                    CompleteCurrentQuest();
                }
            }
        }
    }
    
    private void CompleteCurrentQuest()
    {
        if (currentQuest == null || !currentQuest.isActive || currentQuest.isCompleted)
            return;
            
        currentQuest.isCompleted = true;
        currentQuest.isActive = false;
        
        if (questCompletedPanel != null && completedQuestText != null)
        {
            completedQuestText.text = currentQuest.questText;
            completedQuestText.color = currentQuest.textColor;
            
            if (completionDisplayCoroutine != null)
                StopCoroutine(completionDisplayCoroutine);
                
            completionDisplayCoroutine = StartCoroutine(ShowCompletionUI());
        }
        
        if (audioSource != null && questCompletedSound != null)
        {
            audioSource.PlayOneShot(questCompletedSound);
        }
        
        Debug.Log($"Completed quest: {currentQuest.questID}");
        
        Quest nextQuest = currentQuest.nextQuest;
        currentQuest = null;
        
        if (nextQuest != null)
        {
            ActivateQuest(nextQuest.questID);
        }
    }
    
    private void DisplayQuestUI(string text, Color color)
    {
        if (questPanel == null || questText == null)
            return;
            
        if (questDisplayCoroutine != null)
            StopCoroutine(questDisplayCoroutine);
            
        questDisplayCoroutine = StartCoroutine(ShowQuestUI(text, color));
    }
    
    private IEnumerator ShowQuestUI(string text, Color color)
    {
        questText.text = text;
        questText.color = color;
        questPanel.SetActive(true);
        
        CanvasGroup canvasGroup = questPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = questPanel.AddComponent<CanvasGroup>();
            
        canvasGroup.alpha = 0;
        float duration = 0.5f;
        float elapsed = 0;
        
        while (elapsed < duration)
        {
            canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        canvasGroup.alpha = 1;
        
        yield return new WaitForSeconds(questDisplayDuration);
        
        elapsed = 0;
        while (elapsed < duration)
        {
            canvasGroup.alpha = Mathf.Lerp(1, 0, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        questPanel.SetActive(false);
    }
    
    private IEnumerator ShowCompletionUI()
    {
        questCompletedPanel.SetActive(true);
        
        CanvasGroup canvasGroup = questCompletedPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = questCompletedPanel.AddComponent<CanvasGroup>();
            
        canvasGroup.alpha = 0;
        float duration = 0.5f;
        float elapsed = 0;
        
        while (elapsed < duration)
        {
            canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        canvasGroup.alpha = 1;
        
        yield return new WaitForSeconds(completionDisplayDuration);
        
        elapsed = 0;
        while (elapsed < duration)
        {
            canvasGroup.alpha = Mathf.Lerp(1, 0, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        questCompletedPanel.SetActive(false);
    }
    
    public bool IsQuestActive(string questID)
    {
        Quest quest = availableQuests.Find(q => q.questID == questID);
        return quest != null && quest.isActive;
    }
    
    public bool IsQuestCompleted(string questID)
    {
        Quest quest = availableQuests.Find(q => q.questID == questID);
        return quest != null && quest.isCompleted;
    }
    
    public bool HasEventTriggered(string eventName)
    {
        return triggeredEvents.ContainsKey(eventName) && triggeredEvents[eventName];
    }
}