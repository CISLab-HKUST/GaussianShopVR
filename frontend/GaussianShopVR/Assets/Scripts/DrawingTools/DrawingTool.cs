using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DrawingTool : MonoBehaviour
{
    public static DrawingTool Instance { get; private set; }

    [Header("Cursor Settings")]
    public Vector3 drawingCenter;
    public float drawingRadius = 0.1f;
    public float minRadius = 0.05f;
    public float maxRadius = 0.5f;
    public float radiusAdjustSpeed = 0.1f;

    [Header("Input References")]
    public InputActionReference triggerAction;
    public InputActionReference radiusAdjustAction;
    public InputActionReference clearAction;

    public InputActionReference eraseAction;
    public Transform controllerTransform;
    public InputActionReference addCameraAction;

    [Header("Visual References")]
    public GameObject drawingMenu;
    public GameObject cameraListUI;
    public GameObject drawingSphere;
    public GameObject erasingSphere;
    public GameObject colorPalette;

    [Header("Drawing Settings")]
    public int pointsPerSecond = 500;
    public int maxPointsInArea = 3000;
    public float pointSize = 0.004f;

    public Color currentColor = Color.white;
    private float timeSinceFirstSpawn;

    private GaussianSplatting gs;
    private bool isDrawing = false;
    private bool isErasing = false;
    public ObjInfo currentObj;

    public List<GameObject> cameraPreviewList = new();
    public GameObject cameraPreviewPrefab;

    void Awake()
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

        gs = FindObjectOfType<GaussianSplatting>();
    }

    void OnEnable()
    {
        UpdateObjectState();

        triggerAction.action.performed += OnTriggerPerformed;
        triggerAction.action.canceled += OnTriggerCanceled;
        radiusAdjustAction.action.performed += OnRadiusAdjustActionPerformed;
        radiusAdjustAction.action.canceled += OnRadiusAdjustActionCanceled;
        clearAction.action.performed += OnClearActionPerformed;
        // clearAction.action.canceled += OnClearActionCanceled;
        eraseAction.action.performed += OnEraseActionPerformed;
        eraseAction.action.canceled += OnEraseActionCanceled;

        addCameraAction.action.performed += OnAddCameraActionPerformed;

        drawingSphere.SetActive(false);
        erasingSphere.SetActive(false);

        drawingMenu.SetActive(true);
        colorPalette.SetActive(true);
    }

    void OnDisable()
    {
        triggerAction.action.performed -= OnTriggerPerformed;
        triggerAction.action.canceled -= OnTriggerCanceled;
        radiusAdjustAction.action.performed -= OnRadiusAdjustActionPerformed;
        radiusAdjustAction.action.canceled -= OnRadiusAdjustActionCanceled;
        clearAction.action.performed -= OnClearActionPerformed;
        // clearAction.action.canceled -= OnClearActionCanceled;
        eraseAction.action.performed -= OnEraseActionPerformed;
        eraseAction.action.canceled -= OnEraseActionCanceled;

        addCameraAction.action.performed -= OnAddCameraActionPerformed;

        drawingSphere.SetActive(false);
        erasingSphere.SetActive(false);

        drawingMenu.SetActive(false);
        colorPalette.SetActive(false);
    }

    public void UpdateObjectState()
    {
        ModelManager.Instance.LockAll();
        ModelManager.Instance.worldObject.GetComponent<BoxCollider>().enabled = true;
        ModelManager.Instance.worldObject.GetComponent<ObjInfo>().showSelectionBox = true;
    }

    void Update()
    {
        drawingCenter = controllerTransform.position;
        AdjustRadius();
        UpdateDrawingVisual();
        List<GameObject> activeModels = ModelManager.Instance.GetActiveModels();
        currentObj = activeModels.Count > 0 ? activeModels[0]?.GetComponent<ObjInfo>() : null;
        if (isDrawing)
        {
            DrawPoints();
        }
        else
        {
            timeSinceFirstSpawn = 0f;
        }

        if (isErasing)
        {
            ErasePoints(drawingCenter, drawingRadius);
        }
    }

    private void AdjustRadius()
    {
        float adjustment = radiusAdjustAction.action.ReadValue<Vector2>().x;
        if (adjustment != 0)
        {
            drawingRadius += adjustment * radiusAdjustSpeed * Time.deltaTime;
            drawingRadius = Mathf.Clamp(drawingRadius, minRadius, maxRadius);
        }
    }

    private void OnRadiusAdjustActionPerformed(InputAction.CallbackContext context)
    {
        drawingSphere.SetActive(true);
    }

    private void OnRadiusAdjustActionCanceled(InputAction.CallbackContext context)
    {
        drawingSphere.SetActive(false);
    }

    private void UpdateDrawingVisual()
    {
        if (drawingSphere != null)
        {
            drawingSphere.transform.position = drawingCenter;
            drawingSphere.transform.localScale = Vector3.one * (drawingRadius * 2f);
        }
        if (erasingSphere != null)
        {
            erasingSphere.transform.position = drawingCenter;
            erasingSphere.transform.localScale = Vector3.one * (drawingRadius * 2f);
        }
    }

    private void OnTriggerPerformed(InputAction.CallbackContext context)
    {
        if (FindAnyObjectByType<HandMenuSwitcher>().handMenuCanvas.activeSelf)
            return;
        isDrawing = true;
        drawingSphere.SetActive(true);
    }

    private void OnTriggerCanceled(InputAction.CallbackContext context)
    {
        if (FindAnyObjectByType<HandMenuSwitcher>().handMenuCanvas.activeSelf)
            return;
        isDrawing = false;
        drawingSphere.SetActive(false);
    }

    private void OnClearActionPerformed(InputAction.CallbackContext context)
    {
        ClearAllPoints();
    }

    private void OnEraseActionPerformed(InputAction.CallbackContext context)
    {
        isErasing = true;
        erasingSphere.SetActive(true);
    }

    private void OnEraseActionCanceled(InputAction.CallbackContext context)
    {
        isErasing = false;
        erasingSphere.SetActive(false);
    }

    void DrawPoints()
    {
        if (currentObj == null)
            return;
        timeSinceFirstSpawn += Time.deltaTime;
        int pointsToSpawn = Mathf.FloorToInt(pointsPerSecond * timeSinceFirstSpawn);
        timeSinceFirstSpawn -= pointsToSpawn / (float)pointsPerSecond;
        int nearbyPoints = 0;
        foreach (var point in currentObj.Points)
        {
            if (
                Vector3.Distance(
                    currentObj
                        .GetComponent<ParticleSystem>()
                        .transform.InverseTransformPoint(drawingCenter),
                    point
                ) <= drawingRadius
            )
            {
                nearbyPoints++;
                if (nearbyPoints >= maxPointsInArea)
                {
                    return;
                }
            }
        }
        for (int i = 0; i < pointsToSpawn; i++)
        {
            Vector3 position = drawingCenter + drawingRadius * Random.insideUnitSphere;
            Vector3 localPosition = currentObj
                .GetComponent<ParticleSystem>()
                .transform.InverseTransformPoint(position);
            currentObj.Points.Add(localPosition);
            currentObj.Colors.Add(currentColor);
        }
        RenderPoints();
    }

    void ErasePoints(Vector3 eraseCenter, float radius)
    {
        if (currentObj == null)
            return;
        Vector3 localEraseCenter = currentObj
            .GetComponent<ParticleSystem>()
            .transform.InverseTransformPoint(eraseCenter);
        float localRadius =
            radius / currentObj.GetComponent<ParticleSystem>().transform.lossyScale.x;
        List<Vector3> newPoints = new List<Vector3>();
        List<Color> newColors = new List<Color>();

        for (int i = 0; i < currentObj.Points.Count; i++)
        {
            if (Vector3.Distance(currentObj.Points[i], localEraseCenter) >= localRadius)
            {
                newPoints.Add(currentObj.Points[i]);
                newColors.Add(currentObj.Colors[i]);
            }
        }

        currentObj.Points = newPoints;
        currentObj.Colors = newColors;
        RenderPoints();
    }

    public void ClearAllPoints()
    {
        if (currentObj == null)
            return;
        currentObj.Points.Clear();
        currentObj.Colors.Clear();
        RenderPoints();
    }

    public void DisableParticle()
    {
        if (currentObj == null)
            return;
        currentObj.particleSystem.Stop();
        currentObj.particleSystem.Clear();
    }

    public void EnableParticle()
    {
        if (currentObj == null)
            return;
        currentObj.particleSystem.Play();
        RenderPoints();
    }

    public void RenderPoints()
    {
        if (currentObj == null)
            return;
        var emission = currentObj.particleSystem.emission;
        emission.enabled = true;
        currentObj.particleSystem.startSpeed = 0.0f;
        currentObj.particleSystem.startLifetime = 1000.0f;
        int PointCount = currentObj.Points.Count;
        currentObj.particleSystem.maxParticles = PointCount;
        currentObj.particleSystem.Emit(PointCount);
        currentObj.allParticles = new ParticleSystem.Particle[PointCount];
        currentObj.particleSystem.GetParticles(currentObj.allParticles);
        for (int i = 0; i < PointCount; i++)
        {
            currentObj.allParticles[i].position = currentObj.Points[i];
            currentObj.allParticles[i].startColor = currentObj.Colors[i];
            currentObj.allParticles[i].startSize = pointSize;
        }
        currentObj.particleSystem.SetParticles(currentObj.allParticles, PointCount);
    }

    private void OnAddCameraActionPerformed(InputAction.CallbackContext context)
    {
        Transform cameraTransform = Camera.main.transform;
        Transform worldTransform = ModelManager.Instance.worldObject.transform;
        Vector3 relativePosition = worldTransform.InverseTransformPoint(cameraTransform.position);
        Quaternion relativeRotation =
            Quaternion.Inverse(worldTransform.rotation) * cameraTransform.rotation;
        CamData newCam = new CamData
        {
            cam_translation = new float[]
            {
                relativePosition.x,
                relativePosition.y,
                relativePosition.z
            },
            cam_wxyz = new float[]
            {
                relativeRotation.w,
                relativeRotation.x,
                relativeRotation.y,
                relativeRotation.z
            },
        };
        GameObject cameraPreview = Instantiate(cameraPreviewPrefab, cameraListUI.transform);
        cameraPreview.GetComponent<CameraPreviewItem>().Load(newCam);
        cameraPreviewList.Add(cameraPreview);
    }
}
