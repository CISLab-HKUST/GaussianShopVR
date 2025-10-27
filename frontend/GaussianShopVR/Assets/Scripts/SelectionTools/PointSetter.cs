using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class PointSetter : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField]
    private Color selectedColor = Color.yellow;

    [SerializeField]
    private Color unselectedColor = Color.blue;

    [Header("Point Size")]
    [SerializeField]
    private float pointSize = 1f;

    [Header("Depth Cutoff")]
    public InputActionReference depthCutoffAction;
    public InputActionReference depthCutoffEnable;

    [SerializeField]
    private float depthCutoff = 10000f;

    [SerializeField]
    private float depthCutoffSpeed = 0.1f;

    [SerializeField]
    private float accelerationRate = 2.0f;

    [SerializeField]
    private float maxSpeed = 100.0f;

    [SerializeField]
    private float minDepth = 0f;

    [SerializeField]
    private float maxDepth = 10000f;

    [SerializeField]
    private GameObject DepthCutOffVisual;
    private GaussianSplatting gs;

    private float currentSpeed;

    private void Awake()
    {
        gs = FindObjectOfType<GaussianSplatting>();
    }

    private void OnEnable()
    {
        gs.SetDepthCutoff(depthCutoff);
        StartCoroutine(RepeatColorUpdate());
        gs.SetPointSize(pointSize);
        gs.SetShowCenter(true);

        depthCutoffEnable.action.performed += OnDepthCutoffEnablePerformed;
        depthCutoffEnable.action.canceled += OnDepthCutoffEnableCanceled;
        DepthCutOffVisual.transform.localPosition = new Vector3(
            DepthCutOffVisual.transform.localPosition.x,
            DepthCutOffVisual.transform.localPosition.y,
            depthCutoff
        );
    }

    private void OnDisable()
    {
        depthCutoffEnable.action.performed -= OnDepthCutoffEnablePerformed;
        depthCutoffEnable.action.canceled -= OnDepthCutoffEnableCanceled;
        gs.SetShowCenter(false);
    }

    void Update()
    {
        AdjustDepthCutoff();
    }

    private void OnDepthCutoffEnablePerformed(InputAction.CallbackContext context)
    {
        DepthCutOffVisual.SetActive(true);
    }

    private void OnDepthCutoffEnableCanceled(InputAction.CallbackContext context)
    {
        DepthCutOffVisual.SetActive(false);
    }

    void AdjustDepthCutoff()
    {
        float input = depthCutoffAction.action.ReadValue<Vector2>().y;
        bool enable = depthCutoffEnable.action.ReadValue<float>() > 0.5f;
        if (enable)
        {
            if (input != 0)
            {
                currentSpeed = Mathf.Min(
                    currentSpeed + depthCutoffSpeed * accelerationRate * Time.deltaTime,
                    maxSpeed
                );
            }
            else
            {
                currentSpeed = depthCutoffSpeed;
            }

            float delta = input * currentSpeed * Time.deltaTime;
            depthCutoff = Mathf.Clamp(depthCutoff + delta, minDepth, maxDepth);
            DepthCutOffVisual.transform.localPosition = new Vector3(
                DepthCutOffVisual.transform.localPosition.x,
                DepthCutOffVisual.transform.localPosition.y,
                depthCutoff
            );

            gs.SetDepthCutoff(depthCutoff);
        }
    }

    private IEnumerator RepeatColorUpdate()
    {
        while (true)
        {
            UpdateColors();
            gs.SetPointSize(pointSize);
            yield return new WaitForSeconds(1.0f);
        }
    }

    // Called automatically by Unity when values are changed in the Inspector
    private void OnValidate()
    {
        if (Application.isPlaying && gs != null)
        {
            UpdateColors();
            gs.SetPointSize(pointSize);
        }
    }

    public void UpdateColors()
    {
        if (gs == null)
            return;

        float[] selected = new float[]
        {
            selectedColor.r,
            selectedColor.g,
            selectedColor.b,
            selectedColor.a
        };

        float[] unselected = new float[]
        {
            unselectedColor.r,
            unselectedColor.g,
            unselectedColor.b,
            unselectedColor.a
        };

        gs.SetTwoColors(selected, unselected);
    }
}
