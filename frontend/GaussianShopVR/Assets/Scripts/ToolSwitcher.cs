using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using Unity.VisualScripting;

public class ToolSwitcher : MonoBehaviour
{
    public TextMeshProUGUI toolText;
    private int currentToolIndex = 0;
    public GameObject[] tools = { };
    private float inputThreshold = 0.5f;
    private float previousInputValue;
    private float switchCooldown = 0.5f;
    private float lastSwitchTime = 0f;
    public string currentTool = " ";
    public InputActionProperty right2DAxis;

    private void Awake() { }

    private void OnEnable()
    {
        UpdateTool();
    }

    void Update()
    {
        float inputValue = right2DAxis.action.ReadValue<Vector2>().y;

        if (Mathf.Abs(inputValue) > inputThreshold && Time.time - lastSwitchTime > switchCooldown)
        {
            if (inputValue > 0)
            {
                currentToolIndex = (currentToolIndex + 1) % tools.Length;
            }
            else if (inputValue < 0)
            {
                currentToolIndex = (currentToolIndex - 1 + tools.Length) % tools.Length;
            }
            UpdateTool();
            lastSwitchTime = Time.time;
            previousInputValue = inputValue;
        }
        if (Mathf.Abs(inputValue) < inputThreshold)
        {
            previousInputValue = 0f;
        }
    }

    public void UpdateTool()
    {
        for (int i = 0; i < tools.Length; i++)
        {
            tools[i].SetActive(false);
        }
        toolText.text = tools[currentToolIndex].name;
        currentTool = tools[currentToolIndex].name;
        tools[currentToolIndex].SetActive(true);
    }
}
