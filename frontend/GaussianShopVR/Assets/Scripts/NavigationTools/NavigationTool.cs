using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class NavigationTool : MonoBehaviour
{
    [Header("Input References")]
    public InputActionReference switchLevelAction;
    private int level = 0;

    void OnEnable()
    {
        UpdateObjectState();
        switchLevelAction.action.performed += OnSwitchLevelActionPerformed;
    }

    void OnDisable()
    {
        switchLevelAction.action.performed -= OnSwitchLevelActionPerformed;
    }

    public void UpdateObjectState()
    {
        setLevelInteractive(level);
    }

    void OnSwitchLevelActionPerformed(InputAction.CallbackContext context)
    {
        level = level + 1;
        if (ModelManager.Instance.GetModelByLevel(level).Count == 0)
        {
            level = 0;
        }
        setLevelInteractive(level);
    }

    void setLevelInteractive(int level)
    {
        foreach (var entry in ModelManager.Instance.GSModelList)
        {
            entry.GetComponent<BoxCollider>().enabled = false;
            entry.GetComponent<ObjInfo>().showSelectionBox = false;
        }
        foreach (var entry in ModelManager.Instance.GetModelByLevel(level))
        {
            entry.GetComponent<BoxCollider>().enabled = true;
            entry.GetComponent<ObjInfo>().showSelectionBox = true;
        }
    }
}
