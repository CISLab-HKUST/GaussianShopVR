using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

public static class WebUtils
{
    public static async Task<string> GetRequestAsync(string url)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            Debug.Log("Web GET: " + url);

            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            // Await the request
            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (
                request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError
            )
            {
                Debug.LogError("Error: " + request.error);
                return null;
            }
            else
            {
                string response = request.downloadHandler.text;
                Debug.Log("Response: " + response);
                return response;
            }
        }
    }

    public static async Task<string> PostRequestAsync(string url, string jsonBody = "")
    {
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("accept", "application/json");

            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (
                request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError
            )
            {
                Debug.LogError("Error: " + request.error);
                return null;
            }
            else
            {
                string response = request.downloadHandler.text;
                Debug.Log("Response: " + response);
                return response;
            }
        }
    }

    public static async Task<string> DownloadPlyFile(string url, ModelData modelData)
    {
        string downloadUrl = url + modelData.url_path;
        Debug.Log($"Downloading from {downloadUrl}");

        using (UnityWebRequest getRequest = UnityWebRequest.Get(downloadUrl))
        {
            var operation = getRequest.SendWebRequest();

            // Await until the request is done
            while (!operation.isDone)
                await Task.Yield(); // let Unity continue frames

            if (getRequest.result == UnityWebRequest.Result.Success)
            {
                byte[] fileData = getRequest.downloadHandler.data;

                string localPath = Path.Combine(
                    Application.dataPath,
                    "ServerDatas",
                    modelData.url_path.Replace("/", "_")
                );

                File.WriteAllBytes(localPath, fileData);
                Debug.Log("File downloaded and saved to: " + localPath);

                // return local path to caller
                return localPath;
            }
            else
            {
                Debug.LogError("Error downloading file: " + getRequest.error);
                return null;
            }
        }
    }
}
