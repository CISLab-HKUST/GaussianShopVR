using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class ModelManager : MonoBehaviour
{
    public static ModelManager Instance { get; private set; }

    private GaussianSplatting gaussianSplatting;
    public GameObject GSModelPrefab;
    public GameObject ContainerModelPrefab;
    public GameObject worldObject;
    public List<GameObject> GSModelList;

    private void Awake()
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
        gaussianSplatting = FindObjectOfType<GaussianSplatting>();
    }

    public List<GameObject> GetActiveModels()
    {
        List<GameObject> activeModels = new List<GameObject>();
        foreach (GameObject GSModel in GSModelList)
        {
            ObjInfo objInfo = GSModel.GetComponent<ObjInfo>();
            if (objInfo != null && objInfo.isActivated)
            {
                activeModels.Add(GSModel);
            }
        }
        // if (activeModels.Count == 0)
        // {
        //     Debug.Log("No Active Model");
        // }
        return activeModels;
    }

    public void RemoveModel(GameObject GSModel)
    {
        GameObject modelToRemove = GSModelList.Find(m => m == GSModel);
        if (modelToRemove != null)
        {
            GSModelList.Remove(modelToRemove);
            Destroy(GSModel);
        }
        ModelManagerUI.Instance.SetUI();
    }

    public GameObject AddModel(LocalModelData localModel)
    {
        GameObject parentObject = GSModelList.Find(
            m => m.GetComponent<ObjInfo>().remoteInfo.id == localModel.modelData.parent
        );
        if (parentObject == null)
        {
            parentObject = worldObject;
        }
        GameObject existingModel = GSModelList.Find(
            m => m.name == "GS" + localModel.modelData.id.ToString()
        );
        if (existingModel != null)
        {
            GSModelList.Remove(existingModel);
            Destroy(existingModel);
        }

        Vector3 position = new Vector3(
            localModel.modelData.translation[0],
            localModel.modelData.translation[1],
            localModel.modelData.translation[2]
        );

        Quaternion rotation = new Quaternion(
            localModel.modelData.wxyz[1],
            localModel.modelData.wxyz[2],
            localModel.modelData.wxyz[3],
            localModel.modelData.wxyz[0]
        );
        Vector3 scale = new Vector3(
            localModel.modelData.scale[0],
            localModel.modelData.scale[1],
            localModel.modelData.scale[2]
        );
        Vector3 worldPosition = parentObject.transform.TransformPoint(position);

        GameObject newObj;
        if (localModel.modelData.is_leaf)
        {
            GameObject newGaussianModel = Instantiate(
                GSModelPrefab,
                worldPosition,
                parentObject.transform.rotation * rotation,
                parentObject.transform
            );
            newGaussianModel.transform.localScale = scale;
            newGaussianModel.transform.parent = parentObject.transform;

            GaussianSplattingModel modelComponent =
                newGaussianModel.GetComponent<GaussianSplattingModel>();
            newGaussianModel.GetComponent<ObjInfo>().remoteInfo = localModel.modelData;
            modelComponent.modelFilePath = localModel.local_path;
            newGaussianModel.GetComponent<BoxCollider>().enabled = true;
            newGaussianModel.name = "GS" + localModel.modelData.id.ToString();
            newGaussianModel.SetActive(true);
            GSModelList.Add(newGaussianModel);
            Debug.Log("Created Gaussian Model:" + newGaussianModel.name);
            ObjInfo objInfo = newGaussianModel.GetComponent<ObjInfo>();
            objInfo.remoteInfo = localModel.modelData;
            objInfo.localFilePath = localModel.local_path;
            newObj = newGaussianModel;
        }
        else
        {
            GameObject newContainerModel = Instantiate(
                ContainerModelPrefab,
                worldPosition,
                parentObject.transform.rotation * rotation,
                parentObject.transform
            );
            newContainerModel.transform.parent = parentObject.transform;

            newContainerModel.GetComponent<ObjInfo>().remoteInfo = localModel.modelData;
            newContainerModel.transform.localScale = scale;
            newContainerModel.GetComponent<BoxCollider>().enabled = true;
            newContainerModel.name = "GS" + localModel.modelData.id.ToString();
            newContainerModel.SetActive(true);
            GSModelList.Add(newContainerModel);
            Debug.Log("Created Container Model:" + newContainerModel.name);
            ObjInfo objInfo = newContainerModel.GetComponent<ObjInfo>();
            objInfo.remoteInfo = localModel.modelData;
            objInfo.localFilePath = localModel.local_path;
            newObj = newContainerModel;
        }
        ModelManagerUI.Instance.SetUI();
        if (GameObject.Find("HandMenuCanvas").activeSelf)
        {
            newObj.GetComponent<BoxCollider>().enabled = false;
            newObj.GetComponent<ObjInfo>().showSelectionBox = false;
        }
        return newObj;
    }

    public List<GameObject> GetModelByLevel(int level)
    {
        if (level == 0)
            return GSModelList;
        List<GameObject> models = new List<GameObject>();
        List<GameObject> queue = new List<GameObject> { worldObject };

        level -= 1;
        while (level != 0)
        {
            List<GameObject> newQueue = new List<GameObject>();
            foreach (var entry in queue)
            {
                newQueue.AddRange(GSModelList.FindAll(m => m.transform.parent == entry.transform));
            }
            queue = newQueue;
            level--;
        }
        return queue;
    }

    public void LockAll()
    {
        foreach (GameObject model in GSModelList)
        {
            model.GetComponent<BoxCollider>().enabled = false;
            model.GetComponent<ObjInfo>().showSelectionBox = false;
        }
    }

    public void UnlockAll()
    {
        foreach (GameObject model in GSModelList)
        {
            model.GetComponent<BoxCollider>().enabled = true;
            model.GetComponent<ObjInfo>().showSelectionBox = true;
        }
    }
}
