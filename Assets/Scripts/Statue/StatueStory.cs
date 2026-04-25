// StatueStory.cs MODIFICADO
using UnityEngine;
using TMPro;
using UnityEngine.Localization; // <-- AÑADIR

[CreateAssetMenu(fileName = "New Statue Story", menuName = "World of Nightmares/Statue Story")]
public class StatueStory : ScriptableObject
{
    [Header("Identificación")]
    public string statueId; // Se mantiene como identificador interno
    // public string title; // <-- ELIMINADO
    [SerializeField] public LocalizedString localizedTitle; // <-- NUEVO: Título localizado

    [Header("Contenido")]
    // [TextArea(5, 10)]
    // public string storyText; // <-- ELIMINADO
    [SerializeField] [TextArea(5, 10)] public LocalizedString localizedStoryText; // <-- NUEVO: Texto localizado
    public Sprite storyImage; // Opcional - imagen relacionada con la historia

    [Header("Estilo (Opcional, si el UIManager no lo controla)")]
    public Color titleColor = Color.white;
    public Color textColor = Color.white;
    public TMP_FontAsset customFont; // Cambiado de Font a TMP_FontAsset
}