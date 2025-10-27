using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class GaussianSplattingModel : MonoBehaviour
{
    public string modelFilePath = "";
    public Bounds cropBox;
    public GaussianSplatting gs;

    private void Awake()
    {
        gs = FindObjectOfType<GaussianSplatting>();
    }

    private void Start()
    {
        Debug.Log("Register Model: " + modelFilePath);
        gs.RegisterModel(this);
    }

    private void OnDestroy()
    {
        gs.UnRegisterModel(this);
    }
}
