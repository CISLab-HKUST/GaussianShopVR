using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class GenerationTool : MonoBehaviour
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
        foreach (var model in modelManager.GetActiveModels())
        {
            ObjInfo objInfo = model.GetComponent<ObjInfo>();
            string textPrompt = promptText.text;
            List<float> point_prompt = Convert(objInfo.Points, objInfo.Colors);
            LocalModelData localModel = await serverSyncer.Generate(
                objInfo.remoteInfo.id,
                point_prompt,
                textPrompt
            );
            if (localModel != null)
            {
                modelManager.AddModel(localModel);
            }
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
