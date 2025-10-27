import os
from contextlib import asynccontextmanager
import uuid
import glob

from types import SimpleNamespace
from typing import Annotated
from fastapi import FastAPI, Body
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel
from omegaconf import OmegaConf
import numpy as np
import json
import open3d as o3d
import torch


from gaussianshopvr.utils.system_utils import mkdir_p
from gaussianshopvr.core.gaussian_shop import gaussianshopvr
from gaussianshopvr.utils.server import time_string
from gaussianshopvr.frontend.webui import WebUI
from gaussianshopvr.utils.image_utils import tensor_save_img
from gaussianshopvr.utils.utils import get_mask_from_index
from gaussianshopvr.core.cameras import (
    MiniCam,
)

cfg = OmegaConf.load(os.path.join(os.path.dirname(__file__), "server_config.yaml"))

_base_dir = os.path.dirname(os.path.abspath(__file__))
_save_dir = os.path.join(_base_dir, cfg.save_dir)
_preset_dir = os.path.join(_base_dir, cfg.preset_dir)
_output_dir = os.path.join(_base_dir, cfg.output_dir)


@asynccontextmanager
async def lifespan(app: FastAPI):
    global gsshop, webui
    _new_project()
    gsshop = gaussianshopvr()
    if cfg.default_project:
        gsshop.load(os.path.join(_save_dir, cfg.default_project))
    else:
        gsshop.reset()
    print("Loaded!")
    for gsobj_id in gsshop.gsobjs.keys():
        gsshop.save_ply(_output_dir, gsobj_id)
    webui = WebUI(None, gsshop=gsshop)
    yield


app = FastAPI(lifespan=lifespan)
app.mount(
    "/static/tmp",
    StaticFiles(directory=_output_dir),
    name="Output Gaussians",
)
app.mount(
    "/static/presets",
    StaticFiles(directory=_preset_dir),
    name="Preset Gaussians",
)
app.mount(
    "/static/saves",
    StaticFiles(directory=_save_dir),
    name="Saved Projects",
)


class GaussianObject(BaseModel):
    id: int
    parent: int | None
    scale: list[float]
    wxyz: list[float]
    translation: list[float]
    is_leaf: bool
    url_path: str


class ImageObject(BaseModel):
    id: int
    cam_id: int
    url_path: str


class CameraObject(BaseModel):
    cam_translation: list[float]
    cam_wxyz: list[float]


def _new_project():
    global time_stamp, _output_dir
    time_stamp = time_string()
    _output_dir = os.path.join(_base_dir, cfg.output_dir, time_stamp)
    mkdir_p(_output_dir)
    mkdir_p(os.path.join(_output_dir, "img"))


def _get_ply_file(id):
    ply_files = glob.glob(os.path.join(_output_dir, f"{id}", "*.ply"))
    return sorted(ply_files)[-1]


def _get_gsobj_resp(id):
    """Get the info of the i-th GaussianObject for client loading."""
    gs_file = gsshop.save_ply(_output_dir, id)
    obj_info = gsshop.gsobjs[id].info
    obj_info["url_path"] = gs_file.removeprefix(_base_dir)
    return obj_info


# System
@app.post("/start_webui")
async def start_webui():
    global webui
    await webui.start()
    return "Webui Started!"


@app.post("/stop_webui")
async def stop_webui():
    global webui
    webui.stop()
    return "Webui Stopped!"


@app.post(
    "/reload",
    description="Reload the program",
)
async def reload():
    global time_stamp, _output_dir, gsshop
    _new_project()
    if cfg.default_project:
        gsshop.load(os.path.join(_save_dir, cfg.default_project))
    else:
        gsshop.reset()
    for gsobj_id in gsshop.gsobjs.keys():
        gsshop.save_ply(_output_dir, gsobj_id)
    global webui
    webui.gsshop = gsshop
    print("Reloaded!")
    return "Reloaded!"


