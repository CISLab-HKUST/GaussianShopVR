using UnityEngine;

public class PanelsManager : MonoBehaviour
{
    [System.Serializable]
    public class Panel
    {
        public string panelName;
        public GameObject panelObject;
    }

    public Panel[] panels;
    private GameObject currentPanel;

    private void Start()
    {
        if (panels.Length > 0)
        {
            SwitchPanel(panels[0].panelName);
        }
    }

    public void SwitchPanel(string panelName)
    {
        foreach (var panel in panels)
        {
            panel.panelObject.SetActive(false);
            if (panel.panelName == panelName)
            {
                panel.panelObject.SetActive(true);
                currentPanel = panel.panelObject;
            }
        }

        Debug.LogWarning($"Panel with name '{panelName}' not found!");
    }
}
