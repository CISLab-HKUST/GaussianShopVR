using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using UnityEngine.SocialPlatforms;
using System;

public class ServerSyncer : MonoBehaviour
{
    // Static instance of the ServerSyncer which allows it to be accessed by any other script.
    public static ServerSyncer Instance { get; private set; }

    public string serverURL = "http://yourserver.com/";
    public string openAIKey = "sk-**********";

    [SerializeField]
    private GameObject GaussianModelParent;

    private void Awake()
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
        ReloadServer();
    }

    private void Start()
    {
    }

    private async void ReloadServer()
    {
        await WebUtils.PostRequestAsync(serverURL + "/reload");
    }

    public async void RemovePoints(int modelId, List<int> pointIndices)
    {
        if (pointIndices == null || pointIndices.Count == 0)
        {
            Debug.Log("No points to remove.");
            return;
        }

        var requestData = new RemovePointData { id = modelId, point_index = pointIndices };

        string jsonData = JsonUtility.ToJson(requestData);
        Debug.Log("Sending Remove Points Request: " + jsonData);

        string response = await WebUtils.PostRequestAsync(serverURL + "/remove_points", jsonData);
    }

    public async Task<LocalModelData> SplitModel(int modelId, List<int> pointIndices)
    {
        if (pointIndices == null || pointIndices.Count == 0)
        {
            Debug.Log("No points to split.");
            return null;
        }

        var requestData = new SplitModelData { id = modelId, point_index = pointIndices };

        string jsonData = JsonUtility.ToJson(requestData);
        Debug.Log("Sending Split Model Request: " + jsonData);

        string response = await WebUtils.PostRequestAsync(serverURL + "/split", jsonData);
        ModelData modelData = JsonConvert.DeserializeObject<ModelData>(response);
        // string localPath = await WebUtils.DownloadPlyFile(serverURL, modelData);

        LocalModelData localModel = new LocalModelData();
        localModel.modelData = modelData;
        // localModel.local_path = localPath;
        localModel.modelObj = GameObject.Find("GS" + localModel.modelData.id.ToString());
        return localModel;
    }

    public async void AdjustColor(
        int modelId,
        List<int> pointIndices,
        List<float> rList,
        List<float> gList,
        List<float> bList
    )
    {
        if (pointIndices == null || pointIndices.Count == 0)
        {
            Debug.Log("No points to adjust color.");
            return;
        }

        var requestData = new ColorAdjustData
        {
            id = modelId,
            point_index = pointIndices,
            R = rList,
            G = gList,
            B = bList
        };

        string jsonData = JsonUtility.ToJson(requestData);
        Debug.Log("Sending Color Adjust Request: " + jsonData);

        string response = await WebUtils.PostRequestAsync(serverURL + "/adjust_color", jsonData);
    }

    public async Task<LocalModelData> Generate(
        int modelId,
        List<float> pointPromt,
        string textPrompt
    )
    {
        if (pointPromt == null || pointPromt.Count == 0)
        {
            Debug.Log("No points to generate.");
            return null;
        }

        var requestData = new GenerationData
        {
            id = modelId,
            point_prompt = pointPromt,
            text_prompt = textPrompt
        };

        string jsonData = JsonUtility.ToJson(requestData);
        Debug.Log("Sending Generation Request: " + jsonData);

        // string response = await WebUtils.PostRequestAsync(serverURL + "/generate", jsonData);

        // ModelData modelData = JsonConvert.DeserializeObject<ModelData>(response);
        // string localPath = await WebUtils.DownloadPlyFile(serverURL, modelData);
        // LocalModelData localModel = new LocalModelData();
        // localModel.modelData = modelData;
        // localModel.local_path = localPath;
        // localModel.modelObj = GameObject.Find("GS" + localModel.modelData.id.ToString());
        // return localModel;
        return null;
    }

    public async Task<LocalModelData> Inpaint(
        int modelId,
        List<float> pointPromt,
        string textPrompt,
        List<CamData> cams,
        int contextId = 0
    )
    {
        var requestData = new InpaintData
        {
            id = modelId,
            point_prompt = pointPromt,
            text_prompt = textPrompt,
            context_id = contextId,
            cams = cams
        };

        string jsonData = JsonUtility.ToJson(requestData);
        Debug.Log("Sending Inpaint Request: " + jsonData);

        // string response = await WebUtils.PostRequestAsync(serverURL + "/inpaint", jsonData);

        // ModelData modelData = JsonConvert.DeserializeObject<ModelData>(response);
        // string localPath = await WebUtils.DownloadPlyFile(serverURL, modelData);
        // LocalModelData localModel = new LocalModelData();
        // localModel.modelData = modelData;
        // localModel.local_path = localPath;
        // localModel.modelObj = GameObject.Find("GS" + localModel.modelData.id.ToString());
        // return localModel;
        return null;
    }

    public async void SaveModel(int modelId, string presetName = "None")
    {
        var requestData = new SavePresetData { id = modelId, preset_name = presetName };
        string jsonData = JsonUtility.ToJson(requestData);
        string response = await WebUtils.PostRequestAsync(serverURL + "/save_preset", jsonData);
    }

    public async void RemoveModel(int modelId)
    {
        var requestData = new RemoveObjRequestData { id = modelId };
        string jsonData = JsonUtility.ToJson(requestData);
        string response = await WebUtils.PostRequestAsync(serverURL + "/remove_obj", jsonData);
    }

    public async Task<LocalModelData> MergeModels(int modelId1, int modelId2)
    {
        var requestData = new MergeModelsRequestData { id1 = modelId1, id2 = modelId2 };
        string jsonData = JsonUtility.ToJson(requestData);
        string response = await WebUtils.PostRequestAsync(serverURL + "/merge_obj", jsonData);

        ModelData modelData = JsonConvert.DeserializeObject<ModelData>(response);
        string localPath = await WebUtils.DownloadPlyFile(serverURL, modelData);

        LocalModelData localModel = new LocalModelData();
        localModel.modelData = modelData;
        localModel.local_path = localPath;
        localModel.modelObj = GameObject.Find("GS" + localModel.modelData.id.ToString());
        return localModel;
    }

    public async void SendTransformData(int modelId, Transform targetObject)
    {
        Debug.Log(serverURL + "/transform");

        // Calculate the relative transformation
        Vector3 relativePosition = targetObject.localPosition;
        Quaternion relativeRotation = targetObject.localRotation;
        Vector3 relativeScale = targetObject.localScale;

        TransformData requestData = new TransformData
        {
            id = modelId,
            translation = new float[]
            {
                relativePosition.x,
                relativePosition.y,
                relativePosition.z
            },
            wxyz = new float[]
            {
                relativeRotation.w,
                relativeRotation.x,
                relativeRotation.y,
                relativeRotation.z
            },
            scale = new float[] { relativeScale.x, relativeScale.y, relativeScale.z }
        };

        string jsonData = JsonUtility.ToJson(requestData);
        Debug.Log(jsonData);
        string response = await WebUtils.PostRequestAsync(serverURL + "/transform", jsonData);
    }
}
