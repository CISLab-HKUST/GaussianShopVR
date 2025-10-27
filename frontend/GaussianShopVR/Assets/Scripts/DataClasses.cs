using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RemovePointData
{
    public int id;
    public List<int> point_index;
}

public class RemoveObjRequestData
{
    public int id;
}

public class RemoveResponseData
{
    public int group_id;
    public string ply_file_path;
}

public class SplitModelData
{
    public int id;
    public List<int> point_index;
}

public class ColorAdjustData
{
    public int id;
    public List<int> point_index;
    public List<float> R;
    public List<float> G;
    public List<float> B;
}

public class AddRequestData
{
    public int id;
    public string text_prompt;
    public List<float> point_prompt;
}

public class AddResponseData
{
    public int group_id;
    public string ply_file_path;
}

[System.Serializable]
public class TransformData
{
    public int id;
    public float[] translation;
    public float[] wxyz;
    public float[] scale;
}

public class SavePresetData
{
    public int id;
    public string preset_name;
}

public class ModelSendData
{
    public string model_name;
}

[System.Serializable]
public class ModelData
{
    public int id;
    public int parent;
    public float[] translation;
    public float[] wxyz;
    public float[] scale;
    public bool is_leaf;
    public string url_path;
}

public class LocalModelData
{
    public ModelData modelData;
    public GameObject modelObj;
    public string local_path;
}

[System.Serializable]
public class ModelListData
{
    public ModelData[] GSModels;
}

public class GenerationData
{
    public int id;
    public string text_prompt;
    public List<float> point_prompt;
}

public class InpaintData
{
    public int id;
    public string text_prompt;
    public List<float> point_prompt;
    public int context_id;
    public List<CamData> cams;
}

public class OptimizeData
{
    public int edit_type;
    public int id;
    public string text_prompt;
    public List<int> point_prompt;
    public int context_id;
}

public class CamData
{
    public float[] cam_translation;
    public float[] cam_wxyz;
}

[System.Serializable]
public class ImageListData
{
    public ImageData[] images;
}

[System.Serializable]
public class ImageData
{
    public int id;
    public int cam_id;
    public string url_path;
}

[System.Serializable]
public class MergeModelsRequestData
{
    public int id1;
    public int id2;
}

[System.Serializable]
public class RGBData
{
    public List<float> R;
    public List<float> G;
    public List<float> B;
}

[System.Serializable]
public class SaveTransformData
{
    public int modelId;
    public string modelFilePath;
    public float[] position = new float[3];
    public float[] rotation = new float[4];
    public float[] scale = new float[3];
}

[System.Serializable]
public class SceneData
{
    public List<SaveTransformData> models = new List<SaveTransformData>();
}
