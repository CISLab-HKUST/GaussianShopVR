# GaussianShopVR

## Installation

Make sure `libglm-dev` installed on your system for building `diff-gaussian-rasterization`.

```bash
sudo apt-get install libglm-dev
```

The pacakge can be installed by:

```bash
pip install -e .
```

## Get Started

### Run backend

```bash
fastapi dev gaussianshopvr/backend/server.py --port=8888
```

The API endpoints are on `localhost:8888`, and docs can be found on `http://localhost:8888/docs`.

### Run frontend

## Coordinate Conventions

As our project covers frontend and backend, the coordinates should be converted. The three character string are for the directions of xyz-axes, such as "RFU" for x-axis right, y-axis forward, z-axis up.

Followings are conventions used in our project:

| Coodrinate | Convetion |
| ---------- | --------- |
| Unity      | RUF       |
| World      | RFU       |
| Camera     | RDF       |

## Convention

Scale -> Rotation -> Translation


## 从当前场景随机角度渲染256张图片

```
python dataset_prepare.py dataset/02/models --num_renders=256
```

```bash
export DISPLAY=:0.0 && ~/CG/blender-4.0.2-linux-x64/blender --background --python /home/yulin/code/gaussianshopvr/utils/dataset/blender_script.py -- --object_path '/home/yulin/code/gaussianshopvr/dataset/08/models/max.usdz' --num_renders 256 --output_dir /tmp/tmpraupc8jq --engine BLENDER_EEVEE --only_northern_hemisphere
```

## 使用colmap提取相机位姿

```
python GaussianEditor/gaussian_splatting/convert.py -s base --resize
```

## 训练GS

```
python gaussian_splatting/train.py -s dataset/01/renders/max/ -m dataset/01/GS/max --random_background
```

## 使用编辑器

`webui.py`主要关于网页前端界面和交互
`gaussian_shop.py`主要关于GS的数据结构和训练相关

``` bash
python webui.py --gs_source dataset/03/GS/max/point_cloud/iteration_30000/point_cloud.ply --cam_dir dataset/03/renders/max
```

如果使用COLMAP估计的相机参数，则`--cam_dir`应设置为COLMAL的稀疏估计的父文件夹。如果是用Blender生成图片的相机参数，则`--cam_dir`应设置为包含`transforms-{train,test}.json`的文件夹。

## 启动服务器

`fastapi dev server.py --host=0.0.0.0 --port=8888`


COLMAP convert 会改变图像尺寸

最后渲染的图像的格式应该为(H, W, C)

## Gaussian相关

点的缩放需要用log来处理的
[How to apply transformation and scaling to a 3D Gaussian-trained point cloud?](https://github.com/graphdeco-inria/gaussian-splatting/issues/492)
[Gaussian Transform]( https://github.com/yzslab/gaussian-splatting-lightning/blob/73acf406f6346f2f4241f1deb2ad6c5031258b7e/gaussian_transform.py#L171-L173)

Gaussian的camera存的是W2C的R和T，这个R和T不是相机自身的旋转和位移。

For rendering in Python, we merged two versions of renders from FSGS and GaussianEditor.

## Acknowledgement

Our code is based on these research projects:

- [GaussianEditor](https://github.com/buaacyw/GaussianEditor)
- [gaussian-splatting](https://github.com/graphdeco-inria/gaussian-splatting)
- [objaverse-rendering](https://github.com/allenai/objaverse-rendering)


cmake -G "Visual Studio 17" . -B build
cmake --build build --config Release -j 4