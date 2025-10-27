using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RemovePointTool : MonoBehaviour
{
    private GaussianSplatting gs;
    public InputActionProperty ConfirmButtonInput;
    private ModelManager modelManager;
    private ServerSyncer serverSyncer;

    void Awake()
    {
        gs = FindFirstObjectByType<GaussianSplatting>();
        modelManager = ModelManager.Instance;
        serverSyncer = ServerSyncer.Instance;
    }

    void OnEnable()
    {
        ConfirmButtonInput.action.performed += OnConfirmButtonPressed;
    }

    void OnDisable()
    {
        ConfirmButtonInput.action.performed -= OnConfirmButtonPressed;
    }

    void OnConfirmButtonPressed(InputAction.CallbackContext context)
    {
        foreach (var model in modelManager.GetActiveModels())
        {
            List<int> pointIndices = gs.GetSelectedPoints(
                model.GetComponent<GaussianSplattingModel>()
            );
            serverSyncer.RemovePoints(model.GetComponent<ObjInfo>().remoteInfo.id, pointIndices);
            gs.RemovePointsFromCuda(model.GetComponent<GaussianSplattingModel>());
        }
    }
}
