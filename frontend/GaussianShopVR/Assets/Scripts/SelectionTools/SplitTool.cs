using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class SplitTool : MonoBehaviour
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

    async void OnConfirmButtonPressed(InputAction.CallbackContext context)
    {
        foreach (var model in modelManager.GetActiveModels())
        {
            List<int> pointIndices = gs.GetSelectedPoints(
                model.GetComponent<GaussianSplattingModel>()
            );
            LocalModelData localModel = await serverSyncer.SplitModel(
                model.GetComponent<ObjInfo>().remoteInfo.id,
                pointIndices
            );
            string filepath = gs.SplitPointsFromCuda(model.GetComponent<GaussianSplattingModel>());
            localModel.local_path = filepath;
            modelManager.AddModel(localModel);
        }
    }
}
