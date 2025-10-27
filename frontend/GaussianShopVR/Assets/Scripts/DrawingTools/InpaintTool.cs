using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class InpaintTool : MonoBehaviour
{
    private GaussianSplatting gs;
    public InputActionProperty ConfirmButtonInput;
    private ModelManager modelManager;
    private ServerSyncer serverSyncer;

    public TMPro.TextMeshProUGUI promptText;

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
        string textPrompt = promptText.text;
        Debug.Log("Inpaint Prompt: " + textPrompt);

        ReminderManager.Instance.BeginReminder("Inpainting...");
        string inpaint_url = serverSyncer.serverURL + "/inpaint";

        GameObject model = ModelManager.Instance.GetActiveModels()[0];
        ObjInfo objInfo = model.GetComponent<ObjInfo>();

        List<CamData> cams = new();

        foreach (var cameraPreviewItem in DrawingTool.Instance.cameraPreviewList)
        {
            cams.Add(cameraPreviewItem.GetComponent<CameraPreviewItem>().camData);
        }

        LocalModelData localModel = await serverSyncer.Inpaint(
            objInfo.remoteInfo.id,
            Convert(objInfo.Points, objInfo.Colors),
            textPrompt,
            cams
        );
        if (localModel != null)
        {
            modelManager.AddModel(localModel);
        }
    }

    public List<float> Convert(List<Vector3> vector3List, List<Color> colorList)
    {
        List<float> floatList = new List<float>();
        for (int i = 0; i < vector3List.Count; i++)
        {
            floatList.Add((float)vector3List[i].x);
            floatList.Add((float)vector3List[i].y);
            floatList.Add((float)vector3List[i].z);
            floatList.Add(colorList[i].r);
            floatList.Add(colorList[i].g);
            floatList.Add(colorList[i].b);
        }
        return floatList;
    }
}
