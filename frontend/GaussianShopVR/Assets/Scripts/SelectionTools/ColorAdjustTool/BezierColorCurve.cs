using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

[RequireComponent(typeof(LineRenderer))]
public class BezierColorCurve : MonoBehaviour
{
    public Transform p0;
    public Transform p1;
    public Transform p2;
    public Transform p3;
    public Gradient gradient;
    private LineRenderer lineRenderer;
    private int resolution = 256;
    private List<float> normalizedValues = new List<float>();
    private float initialStart;
    private float initialEnd;
    private bool firstTime = true;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = resolution;
        normalizedValues = new List<float>(new float[resolution]);
        p0.localPosition = new Vector3(0, 0, 0);
        p1.localPosition = new Vector3(0.1f, 0.1f, 0);
        p2.localPosition = new Vector3(0.9f, 0.9f, 0);
        p3.localPosition = new Vector3(1f, 1f, 0);
        initialStart = 0;
        initialEnd = 1;
        DrawBezierCurve();
    }

    void OnEnable()
    {
        lineRenderer = GetComponent<LineRenderer>();

        lineRenderer.positionCount = resolution;

        normalizedValues = new List<float>(new float[resolution]);

        p0.localPosition = new Vector3(0, 0, 0);
        p1.localPosition = new Vector3(0.1f, 0.1f, 0);
        p2.localPosition = new Vector3(0.9f, 0.9f, 0);
        p3.localPosition = new Vector3(1f, 1f, 0);
        initialStart = 0;
        initialEnd = 1;
        DrawBezierCurve();
    }

    void ConstrainPosition(Transform targetObject, bool fixX = false, float x = 0)
    {
        Vector3 localPosition = transform.InverseTransformPoint(targetObject.position);
        if (fixX)
        {
            localPosition.x = x;
        }
        else
        {
            localPosition.x = Mathf.Clamp01(localPosition.x);
        }
        localPosition.y = Mathf.Clamp01(localPosition.y);
        localPosition.z = 0;

        Vector3 limitedWorldPosition = transform.TransformPoint(localPosition);

        targetObject.position = limitedWorldPosition;
    }

    void Update()
    {
        ConstrainPosition(p0.transform, fixX: true, x: 0);
        ConstrainPosition(p1.transform);
        ConstrainPosition(p2.transform);
        ConstrainPosition(p3.transform, fixX: true, x: 1);
        DrawBezierCurve();
    }

    void DrawBezierCurve()
    {
        for (int i = 0; i < resolution; i++)
        {
            float t = i / (float)(resolution - 1);
            Vector3 bezierPoint = CalculateBezierPoint(
                t,
                transform.InverseTransformPoint(p0.position),
                transform.InverseTransformPoint(p1.position),
                transform.InverseTransformPoint(p2.position),
                transform.InverseTransformPoint(p3.position)
            );
            normalizedValues[i] = (bezierPoint.y - initialStart) / (initialEnd - initialStart);
            normalizedValues[i] = Mathf.Round(normalizedValues[i] * 255);
            Vector3 worldPosition = transform.parent.TransformPoint(bezierPoint);
            lineRenderer.SetPosition(i, worldPosition);
            float colorPosition = t;
            Color color = gradient.Evaluate(colorPosition);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }
    }

    // Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    // {
    //     float u = 1 - t;
    //     float tt = t * t;
    //     float uu = u * u;
    //     float uuu = uu * u;
    //     float ttt = tt * t;

    //     Vector3 p = uuu * p0;
    //     p += 3 * uu * t * p1;
    //     p += 3 * u * tt * p2;
    //     p += ttt * p3;

    //     return p;
    // }
    Vector3 CalculateBezierPoint(float x, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        // Binary search to find t for given x
        float t = x; // initial guess
        float tolerance = 0.0001f;
        int maxIterations = 10;

        for (int i = 0; i < maxIterations; i++)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            float currentX = uuu * p0.x + 3 * uu * t * p1.x + 3 * u * tt * p2.x + ttt * p3.x;

            if (Mathf.Abs(currentX - x) < tolerance)
                break;

            // Derivative of bezier x with respect to t
            float derivative =
                -3 * uu * p0.x
                + 3 * uu * p1.x
                - 6 * u * t * p1.x
                + 6 * u * t * p2.x
                - 3 * tt * p2.x
                + 3 * tt * p3.x;

            if (Mathf.Abs(derivative) > 0.0001f)
                t -= (currentX - x) / derivative;
        }

        t = Mathf.Clamp01(t);

        float finalU = 1 - t;
        float finalTt = t * t;
        float finalUu = finalU * finalU;
        float finalUuu = finalUu * finalU;
        float finalTtt = finalTt * t;

        Vector3 p = finalUuu * p0;
        p += 3 * finalUu * t * p1;
        p += 3 * finalU * finalTt * p2;
        p += finalTtt * p3;

        return p;
    }

    public List<float> GetValue()
    {
        // Debug.Log("initialStart" + initialStart.ToString());
        // Debug.Log("initialEnd" + initialEnd.ToString());
        return normalizedValues;
    }
}
