using UnityEngine;

public class LoadModelTool : MonoBehaviour
{
    public static LoadModelTool Instance { get; private set; }
    public GameObject presetMenu;

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

    void OnEnable()
    {
        PresetManager.Instance.RefreshPresetList();
        presetMenu.SetActive(true);
    }

    void OnDisable()
    {
        presetMenu.SetActive(false);
    }
}
