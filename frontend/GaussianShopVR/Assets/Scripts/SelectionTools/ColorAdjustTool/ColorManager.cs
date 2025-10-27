using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorManager : MonoBehaviour
{
    public BezierColorCurve BezierColor_R;
    public BezierColorCurve BezierColor_G;
    public BezierColorCurve BezierColor_B;

    public RGBData GetRGBValue()
    {
        RGBData rgbData = new();
        rgbData.R = BezierColor_R.GetValue();
        rgbData.G = BezierColor_G.GetValue();
        rgbData.B = BezierColor_B.GetValue();
        return rgbData;
    }
}