# Project
@app.get(
    "/project_list",
    description="Get the list of saved projects",
    response_description="Project list",
)
async def get_project_list() -> list:
    saves = sorted(glob.glob("*", root_dir=_save_dir))
    return saves


@app.get("/project_info", description="Get the info of current project")
async def get_project_info():
    # return gsshop.
    gsobjs_info = []
    for gsobj_id in gsshop.gsobjs.keys():
        gsobjs_info.append(_get_gsobj_resp(gsobj_id))

    return gsobjs_info


@app.post("/save_project", description="Save the project")
async def save_project():
    gsshop.save(os.path.join(_save_dir, time_string()))
    return "Project saved!"


@app.post(
    "/load_project",
    description="Load a saved project",
    response_model=list[GaussianObject],
)
async def load_project(
    project_name: Annotated[str, Body(description="Project name in the project list")],
):
    _new_project()
    gsshop.load(os.path.join(_save_dir, project_name))

    gsobjs_info = []
    for gsobj_id in gsshop.gsobjs.keys():
        gsobjs_info.append(_get_gsobj_resp(gsobj_id))

    return gsobjs_info


# Object
@app.get("/preset_list", response_description="GS file list")
async def get_preset_list() -> list:
    presets = sorted(glob.glob("**/*.ply", root_dir=_preset_dir, recursive=True))
    return presets


@app.post("/save_preset", description="Save the selected object as a preset")
async def save_preset(
    id: Annotated[int, Body()],
    preset_name: Annotated[str, Body()],
):
    path_name = preset_name if preset_name != "None" else time_string()
    gsshop.gsobjs[id].gaussian.save_ply(
        os.path.join(_preset_dir, f"objects/{path_name}.ply")
    )
    return "Preset saved!"


@app.post(
    "/load_preset",
    description="Load a preset",
    response_model=GaussianObject,
    response_description="A ply file loction",
)
async def load_preset(
    preset_name: Annotated[str, Body(description="Preset name in the preset list")],
):
    new_obj_id = gsshop.add_obj(os.path.join(_preset_dir, preset_name))
    return _get_gsobj_resp(new_obj_id)


@app.get(
    "/object_info",
    response_model=GaussianObject,
    description="Get the latest ply file path of the i-th group",
)
async def get_object_info(id: Annotated[int, Body]):
    obj_info = gsshop.gsobjs[id].info
    obj_info["url_path"] = _get_ply_file(id)
    return obj_info


# Edit
@app.post(
    "/remove_points",
    response_model=GaussianObject,
    response_description="A ply file loction",
)
async def remove_points(
    id: Annotated[int, Body()], point_index: Annotated[list[int], Body()]
):
    gsshop.remove_points(id, point_index)

    return _get_gsobj_resp(id)


@app.post("/remove_obj", description="Remove a GaussianObject")
async def remove_obj(id: int = Body(..., embed=True)):
    gsshop.remove_obj(id)
    return f"GaussianObject {id} removed!"


@app.post(
    "/split",
    response_model=GaussianObject,
    response_description="A list of ply file loctions",
)
async def split(
    id: Annotated[int, Body()],
    point_index: Annotated[list[int], Body()],
):
    """Split an object from the current scene."""
    new_obj_id = gsshop.split(id, point_index)

    obj_info = _get_gsobj_resp(id)
    new_obj_info = _get_gsobj_resp(new_obj_id)

    return new_obj_info


@app.post(
    "/merge_obj",
    response_model=GaussianObject,
    response_description="A ply file loction",
)
async def merge_obj(id1: Annotated[int, Body()], id2: Annotated[int, Body()]):
    gsshop.merge_obj(id1, id2)
    return _get_gsobj_resp(id1)


