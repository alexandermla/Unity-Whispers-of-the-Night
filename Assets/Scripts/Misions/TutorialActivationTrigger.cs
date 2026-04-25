using UnityEngine;

public class TutorialActivationTrigger : MonoBehaviour
{
    [SerializeField] private VillageQuestManager questManager;
    [SerializeField] private string questToActivate = "LOCK_ON_TUTORIAL";
    [SerializeField] private bool oneTimeOnly = true;
    
    private bool hasActivated = false;
    
    private void OnTriggerEnter(Collider other)
    {
        if (oneTimeOnly && hasActivated) return;
        
        if (other.CompareTag("Player"))
        {
            if (questManager != null)
            {
                // Verificar si la misión ya está completada
                if (!questManager.IsQuestCompleted(questToActivate))
                {
                    questManager.ActivateQuest(questToActivate);
                    hasActivated = true;
                    Debug.Log($"TutorialActivationTrigger: Activated quest {questToActivate}");
                }
                else
                {
                    Debug.Log($"TutorialActivationTrigger: Quest {questToActivate} already completed, skipping activation");
                }
            }
            else
            {
                Debug.LogError("TutorialActivationTrigger: No VillageQuestManager assigned");
            }
        }
    }
}