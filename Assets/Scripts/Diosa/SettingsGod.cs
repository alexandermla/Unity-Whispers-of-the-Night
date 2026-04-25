using UnityEngine;

public class SettingsGod : MonoBehaviour
{
    public GameObject settingsPanel;
    private bool isSettingsOpen = false;

    private void Start()
    {
        //settingsPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isSettingsOpen)
            {
                settingsPanel.SetActive(false);
                isSettingsOpen = false;
                Time.timeScale = 1f; // Resume the game
            }
            else
            {
                settingsPanel.SetActive(true);
                isSettingsOpen = true;
                Time.timeScale = 0f; // Pause the game
            }
        }
    }
}
