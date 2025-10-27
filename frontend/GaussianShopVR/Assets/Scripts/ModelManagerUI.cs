using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Newtonsoft.Json;
using HuggingFace.API;
using System.Threading.Tasks;

public class ModelManagerUI : MonoBehaviour
{
    public static ModelManagerUI Instance { get; private set; }
    public MenuListItem ModelUIItemPrefab;
    public RectTransform ModelMenu;

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
    }

    public void SetUI()
    {
        foreach (Transform transform in ModelMenu)
        {
            Destroy(transform.gameObject);
        }
        if (ModelManager.Instance.GSModelList.Count == 0)
        {
            return;
        }
        int index = 0;
        CreateModelButton(ModelManager.Instance.worldObject, 0, ref index);
    }

    private void CreateModelButton(GameObject model, int level, ref int index)
    {
        ObjInfo objInfo = model.GetComponent<ObjInfo>();

        if (objInfo == null || objInfo.remoteInfo == null)
        {
            List<GameObject> worldChildren = ModelManager.Instance.GSModelList.FindAll(
                m => m.transform.parent == model.transform
            );

            foreach (var child in worldChildren)
            {
                CreateModelButton(child, level, ref index);
            }
            return;
        }

        // Create button for current model
        MenuListItem listItem = Instantiate(ModelUIItemPrefab, ModelMenu);
        listItem.index = index;

        // Add indentation by adjusting the layout group padding
        HorizontalLayoutGroup layoutGroup = listItem.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup != null)
        {
            // Set left padding based on hierarchy level (30 pixels per level)
            layoutGroup.padding.left = level * 30;
        }

        // Set button width to 1/3 of ModelMenu width
        RectTransform buttonRect = listItem.button.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            float menuWidth = ModelMenu.rect.width;
            float buttonWidth = menuWidth / 3f;
            buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, buttonWidth);
        }

        listItem.text.text = $"GS{objInfo.remoteInfo.id}";
        listItem.type = "model";

        // Set button visual state based on model activation
        UpdateButtonVisualState(listItem.button, objInfo.isActivated);

        // Add button click listener to toggle activation
        listItem.button.onClick.AddListener(() =>
        {
            ToggleModelActivation(model);
        });

        index++;

        // Find and create buttons for children
        List<GameObject> children = ModelManager.Instance.GSModelList.FindAll(
            m => m.GetComponent<ObjInfo>().remoteInfo.parent == objInfo.remoteInfo.id
        );

        foreach (var child in children)
        {
            CreateModelButton(child, level + 1, ref index);
        }
    }

    private void ToggleModelActivation(GameObject model)
    {
        ObjInfo objInfo = model.GetComponent<ObjInfo>();
        objInfo.isActivated = !objInfo.isActivated;

        if (objInfo.isActivated)
        {
            Debug.Log($"Activated model: GS{objInfo.remoteInfo.id}");
        }
        else
        {
            Debug.Log($"Deactivated model: GS{objInfo.remoteInfo.id}");
        }

        // Refresh UI to show updated state
        SetUI();
    }

    private void UpdateButtonVisualState(Button button, bool isActivated)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        if (isActivated)
        {
            // Set button to appear pressed/selected
            button.image.color = colors.pressedColor;
        }
        else
        {
            // Set button to normal state
            button.image.color = colors.normalColor;
        }
    }
}
