using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class ColorTool : MonoBehaviour
{
    public GameObject CurveParent;
    private GaussianSplatting gs;
    private bool isColorAdjusting = false;
    public float adjustInterval = 0.1f;
    private float lastAdjustTime = 0f;
    private ModelManager modelManager;
    private ServerSyncer serverSyncer;
    private ColorManager colorManager;
    public InputActionProperty ConfirmButtonInput;
    public InputActionProperty PreviewButtonInput;

    void Awake()
    {
        gs = FindObjectOfType<GaussianSplatting>();
        modelManager = FindObjectOfType<ModelManager>();
        serverSyncer = ServerSyncer.Instance;
        colorManager = CurveParent.GetComponent<ColorManager>();
        CurveParent.SetActive(false);
    }

    void Update()
    {
        if (isColorAdjusting)
        {
            if (Time.time - lastAdjustTime >= adjustInterval)
            {
                ColorAdjust();
                lastAdjustTime = Time.time;
            }
        }
    }

    private void OnEnable()
    {
        gs.BeginColorAdjust();
        isColorAdjusting = true;
        lastAdjustTime = Time.time;
        CurveParent.SetActive(true);
        ConfirmButtonInput.action.performed += OnConfirmButtonPressed;
        PreviewButtonInput.action.performed += OnPreviewButtonPressed;
        PreviewButtonInput.action.canceled += OnPreviewButtonCanceled;
    }

    private void OnDisable()
    {
        CurveParent.SetActive(false);
        isColorAdjusting = false;
        gs.ClearSelection();
        gs.EndColorAdjust();
        ConfirmButtonInput.action.performed -= OnConfirmButtonPressed;
        PreviewButtonInput.action.performed -= OnPreviewButtonPressed;
        PreviewButtonInput.action.canceled -= OnPreviewButtonCanceled;
    }

    void OnConfirmButtonPressed(InputAction.CallbackContext context)
    {
        RGBData rgbData = colorManager.GetRGBValue();
        foreach (var model in modelManager.GetActiveModels())
        {
            List<int> pointIndices = gs.GetSelectedPoints(
                model.GetComponent<GaussianSplattingModel>()
            );
            serverSyncer.AdjustColor(
                model.GetComponent<ObjInfo>().remoteInfo.id,
                pointIndices,
                rgbData.R,
                rgbData.G,
                rgbData.B
            );
        }
        gs.EndColorAdjust();
        gs.ClearSelection();
        gs.BeginColorAdjust();
    }

    void OnPreviewButtonPressed(InputAction.CallbackContext context)
    {
        gs.SetShowCenter(false);
    }

    void OnPreviewButtonCanceled(InputAction.CallbackContext context)
    {
        gs.SetShowCenter(true);
    }

    void ColorAdjust()
    {
        RGBData rgbData = colorManager.GetRGBValue();
        float[] rArray = rgbData.R.ToArray();
        float[] gArray = rgbData.G.ToArray();
        float[] bArray = rgbData.B.ToArray();
        gs.ColorAdjustCuda(rArray, gArray, bArray);
    }
}
