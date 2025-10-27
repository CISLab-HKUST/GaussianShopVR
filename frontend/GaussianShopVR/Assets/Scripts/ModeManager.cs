using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.ComponentModel;
using Unity.VisualScripting;
using Unity.XR.CoreUtils;

public enum Modes
{
    NavigationMode,
    SelectionMode,
    DrawingMode,
}

public class ModeManager : MonoBehaviour
{
    public static ModeManager Instance { get; private set; }

    [Header("Mode Settings")]
    public Modes CurrentMode = Modes.NavigationMode;

    [Header("UI References")]
    public TMP_Dropdown modeDropdown;
    public TextMeshProUGUI modeIndicator;

    [Header("Tool Sets")]
    public GameObject navigationToolSet;
    public GameObject selectionToolSet;
    public GameObject drawingToolSet;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        SwitchMode(CurrentMode);
        modeDropdown.onValueChanged.AddListener(OnModeChanged);
    }

    public void SwitchMode(Modes newMode)
    {
        CurrentMode = newMode;
        UpdateModeState();
        modeIndicator.text = newMode.ToString().Insert(newMode.ToString().Length - 4, " ");
    }

    void OnModeChanged(int index)
    {
        switch (index)
        {
            case 0:
                SwitchMode(Modes.NavigationMode);
                break;
            case 1:
                SwitchMode(Modes.SelectionMode);
                break;
            case 2:
                SwitchMode(Modes.DrawingMode);
                break;
            default:
                Debug.LogWarning("Invalid mode index");
                break;
        }

        Debug.Log($"Switched to: {modeDropdown.options[index].text}");
    }

    public void UpdateObjectState()
    {
        switch (CurrentMode)
        {
            case Modes.NavigationMode:
                FindObjectsOfType<NavigationTool>()[0].UpdateObjectState();
                break;
            case Modes.SelectionMode:
                FindObjectsOfType<SelectionTool>()[0].UpdateObjectState();
                break;
            case Modes.DrawingMode:
                FindObjectsOfType<DrawingTool>()[0].UpdateObjectState();
                break;
        }
    }

    private void UpdateModeState()
    {
        navigationToolSet.SetActive(false);
        selectionToolSet.SetActive(false);
        drawingToolSet.SetActive(false);
        switch (CurrentMode)
        {
            case Modes.NavigationMode:
                navigationToolSet.SetActive(true);
                break;
            case Modes.SelectionMode:
                selectionToolSet.SetActive(true);
                break;
            case Modes.DrawingMode:
                drawingToolSet.SetActive(true);
                break;
        }
    }
}