@app.post("/transform")
async def transform(
    id: Annotated[int, Body()],
    translation: Annotated[list[float], Body()],
    wxyz: Annotated[list[float], Body()],
    scale: Annotated[list[float], Body()],
) -> None:
    """Move a Gaussian Object."""
    translation[1] = -translation[1]
    wxyz[1] = -wxyz[1]
    wxyz[3] = -wxyz[3]
    try:
        gsshop.transform(id, translation, wxyz, scale)

    except Exception as e:
        raise e
    return "Received!"


@app.post("/inpaint", response_model=GaussianObject | None)
async def inpaint(
    id: Annotated[int, Body()],
    text_prompt: Annotated[str, Body()],
    point_prompt: Annotated[list[float], Body()],
    context_id: Annotated[int | None, Body()] = None,
    cams: Annotated[list, Body()] = [],
):
    data = {
        "id": id,
        "text_prompt": text_prompt,
        "point_prompt": point_prompt,
        "context_id": context_id,
        "cams": cams,
    }
    print(data)
    # Your code here

    return None


# AI Edit
# @app.post(
#     "/begin_edit", description="type 0 is adding points, type 1 is edit originals"
# )
# async def begin_edit(
#     edit_type: Annotated[int, Body()],
#     id: Annotated[int, Body()],
#     text_prompt: Annotated[str, Body()],
#     point_prompt: Annotated[list[float], Body()],
#     context_id: Annotated[int | None, Body()] = None,
# ):
#     data = {
#         "edit_type": edit_type,
#         "id": id,
#         "text_prompt": text_prompt,
#         "point_prompt": point_prompt,
#         "context_id": context_id,
#     }
#     with open("request_params.json", "a") as f:
#         f.write(json.dumps(data) + "\n")
#     if gsshop.edit_status != -1:
#         return "Still in editing"
#     else:
#         if not gsshop.gsobjs[id].is_leaf:
#             return "Target object must be leaf"
#         if not gsshop.is_decendant(gsshop.gsobjs[id], gsshop.gsobjs[context_id]):
#             return "Target object is not in the context"
#         if context_id is not None:
#             gsshop.editing_context_id = context_id
#         else:
#             gsshop.editing_context_id = id
#         gsshop.edit_status = edit_type
#         gsshop.text_prompt = text_prompt
#         gsshop.editing_obj_id = id
#         gsshop.cams = []
#         gsshop.edited_imgs = []

#     print(point_prompt)

#     if edit_type == 0:
#         point_prompt = np.array(point_prompt).reshape(-1, 3)
#         points_xyz = point_prompt[0::2]
#         points_xyz[:, 1] = -points_xyz[:, 1]
#         points_rgb = point_prompt[1::2]
#         mask = gsshop.add_points(id, points_xyz, points_rgb)
#         gsshop.gsobjs[id].activate(mask)
#         gsshop.update(gsshop.gsobjs[id])
#     else:
#         point_index = np.array(point_prompt)
#         gsshop.gsobjs[id].activate(
#             get_mask_from_index(gsshop.gsobjs[id].gaussian.points_num, point_index)
#         )


# @app.post(
#     "/upload_cam",
#     response_model=list[ImageObject] | str,
#     response_description="List of image file locations",
# )
# async def upload_cam(
#     cam_translation: Annotated[list[float], Body()],
#     cam_wxyz: Annotated[list[float], Body()],
# ):
#     print(cam_translation, cam_wxyz)
#     data = {"cam_translation": cam_translation, "cam_wxyz": cam_wxyz}
#     with open("cam_params.json", "a") as f:
#         f.write(json.dumps(data) + "\n")

#     if gsshop.edit_status == -1:
#         return "Not in editing!"
#     cam_translation[1] = -cam_translation[1]
#     cam_wxyz[1] = -cam_wxyz[1]
#     cam_wxyz[3] = -cam_wxyz[3]
#     img_ids = gsshop.add_cam(cam_translation, cam_wxyz)
#     imgs = [gsshop.edited_imgs[img_id] for img_id in img_ids]
#     imgs_resp = []
#     for id, img in zip(img_ids, imgs):
#         url_path = os.path.join(_output_dir, "img", str(uuid.uuid4())[:8] + ".png")
#         tensor_save_img(img["img"], url_path)
#         imgs_resp.append(ImageObject(id=id, cam_id=img["cam_id"], url_path=url_path))

