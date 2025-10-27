using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.XR.Interaction.Toolkit;

public class ObjInfo : MonoBehaviour
{
    public ModelData remoteInfo;
    public string localFilePath;
    public bool isActivated = false;
    public bool showSelectionBox = true;
    XRGrabInteractable grab;

    void Awake()
    {
        isActivated = false;
        grab = GetComponent<XRGrabInteractable>();
        particleSystem = GetComponent<ParticleSystem>();
        particleSystem.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = particleSystem.emission;
        emission.enabled = false;
    }

    void OnEnable()
    {
        grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        grab.selectExited.RemoveListener(OnSelectExited);
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        ServerSyncer.Instance.SendTransformData(remoteInfo.id, transform);
        // var interactor = args.interactorObject as XRBaseInteractor;
        // if (interactor is XRRayInteractor)
        //     Debug.Log("Released by RAY");
        // else if (interactor is XRDirectInteractor)
        //     Debug.Log("Released by HAND");
        // else if (interactor is XRSocketInteractor)
        //     Debug.Log("Hand-off to SOCKET");
    }

    void Start() { }

    void Update()
    {
        if (isActivated)
        {
            SelectionBox selectionBox = GetComponent<SelectionBox>();
            selectionBox.currentColor = selectionBox.lineActivatedColor;
            selectionBox.ShowLines(true);
        }
        else
        {
            SelectionBox selectionBox = GetComponent<SelectionBox>();
            selectionBox.currentColor = selectionBox.lineColor;
        }
    }

    public ParticleSystem particleSystem;
    public ParticleSystem.Particle[] allParticles;

    public List<Vector3> Points = new List<Vector3>();
    public List<Color> Colors = new List<Color>();
}
