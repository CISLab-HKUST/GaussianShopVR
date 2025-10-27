using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewpointScript : MonoBehaviour
{
    static public ViewpointScript m_Instance;

    public static Transform Head
    {
        get
        {
            return m_Instance.GetHeadTransform();
        }
    }
    Transform GetHeadTransform()
    {
        return transform;
    }
}