#     return imgs_resp


# @app.post(
#     "/end_edit",
#     response_model=GaussianObject,
#     response_description="A ply file loction",
# )
# async def end_edit(img_ids: Annotated[list[int], Body()]):
#     if len(img_ids) != 0:
#         gsshop.optimize(img_ids)
#         gsshop.update(gsshop.gsobjs[gsshop.editing_obj_id])
#     id = gsshop.editing_obj_id
#     gsshop.editing_obj_id = None
#     gsshop.editint_context_id = None
#     gsshop.edit_status = -1
#     gsshop.text_prompt = None
#     gsshop.cams = []
#     gsshop.edited_imgs = []
#     if len(img_ids) == 0:
#         gsshop.gsobj[id].remove_points(gsshop.gsobj[id].gaussian.mask)
#     return _get_gsobj_resp(id)


# @app.post(
#     "/begin_style_transfer",
#     description="type 0 is adding points, type 1 is edit originals",
# )
# async def begin_style_transfer(
#     edit_type: Annotated[int, Body()],
#     id: Annotated[int, Body()],
#     text_prompt: Annotated[str, Body()],
#     point_prompt: Annotated[list[float], Body()],
#     context_id: Annotated[int | None, Body()] = None,
# ):
#     data = {
#         "edit_type": edit_type,
#         "id": id,
#         "text_prompt": text_prompt,
#         "point_prompt": point_prompt,
#         "context_id": context_id,
#     }
#     with open("request_params.json", "a") as f:
#         f.write(json.dumps(data) + "\n")
#     if gsshop.edit_status != -1:
#         return "Still in editing"
#     else:
#         if not gsshop.gsobjs[id].is_leaf:
#             return "Target object must be leaf"
#         if not gsshop.is_decendant(gsshop.gsobjs[id], gsshop.gsobjs[context_id]):
#             return "Target object is not in the context"
#         if context_id is not None:
#             gsshop.editing_context_id = context_id
#         else:
#             gsshop.editing_context_id = id
#         gsshop.edit_status = edit_type
#         gsshop.text_prompt = text_prompt
#         gsshop.editing_obj_id = id
#         gsshop.cams = []
#         gsshop.edited_imgs = []

#     print(point_prompt)

#     if edit_type == 0:
#         point_prompt = np.array(point_prompt).reshape(-1, 3)
#         points_xyz = point_prompt[0::2]
#         points_xyz[:, 1] = -points_xyz[:, 1]
#         points_rgb = point_prompt[1::2]
#         mask = gsshop.add_points(id, points_xyz, points_rgb)
#         gsshop.gsobjs[id].activate(mask)
#         gsshop.update(gsshop.gsobjs[id])
#     else:
#         # point_index = np.array(point_prompt)
#         point_index = np.arange(gsshop.gsobjs[id].gaussian.points_num)
#         gsshop.gsobjs[id].activate(
#             get_mask_from_index(gsshop.gsobjs[id].gaussian.points_num, point_index)
#         )


# @app.post(
#     "/upload_cam_style_transfer",
#     response_model=str,
#     response_description="List of image file locations",
# )
# async def upload_cam_style_transfer(
#     cam_translation: Annotated[list[float], Body()],
#     cam_wxyz: Annotated[list[float], Body()],
# ):
#     print(cam_translation, cam_wxyz)
#     data = {"cam_translation": cam_translation, "cam_wxyz": cam_wxyz}
#     with open("cam_params.json", "a") as f:
#         f.write(json.dumps(data) + "\n")
#     if gsshop.edit_status == -1:
#         return "Not in editing!"

