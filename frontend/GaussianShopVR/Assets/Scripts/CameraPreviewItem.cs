using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class CameraPreviewItem : MonoBehaviour
{
    public CamData camData;

    void Start() { }

    // Update is called once per frame
    void Update() { }

    public async void Load(CamData camData)
    {
        this.camData = camData;
        string jsonData = JsonUtility.ToJson(camData);

        string url = ServerSyncer.Instance.serverURL + "/get_unity_preview";

        string url_path = await WebUtils.PostRequestAsync(url, jsonData);
        url_path = url_path.Trim('"');

        string download_url = ServerSyncer.Instance.serverURL + url_path;
        Debug.Log(download_url);

        UnityWebRequest getRequest = UnityWebRequestTexture.GetTexture(download_url);

        var operation = getRequest.SendWebRequest();
        while (!operation.isDone)
        {
            await System.Threading.Tasks.Task.Yield();
        }

        if (getRequest.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = ((DownloadHandlerTexture)getRequest.downloadHandler).texture;

            Sprite newSprite = SpriteFromTexture2D(texture);

            // GameObject ImageObject = Instantiate(ImagePrefab, ImageListContainer.transform);
            // ImageObject.name = "Image" + imageData.id.ToString();

            Button buttonComponent = GetComponent<Button>();
            buttonComponent.image.sprite = newSprite;
        }
    }

    private Sprite SpriteFromTexture2D(Texture2D texture)
    {
        return Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );
    }

    public void OnClick()
    {
        Destroy(gameObject);
    }
}
