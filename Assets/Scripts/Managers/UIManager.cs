using UnityEngine;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    private Dictionary<string, bool> canvasStates = new Dictionary<string, bool>();

    private void Awake()
    {
        // Inicializar estados del canvas
        InitializeCanvasStates();
    }

    private void InitializeCanvasStates()
    {
        // Inicializar estados de UI conocidos
        SetCanvasState("Inventory", false);
        SetCanvasState("QuestLog", false);
        SetCanvasState("Map", false);
        // ... agregar más estados según sea necesario
    }

    public void SetCanvasState(string elementId, bool state)
    {
        canvasStates[elementId] = state;
        UpdateUIElement(elementId, state);
    }

    private void UpdateUIElement(string elementId, bool state)
    {
        // Buscar el elemento UI por su nombre y actualizar su estado
        GameObject uiElement = GameObject.Find(elementId);
        if (uiElement != null)
        {
            uiElement.SetActive(state);
        }
    }

    public bool GetCanvasState(string elementId)
    {
        return canvasStates.TryGetValue(elementId, out bool state) && state;
    }

    public Dictionary<string, bool> GetCanvasStates()
    {
        return new Dictionary<string, bool>(canvasStates);
    }

    public void LoadCanvasStates(Dictionary<string, bool> states)
    {
        foreach (var state in states)
        {
            SetCanvasState(state.Key, state.Value);
        }
    }   
}