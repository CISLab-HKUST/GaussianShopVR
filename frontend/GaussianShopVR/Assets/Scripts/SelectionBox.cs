using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SelectionBox : MonoBehaviour
{
    public GaussianSplattingModel gsModel;
    public Material lineMaterial;
    public Color lineColor;
    public Color lineActivatedColor;

    [Range(0.001f, 1f)]
    public float lineWidth = 0.1f;

    BoxCollider box = null;

    [SerializeField]
    LineRenderer[] lines = null;
    public Color currentColor;

    void Start()
    {
        box = GetComponent<BoxCollider>();
        currentColor = lineColor;
        lines = new LineRenderer[6];
        for (int i = 0; i < 6; ++i)
        {
            GameObject child = new();
            child.transform.parent = transform;
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = new Vector3(1, 1, 1);
            lines[i] = child.AddComponent<LineRenderer>();
            lines[i].positionCount = 4;
            lines[i].loop = true;
            lines[i].useWorldSpace = false;
            lines[i].gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (gsModel == null)
        {
            // Find all SelectionBox children and merge their boxes
            SelectionBox[] childBoxes = GetComponentsInChildren<SelectionBox>(false);
            childBoxes = System.Array.FindAll(
                childBoxes,
                box => box != this && box.transform.parent == this.transform
            );
            if (childBoxes.Length > 0)
            {
                // Initialize merged bounds
                Bounds mergedBounds = new Bounds();
                bool firstBound = true;

                // Encapsulate all children by their corner points
                for (int i = 0; i < childBoxes.Length; i++)
                {
                    Transform childTransform = childBoxes[i].transform;
                    Vector3 childCenter = childBoxes[i].box.center;
                    Vector3 childSize = childBoxes[i].box.size;

                    // Get all 8 corners of the child box in world space
                    Vector3[] corners = new Vector3[8];
                    corners[0] = childTransform.TransformPoint(
                        childCenter + new Vector3(-childSize.x, -childSize.y, -childSize.z) * 0.5f
                    );
                    corners[1] = childTransform.TransformPoint(
                        childCenter + new Vector3(childSize.x, -childSize.y, -childSize.z) * 0.5f
                    );
                    corners[2] = childTransform.TransformPoint(
                        childCenter + new Vector3(-childSize.x, childSize.y, -childSize.z) * 0.5f
                    );
                    corners[3] = childTransform.TransformPoint(
                        childCenter + new Vector3(childSize.x, childSize.y, -childSize.z) * 0.5f
                    );
                    corners[4] = childTransform.TransformPoint(
                        childCenter + new Vector3(-childSize.x, -childSize.y, childSize.z) * 0.5f
                    );
                    corners[5] = childTransform.TransformPoint(
                        childCenter + new Vector3(childSize.x, -childSize.y, childSize.z) * 0.5f
                    );
                    corners[6] = childTransform.TransformPoint(
                        childCenter + new Vector3(-childSize.x, childSize.y, childSize.z) * 0.5f
                    );
                    corners[7] = childTransform.TransformPoint(
                        childCenter + new Vector3(childSize.x, childSize.y, childSize.z) * 0.5f
                    );

                    // Transform corners to parent's local space and encapsulate
                    for (int j = 0; j < 8; j++)
                    {
                        Vector3 localCorner = transform.InverseTransformPoint(corners[j]);
                        if (firstBound)
                        {
                            mergedBounds = new Bounds(localCorner, Vector3.zero);
                            firstBound = false;
                        }
                        else
                        {
                            mergedBounds.Encapsulate(localCorner);
                        }
                    }
                }
                box.center = mergedBounds.center;
                box.size = mergedBounds.size * 1.25f;
            }
        }
        else
        {
            Vector3 center = gsModel.cropBox.center;
            center.y = -center.y;
            box.center = center;
            box.size = gsModel.cropBox.size;
        }
        UpdateLinesBox();
    }

    void UpdateLinesBox()
    {
        Vector3 center = box.center;
        Vector3 size = box.size;

        Vector3 min = center - size / 2;
        Vector3 max = center + size / 2;

        for (int i = 0; i < 6; ++i)
        {
            lines[i].startWidth = lineWidth * transform.lossyScale.x;
            lines[i].endWidth = lineWidth * transform.lossyScale.x;
            lines[i].material = lineMaterial;
            lines[i].material.color = currentColor;
            lines[i].startColor = currentColor;
            lines[i].endColor = currentColor;
        }

        lines[0].SetPositions(
            new Vector3[]
            {
                new(min.x, min.y, min.z),
                new(max.x, min.y, min.z),
                new(max.x, max.y, min.z),
                new(min.x, max.y, min.z)
            }
        );
        lines[1].SetPositions(
            new Vector3[]
            {
                new(min.x, min.y, max.z),
                new(max.x, min.y, max.z),
                new(max.x, max.y, max.z),
                new(min.x, max.y, max.z)
            }
        );
        lines[2].SetPositions(
            new Vector3[]
            {
                new(min.x, min.y, min.z),
                new(max.x, min.y, min.z),
                new(max.x, min.y, max.z),
                new(min.x, min.y, max.z)
            }
        );
        lines[3].SetPositions(
            new Vector3[]
            {
                new(min.x, max.y, min.z),
                new(max.x, max.y, min.z),
                new(max.x, max.y, max.z),
                new(min.x, max.y, max.z)
            }
        );
        lines[4].SetPositions(
            new Vector3[]
            {
                new(min.x, min.y, min.z),
                new(min.x, max.y, min.z),
                new(min.x, max.y, max.z),
                new(min.x, min.y, max.z)
            }
        );
        lines[5].SetPositions(
            new Vector3[]
            {
                new(max.x, min.y, min.z),
                new(max.x, max.y, min.z),
                new(max.x, max.y, max.z),
                new(max.x, min.y, max.z)
            }
        );
    }

    public void ActivateLines(bool value)
    {
        ShowLines(value && GetComponentInParent<ObjInfo>().showSelectionBox);
    }

    public void ShowLines(bool value)
    {
        if (lines != null && lines.Length == 6)
        {
            for (int i = 0; i < 6; ++i)
            {
                lines[i].gameObject.SetActive(value);
            }
        }
    }

    public void DeleteAllLines()
    {
        if (lines != null)
        {
            for (int i = 0; i < lines.Length; ++i)
            {
                if (lines[i] != null)
                {
                    Destroy(lines[i].gameObject);
                }
            }
            lines = null;
            Debug.Log("All lines have been deleted.");
        }
        else
        {
            Debug.LogWarning("No lines to delete.");
        }
    }
}
