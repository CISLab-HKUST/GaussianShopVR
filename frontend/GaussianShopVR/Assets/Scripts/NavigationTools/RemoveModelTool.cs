using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RemoveModelTool : MonoBehaviour
{
    public InputActionProperty ConfirmButtonInput;
    private ModelManager modelManager;
    private ServerSyncer serverSyncer;

    void Start()
    {
        modelManager = ModelManager.Instance;
        serverSyncer = ServerSyncer.Instance;
    }

    // Update is called once per frame
    void Update()
    {
        if (ConfirmButtonInput.action.triggered)
        {
            RemoveActivatedModels();
        }
    }

    public void RemoveActivatedModels()
    {
        List<GameObject> activatedGSs = modelManager.GetActiveModels();
        for (int i = 0; i < activatedGSs.Count; i++)
        {
            serverSyncer.RemoveModel(activatedGSs[i].GetComponent<ObjInfo>().remoteInfo.id);
            modelManager.RemoveModel(activatedGSs[i]);
        }
    }
}
