using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HandMenuSwitcher : MonoBehaviour
{
    public InputActionReference handMenuActivate;
    public GameObject handMenuCanvas;
    public GameObject[] menuPanels;

    public GaussianSplattingCamera gscam;
    public TMPro.TextMeshProUGUI fpsText;
    public TMPro.TextMeshProUGUI resolutionText;
    public TMPro.TextMeshProUGUI resolutionPercent;
    public Slider texScaleSlider;
    public TMPro.TextMeshProUGUI lastMessage;
    public TMPro.TextMeshProUGUI splatCount;
    public Info gsInfo;
    private System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
    private int nb_frame = 0;
    private float cibleAngle = 0;

    // Start is called before the first frame update
    void Awake()
    {
        handMenuActivate.action.performed += HandMenuActivate_performed;
        GaussianSplatting gs = FindObjectOfType<GaussianSplatting>();
        //gs?.AddObserver(this);

        texScaleSlider.value = Mathf.Floor(gscam.texFactor * 10);
        fpsText.text = "";
        resolutionText.text = "";
    }

    private void HandMenuActivate_performed(InputAction.CallbackContext obj)
    {
        if (handMenuCanvas.activeSelf)
        {
            ModeManager.Instance.UpdateObjectState();
            handMenuCanvas.SetActive(false);
        }
        else
        {
            ModelManager.Instance.LockAll();
            handMenuCanvas.SetActive(true);
        }
    }

    void Update()
    {
        nb_frame += 1;
        if (sw.ElapsedMilliseconds > 250)
        {
            fpsText.text = string.Format("{0} FPS", (nb_frame * 1000) / sw.ElapsedMilliseconds);
            nb_frame = 0;
            sw.Restart();
        }
        resolutionText.text =
            gscam.InternalTexSize == Vector2.zero
                ? ""
                : string.Format("{0}x{1} px", gscam.InternalTexSize.x, gscam.InternalTexSize.y);
        resolutionPercent.text = string.Format("{0}%", Mathf.RoundToInt(gscam.texFactor * 100));

        var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        nfi.NumberGroupSeparator = " ";
        splatCount.text = gsInfo.nb_splats.ToString("#,0", nfi);
    }

    public void SliderValueChanged(float value)
    {
        gscam.texFactor = value / 10;
    }
}
