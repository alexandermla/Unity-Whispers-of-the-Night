using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SocialPlatforms;
using System.Collections;
public class Prueba : MonoBehaviour
{

    private bool _active = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        int ID = PlayerPrefs.GetInt("LocaleKey", 0);
        ChangeLocale(ID);
    }

    public void ChangeLocale(int localeID)
    {
        if (_active)
        {
            return;
        }
        StartCoroutine(SetLocale(localeID));
    }   

    private IEnumerator SetLocale(int localeID)
    {
        _active = true;
        yield return LocalizationSettings.InitializationOperation;
        
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[localeID];
        PlayerPrefs.SetInt("LocalKey", localeID);
        _active = false;
    }
}
