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

public class PresetManager : MonoBehaviour
{
    public static PresetManager Instance { get; private set; }
    public MenuListItem UIListItemPrefab;
    public RectTransform PresetsUIListElement;
    public List<string> presetList = new List<string>();

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

    public async Task RefreshPresetList()
    {
        await GetPresetList();
        SetPresetsUI();
    }

    private async Task GetPresetList()
    {
        string payload = await WebUtils.GetRequestAsync(
            ServerSyncer.Instance.serverURL + "/preset_list"
        );
        presetList = JsonConvert.DeserializeObject<List<string>>(payload);
    }

    public void SetPresetsUI()
    {
        if (presetList.Count == 0)
        {
            return;
        }
        int i = 0;
        foreach (Transform transform in PresetsUIListElement)
        {
            Destroy(transform.gameObject);
        }
        foreach (var entry in presetList)
        {
            MenuListItem listItem = Instantiate(UIListItemPrefab, PresetsUIListElement);
            listItem.index = i;
            listItem.text.text = entry;
            listItem.type = "preset";
            listItem.button.onClick.AddListener(async () =>
            {
                await LoadPreset(entry);
            });
            i++;
        }
    }

    public async Task LoadPreset(string presetName)
    {
        string url = ServerSyncer.Instance.serverURL;
        string jsonData = JsonConvert.SerializeObject(presetName);
        Debug.Log(jsonData);

        string response = await WebUtils.PostRequestAsync(url + "/load_preset", jsonData);

        ModelData modelData = JsonConvert.DeserializeObject<ModelData>(response);
        string localPath = await WebUtils.DownloadPlyFile(url, modelData);

        LocalModelData localModel = new LocalModelData();
        localModel.modelData = modelData;
        localModel.local_path = localPath;
        localModel.modelObj = GameObject.Find("GS" + localModel.modelData.id.ToString());
        ModelManager.Instance.AddModel(localModel);
    }
}
