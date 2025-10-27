using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SelectionTool : MonoBehaviour
{
    [Header("Selection Settings")]
    public Vector3 selectionCenter;
    public float selectionRadius = 0.1f;
    public float minRadius = 0.05f;
    public float maxRadius = 0.5f;
    public float radiusAdjustSpeed = 0.1f;

    [Header("Input References")]
    public InputActionReference triggerAction;
    public InputActionReference radiusAdjustAction;

    public InputActionReference clearAction;

    public InputActionReference eraseAction;
    public Transform controllerTransform;

    [Header("Visual References")]
    public GameObject selectionSphere;
    public GameObject erasingSphere;
    private GaussianSplatting gs;
    private bool isSelecting = false;

    void Awake()
    {
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

        selectionSphere.SetActive(false);
        erasingSphere.SetActive(false);
    }

    public void UpdateObjectState()
    {
        ModelManager.Instance.LockAll();
        ModelManager.Instance.worldObject.GetComponent<BoxCollider>().enabled = true;
        ModelManager.Instance.worldObject.GetComponent<ObjInfo>().showSelectionBox = true;
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

        selectionSphere.SetActive(false);
    }

    void Update()
    {
        selectionCenter = controllerTransform.position;
        AdjustRadius();
        UpdateSelectionVisual();
        if (isSelecting)
        {
            SelectPoints();
        }
    }

    private void AdjustRadius()
    {
        float adjustment = radiusAdjustAction.action.ReadValue<Vector2>().x;
        if (adjustment != 0)
        {
            selectionRadius += adjustment * radiusAdjustSpeed * Time.deltaTime;
            selectionRadius = Mathf.Clamp(selectionRadius, minRadius, maxRadius);
        }
    }

    private void OnRadiusAdjustActionPerformed(InputAction.CallbackContext context)
    {
        selectionSphere.SetActive(true);
    }

    private void OnRadiusAdjustActionCanceled(InputAction.CallbackContext context)
    {
        selectionSphere.SetActive(false);
    }

    private void UpdateSelectionVisual()
    {
        if (selectionSphere != null)
        {
            selectionSphere.transform.position = selectionCenter;
            selectionSphere.transform.localScale = Vector3.one * (selectionRadius * 2f);
        }
        if (erasingSphere != null)
        {
            erasingSphere.transform.position = selectionCenter;
            erasingSphere.transform.localScale = Vector3.one * (selectionRadius * 2f);
        }
    }

    private void OnTriggerPerformed(InputAction.CallbackContext context)
    {
        if (FindAnyObjectByType<HandMenuSwitcher>().handMenuCanvas.activeSelf)
            return;
        isSelecting = true;
        selectionSphere.SetActive(true);
    }

    private void OnTriggerCanceled(InputAction.CallbackContext context)
    {
        if (FindAnyObjectByType<HandMenuSwitcher>().handMenuCanvas.activeSelf)
            return;
        isSelecting = false;
        selectionSphere.SetActive(false);
        gs.StopSelection();
    }

    private void OnClearActionPerformed(InputAction.CallbackContext context)
    {
        gs.ClearSelection();
    }

    private void OnEraseActionPerformed(InputAction.CallbackContext context)
    {
        isSelecting = true;
        erasingSphere.SetActive(true);
        gs.SetEraseSelection(true);
    }

    private void OnEraseActionCanceled(InputAction.CallbackContext context)
    {
        isSelecting = false;
        erasingSphere.SetActive(false);
        gs.StopSelection();
        gs.SetEraseSelection(false);
    }

    private void SelectPoints()
    {
        gs.SelectPointsInSphere(selectionCenter, selectionRadius);
    }
}
