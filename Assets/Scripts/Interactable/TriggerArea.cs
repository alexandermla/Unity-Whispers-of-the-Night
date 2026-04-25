using UnityEngine;

public class TriggerArea : MonoBehaviour
{
    [SerializeField] private string triggerType;
    [SerializeField] private TutorialQuestManager questManager;
    [SerializeField] private string questToActivate;
    [SerializeField] private string triggerToComplete;
    [SerializeField] private bool activateOnce = true;
    
    private bool hasBeenTriggered = false;
    
    private void OnTriggerEnter(Collider other)
    {
        if (hasBeenTriggered && activateOnce) return;
        
        if (other.CompareTag("Player"))
        {
            ProcessTrigger();
        }
    }
    
    private void ProcessTrigger()
    {
        if (questManager == null) return;
        
        switch (triggerType)
        {
            case "ActivateQuest":
                if (!string.IsNullOrEmpty(questToActivate))
                {
                    questManager.ActivateQuest(questToActivate);
                }
                break;
            
            case "CompleteQuest":
                if (!string.IsNullOrEmpty(triggerToComplete))
                {
                    questManager.CompleteQuest(triggerToComplete);
                }
                break;
            
            case "JumpTrigger":
                // Solo activamos, no completamos
                if (!string.IsNullOrEmpty(questToActivate))
                {
                    questManager.ActivateQuest(questToActivate);
                    Debug.Log($"Jump mission activated: {questToActivate}");
                }
                break;
            
            case "FlashlightArea":
                questManager.ActivateQuest("TUT_INTERACT");
                break;
            
            case "ExitDoor":
                questManager.TriggerReachedExit();
                break;
        }
        
        hasBeenTriggered = true;
    }
}