#     with open("cam_params", "r") as f:
#         cams = [json.loads(line) for line in f.readlines()]
#     for cam in cams:
#         cam_translation = cam["cam_translation"]
#         cam_wxyz = cam["cam_wxyz"]
#         cam_translation[1] = -cam_translation[1]
#         cam_wxyz[1] = -cam_wxyz[1]
#         cam_wxyz[3] = -cam_wxyz[3]
#         gsshop.add_cam(cam_translation, cam_wxyz, sd=False)

#     # cam_translation[1] = -cam_translation[1]
#     # cam_wxyz[1] = -cam_wxyz[1]
#     # cam_wxyz[3] = -cam_wxyz[3]
#     # gsshop.add_cam(cam_translation, cam_wxyz, sd=False)

#     return "Uploaded!"


# @app.post(
#     "/end_style_transfer",
#     response_model=GaussianObject,
#     response_description="A ply file loction",
# )
# async def end_style_transfer():
#     gsshop.train_guidance(
#         gsshop.gsobjs[gsshop.editing_context_id],
#         gsshop.gsobjs[gsshop.editing_obj_id],
#         cams=gsshop.cams,
#     )
#     gsshop.update(gsshop.gsobjs[gsshop.editing_obj_id])
#     id = gsshop.editing_obj_id
#     gsshop.editing_obj_id = None
#     gsshop.editint_context_id = None
#     gsshop.edit_status = -1
#     gsshop.text_prompt = None
#     gsshop.cams = []
#     gsshop.edited_imgs = []
#     return _get_gsobj_resp(id)


@app.post(
    "/generate",
    #   response_model=GaussianObject, response_description="A ply file loction"
)
async def generate(
    id: Annotated[int, Body()],
    text_prompt: Annotated[str, Body()],
    point_prompt: Annotated[list[float], Body()],
):
    data = {
        "id": id,
        "text_prompt": text_prompt,
        "point_prompt": point_prompt,
    }
    with open("generate_requests.log", "a") as f:
        f.write(json.dumps(data) + "\n")
    point_prompt = np.array(point_prompt).reshape(-1, 3)
    points_xyz = point_prompt[0::2]
    points_xyz[:, 1] = -points_xyz[:, 1]
    points_rgb = point_prompt[1::2]

    min_vals = np.min(points_xyz, axis=0)
    max_vals = np.max(points_xyz, axis=0)

    # Compute the center and range for each dimension
    center = (max_vals + min_vals) / 2
    # normalize to [-0.5, 0.5]
    scale = max_vals - min_vals
    # scale[scale == 0] = 1.0
    scale = np.max(scale)

    # current_center = np.mean(points_xyz, axis=0)
    points_xyz = (points_xyz - center) / scale

    pcd = o3d.geometry.PointCloud()
    pcd.points = o3d.utility.Vector3dVector(points_xyz)
    pcd.colors = o3d.utility.Vector3dVector(points_rgb)  # Set the color for each point

    # Save to PLY file (including color)
    o3d.io.write_point_cloud(f"{time_stamp}.ply", pcd)

    # with open(f"{time_stamp}.log", "a") as f:
    #     f.write(
    #         "add:\n"
    #         + json.dumps(
    #             {
    #                 "id": id,
    #                 "point_xyz": points_xyz.tolist(),
    #                 "point_rgb": points_rgb.tolist(),
    #             }
    #         )
    #         + "\n"
    #     )
    return "Logged!"


