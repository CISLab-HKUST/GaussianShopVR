# GaussianShopVR: Facilitating Immersive 3D Authoring Using Gaussian Splatting in VR

<h4 align="center">

[![Project Page](https://img.shields.io/badge/Project%20Page-385723)](https://cislab.hkust-gz.edu.cn/projects/gaussianshopvr/)
[![Poster](https://img.shields.io/badge/Poster-ED7D31)](https://cislab.hkust-gz.edu.cn/projects/gaussianshopvr/static/poster.pdf)

<p>
    <img width="90%" alt="workflow", src="./assets/workflow.png">
</p>
</h4>

## Python Backend

To install the environment, just run:

```bash
uv sync
```

To run the server:

```bash
source .venv/bin/activate 
fastapi dev gaussianshopvr/backend/server.py --host=0.0.0.0 --port=8888
```

You can access API document on [http://localhost:8888/docs](http://localhost:8888/docs), and there is also a web visuilization on [http://localhost:8088/](http://localhost:8088/). All operations in Unity will be synced to the server. We remove AI models in this repo but keep the interfaces.

## Unity Frontend

There are three modes in GaussianShopVR: Navigation, Selection and Drawing. Use `Menu` button on the left controller to toggle menu and select mode. In each mode, you can use the `JoyStick` on the left controller to change tool. You can not operate on objects when the menu is opened.

### Navigation

In Navigation Mode, you can move, load, remove, save and merge objects. First, activate objects by clicking items in the model menu, and the activated objects will be highlighted.

For moving, toggle the menu, and directly interacti with Gaussian objects. Pressing right `JoyStick` can change the level of object to interac with (to avoid overlaping due to the hierachy).

For loading presets, change to the loading tool and a panel will show. Click the target item and the system will load it from the server.

For saving a model as preset, first activate an object.
Then, press `X` button to confirm.

For merging, activate two objects and press `X` button to confirm.

### Selection

In Selection Mode, you can use three tools: remove, split and color adjusting. You should activate an object, and then hold right `Trigger` to select points. Hold `A` to erase and press `B` to clear selection. You can use right `JoyStick` to adjust the size of the curosr.

After selection, press `X` button to confirm operations.

For color adjusting, the RGB curves are above the menu. And you can use `Y` to preview the result.

### Drawing

In Drawing Mode, there are two tools: inpainting and generation. You should activate an object, and then hold right `Trigger` to draw points. Hold `A` to erase and press `B` to clear drawing. You can use right `JoyStick` to adjust the size of the curosr.

For inpainting, you also should upload some camera pos using `Y`, and press `X` for confirmation.

For generation, you can see a panel besides the menu. Record your audio and press `X` for confirmation to send data to the backend server.

## Citation
If you find our work helpful, please consider citing:
```bibtex
@inproceedings{shen2025GaussianShopVR,
    author = {Shen, Yulin and Li, Boyu and Huang, Jiayang and Yip, David and Wang, Zeyu},
    title = {GaussianShopVR: Facilitating Immersive 3D Authoring Using Gaussian Splatting in VR},
    year = {2025},
    isbn = {9798400720376},
    publisher = {Association for Computing Machinery},
    address = {New York, NY, USA},
    url = {https://doi.org/10.1145/3746059.3747803},
    doi = {10.1145/3746059.3747803},
    booktitle = {Proceedings of the 38th Annual ACM Symposium on User Interface Software and Technology},
}
```