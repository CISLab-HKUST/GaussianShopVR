using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Newtonsoft.Json;

public class MergeModelTool : MonoBehaviour
{
    private GaussianSplatting gaussianSplatting;
    public InputActionProperty ConfirmButtonInput;
    private ModelManager modelManager;
    private ServerSyncer serverSyncer;

    // Start is called before the first frame update
    void Start()
    {
        gaussianSplatting = FindFirstObjectByType<GaussianSplatting>();
        modelManager = ModelManager.Instance;
        serverSyncer = ServerSyncer.Instance;
    }

    // Update is called once per frame
    void Update()
    {
        if (ConfirmButtonInput.action.triggered)
        {
            MergeActivatedModels();
        }
    }

    public async void MergeActivatedModels()
    {
        List<GameObject> activatedGSs = modelManager.GetActiveModels();
        if (activatedGSs.Count != 2)
        {
            ReminderManager.Instance.ShowReminder("Need two activated leaf models to merge.");
            return;
        }
        foreach (var entry in activatedGSs)
        {
            if (entry.GetComponent<ObjInfo>().remoteInfo.is_leaf == false)
            {
                ReminderManager.Instance.ShowReminder(
                    "Both activated models need to be leaf models to merge."
                );
                return;
            }
        }
        foreach (var entry in activatedGSs)
        {
            modelManager.RemoveModel(entry);
        }

        LocalModelData localModel = await serverSyncer.MergeModels(
            activatedGSs[0].GetComponent<ObjInfo>().remoteInfo.id,
            activatedGSs[1].GetComponent<ObjInfo>().remoteInfo.id
        );

        ModelManager.Instance.AddModel(localModel);
    }

    // public void SaveAllModels()
    // {
    //     string projectPath = gaussianSplatting.SaveAllModels();
    //     Debug.Log("Save All Models: " + projectPath);

    //     SceneData sceneData = new SceneData();

    //     foreach (GameObject model in GSModelList)
    //     {
    //         SaveTransformData transformData = new SaveTransformData();

    //         transformData.position[0] = model.transform.position.x;
    //         transformData.position[1] = model.transform.position.y;
    //         transformData.position[2] = model.transform.position.z;

    //         transformData.rotation[0] = model.transform.rotation.x;
    //         transformData.rotation[1] = model.transform.rotation.y;
    //         transformData.rotation[2] = model.transform.rotation.z;
    //         transformData.rotation[3] = model.transform.rotation.w;

    //         transformData.scale[0] = model.transform.localScale.x;
    //         transformData.scale[1] = model.transform.localScale.y;
    //         transformData.scale[2] = model.transform.localScale.z;

    //         GaussianSplattingModel gsModel = model.GetComponent<GaussianSplattingModel>();
    //         if (gsModel != null)
    //         {
    //             transformData.modelId = gsModel.modelId;
    //         }
    //         transformData.modelFilePath = Path.Combine(
    //             projectPath,
    //             gsModel.modelId.ToString() + ".ply"
    //         );
    //         sceneData.models.Add(transformData);
    //     }

    //     string json = JsonUtility.ToJson(sceneData);
    //     string filePath = Path.Combine(projectPath, "scene_data.json");
    //     File.WriteAllText(filePath, json);
    //     Debug.Log("Scene data saved to: " + filePath);
    // }

    // public void SavePreset()
    // {
    //     gaussianSplatting.SaveModel(GetActiveModelID());
    //     ReminderManager.Instance.ShowReminder("Preset Saved");
    // }
}