@app.post("/adjust_color")
def adjust_color(
    id: Annotated[int, Body()],
    point_index: Annotated[list[int], Body()],
    R: Annotated[list[float], Body()],
    G: Annotated[list[float], Body()],
    B: Annotated[list[float], Body()],
):
    print(R)
    print(G)
    print(B)
    color_mask = get_mask_from_index(
        gsshop.gsobjs[id].gaussian.points_num, np.array(point_index)
    )

    ori_color = (gsshop.gsobjs[id].gaussian.get_rgb * 255).long()

    with torch.no_grad():
        rgb = torch.stack(
            [torch.tensor(R).cuda(), torch.tensor(G).cuda(), torch.tensor(B).cuda()],
            dim=-1,
        ).long()
        new_rgb = torch.gather(rgb, 0, ori_color).float() / 255
        gsshop.gsobjs[id].gaussian.set_rgb(new_rgb, color_mask)
        gsshop.update(gsshop.gsobjs[id])


# Camera
@app.post("/add_cam", response_description="Add cameras")
async def add_cam(
    cam_translation: Annotated[list[float], Body()],
    cam_wxyz: Annotated[list[float], Body()],
):
    gsshop.add_cam(cam_translation, cam_wxyz)


@app.post("/clear_cams", response_description="Clear cameras")
async def clear_cams():
    gsshop.cams = []


@app.get("/get_viser_cam", response_description="Get preview camera")
async def get_preview_cam():
    if webui.status:
        return {
            "cam_translation": webui.client.camera.position.tolist(),
            "cam_wxyz": webui.client.camera.wxyz.tolist(),
        }
    else:
        return None


@app.post("/set_viser_cam", response_description="Set preview camera")
async def set_preview_cam(
    cam_translation: Annotated[list[float], Body()],
    cam_wxyz: Annotated[list[float], Body()],
):
    if webui.status:
        webui.camera = SimpleNamespace(
            wxyz=np.array(cam_wxyz), position=np.array(cam_translation)
        )
        return "Done"
    else:
        return "WebUI not running"


@app.post("/get_unity_preview", response_description="Get preview render")
async def get_unity_preview(
    cam_translation: Annotated[list[float], Body()],
    cam_wxyz: Annotated[list[float], Body()],
):

    cam_translation[1] = -cam_translation[1]
    cam_wxyz[1] = -cam_wxyz[1]
    cam_wxyz[3] = -cam_wxyz[3]
    cam = MiniCam.from_cam_params(cam_translation, cam_wxyz, 1, 1.2, 1.2, 512, 512)
    img = gsshop.render(cam)["render"]
    url_path = os.path.join(_output_dir, "img", str(uuid.uuid4())[:8] + ".png")
    tensor_save_img(img, url_path)
    return url_path.removeprefix(_base_dir)


@app.get("/get_preview", response_description="Get preview render")
async def get_rendering():
    print(type(webui.camera))
    img = gsshop.render(webui.camera)["render"]
    url_path = os.path.join(_output_dir, "img", str(uuid.uuid4())[:8] + ".png")
    tensor_save_img(img, url_path)
    return ImageObject(id=0, cam_id=0, url_path=url_path.removeprefix(_base_dir))


@app.get(
    "/get_video_along_cams",
    response_description="Get preview video following camera trajectory",
)
async def render_video_with_cams():
    if len(gsshop.cams) < 2:
        return "Need at least two cameras to render video!"
    gsshop.render_video_with_cams()
    return "Done!"


@app.get(
    "/get_mask_video_along_cams",
    response_description="Get preview video following camera trajectory",
)
async def render_mask_video_with_cams():
    if len(gsshop.cams) < 2:
        return "Need at least two cameras to render video!"
    gsshop.render_mask_video_with_cams()
    return "Done!"


@app.get("/get_preview_video", response_description="Get preview video")
async def get_video(id: int = 0, coord_type: str = "RFU"):
    # print(type(webui.camera))
    # img = gsshop.render(webui.camera)["render"]
    # url_path = os.path.join(output_dir, "img", str(uuid.uuid4())[:8] + ".png")
    # tensor_save_img(img, url_path)
    # return ImageObject(id=0, cam_id=0, url_path=url_path)
    gsshop.render_obj_video(id, coord_type=coord_type)
    return "Done!"
