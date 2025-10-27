import os

import numpy as np
from random import randint
from types import SimpleNamespace
import torch
import json
from tqdm import tqdm
from concave_hull import concave_hull_indexes
import pytorch3d.transforms
from omegaconf import OmegaConf
import math
import torch.nn.functional as F

# from threestudio.utils.ops import (
#     get_mvp_matrix,
#     get_projection_matrix,
#     get_ray_directions,
#     get_rays,
# )
# from threestudio.models.prompt_processors.stable_diffusion_prompt_processor import (
#     StableDiffusionPromptProcessor,
# )
# from threestudio.models.guidance.instructpix2pix_guidance import (
#     InstructPix2PixGuidance,
# )

from gaussianshopvr.utils.coordinate_utils import get_coordinate_trans_matrix
from gaussianshopvr.utils.image_utils import (
    load_image,
    save_2dpoints_img,
    save_hull_img,
    tensor_save_img,
    cvt_tensor_img,
    get_inpainted_imgs,
    get_inpainted_imgs_online,
    get_inpainted_imgs_lama,
)
from gaussianshopvr.utils.video_utils import seq2video
from gaussianshopvr.utils.graphics_utils import (
    get_extent,
    world2screen,
    decompose_matrix,
    interpolate_extrinsics,
)
from gaussianshopvr.utils.server import time_string
from gaussianshopvr.utils.graphics_utils import BasicPointCloud
from gaussianshopvr.core.gaussian_model import SimpleGaussianModel, GaussianModel
from gaussianshopvr.core.gaussian_object import (
    GSObject,
    ContainerObject,
    GaussianObject,
)

from gaussianshopvr.utils.dataset_readers import (
    sceneLoadTypeCallbacks,
)
from gaussianshopvr.core.cameras import (
    MiniCam,
)
from gaussianshopvr.utils.camera_utils import (
    cameraList_from_camInfos,
)
from gaussianshopvr.core import gaussian_renderer
from gaussianshopvr.utils.loss_utils import l1_loss, ssim
from gaussianshopvr.utils.camera_utils import cam_colmap2viser, cam_viser2colmap


class gaussianshopvr:
    def __init__(self, cam_dir=None):
        self.gsobjs: dict[int, GSObject] = {}
        self.obj_num = 0

        # objects[0] is the root scene
        self.gsobjs[0] = ContainerObject(id=0, is_leaf=False)

        self.cam_dir = os.path.abspath(cam_dir) if cam_dir else cam_dir
        self.load_cameras()

        self.cams = []

        self.edit_status = -1
        self.color_adjust_status = -1

    # Project Management
    @property
    def info(self):
        gsshop_info = {"obj_num": self.obj_num, "cam_dir": self.cam_dir}
        gsobjs_info = {}
        for gsobj in self.gsobjs.values():
            gsobj_info = gsobj.info
            gsobjs_info[gsobj.id] = gsobj_info
        gsshop_info["gsobjs_info"] = gsobjs_info
        return gsshop_info

    def save(self, dir_path):
        dir_path = os.path.abspath(dir_path)
        os.makedirs(dir_path)
        gsshop_info = self.info
        gsobjs_info = gsshop_info["gsobjs_info"]
        for gsobj in self.gsobjs.values():
            path = os.path.join(dir_path, f"{gsobj.id}.ply")
            gsobj_info = gsobjs_info[gsobj.id]
            if gsobj.is_leaf:
                gsobj.gaussian.save_ply(path)
                gsobj_info["path"] = path
        with open(os.path.join(dir_path, "manifest.json"), "w") as f:
            json.dump(gsshop_info, f)

    def load(self, path):
        self.reset()
        path = os.path.join(os.path.abspath(path), "manifest.json")
        with open(path, "r") as f:
            gsshop_info = json.load(f)

        self.obj_num = gsshop_info["obj_num"]
        self.cam_dir = gsshop_info["cam_dir"]
        self.load_cameras()

        self.remove_obj(0)

        leaf_ids = []
        for id, gsobj_info in gsshop_info["gsobjs_info"].items():
            id = int(id)
            # convert from left-hand system to right-hand system
            gsobj_info["translation"][1] = -gsobj_info["translation"][1]
            gsobj_info["wxyz"][1] = -gsobj_info["wxyz"][1]
            gsobj_info["wxyz"][3] = -gsobj_info["wxyz"][3]

            if gsobj_info["is_leaf"]:
                gsobj = GaussianObject(
                    **gsobj_info,
                    gs=os.path.join(os.path.dirname(path), gsobj_info["path"]),
                )
                leaf_ids.append(id)
            else:
                gsobj = ContainerObject(**gsobj_info)
            self.gsobjs[id] = gsobj

        for gsobj in self.gsobjs.values():
            if gsobj.parent is not None:
                gsobj.parent = self.gsobjs[gsobj.parent]
                gsobj.parent.children.append(gsobj)

    def reset(self):
        self.obj_num = 0
        self.cam_dir = None
        self.remove_obj(0)
        self.load_cameras()
        self.gsobjs[0] = ContainerObject(id=0, is_leaf=False)
        self.edit_status = -1
        self.color_adjust_status = -1
        self.color_obj_id = None
        self.editing_obj_id = None
        self.editing_context_id = None
        self.text_prompt = None
        self.cams = []
        self.edited_imgs = []

    @torch.no_grad
    def load_cameras(self):
        if self.cam_dir is None:
            self.colmap_cameras_extent = 10
            self.colmap_cameras = []
            return
        if os.path.exists(os.path.join(self.cam_dir, "sparse")):
            scene_info = sceneLoadTypeCallbacks["Colmap"](self.cam_dir, None, False)
        elif os.path.exists(os.path.join(self.cam_dir, "transforms_train.json")):
            print("Found transforms_train.json file, assuming Blender data set!")
            scene_info = sceneLoadTypeCallbacks["Blender"](self.cam_dir, False, False)
        else:
            assert False, "Could not recognize scene type!"
        self.colmap_cameras_extent = scene_info.nerf_normalization["radius"]
        self.colmap_cameras = cameraList_from_camInfos(
            scene_info.train_cameras,
            1,
            SimpleNamespace(resolution=1, data_device="cuda"),
        )

    @torch.no_grad
    def is_decendant(self, decendant_obj, ancestor_obj):
        while decendant_obj is not None:
            if decendant_obj == ancestor_obj:
                return True
            else:
                decendant_obj = decendant_obj.parent
        return False

    @torch.no_grad
    def save_ply(self, dir_path, id=0):
        path = os.path.join(dir_path, f"{id}", f"{time_string()}.ply")
        self.gsobjs[id].gaussian.save_ply(path)
        return path

    def update(self, gsobj):
        while gsobj is not None:
            if not gsobj.is_leaf:
                gsobj.cached = False
            gsobj = gsobj.parent

    # Ojbect Management
    @torch.no_grad
    def remove_obj(self, id):
        parent = self.gsobjs[id].parent
        for idx, gs_obj in self.gsobjs.items():
            print(idx, gs_obj.translation, gs_obj.scale, gs_obj.wxyz)
        if parent is not None:
            parent.children.remove(self.gsobjs[id])

        def _remove_obj(pt_id):
            if not self.gsobjs[pt_id].is_leaf:
                while len(self.gsobjs[pt_id].children):
                    child = self.gsobjs[pt_id].children.pop()
                    _remove_obj(child.id)
            self.gsobjs.pop(pt_id)

        _remove_obj(id)

        self.update(parent)

        for idx, gs_obj in self.gsobjs.items():
            print(id, gs_obj.translation, gs_obj.scale, gs_obj.wxyz)

    @torch.no_grad
    def transform(self, id, translation, wxyz, scale):
        self.gsobjs[id].translation = translation
        self.gsobjs[id].wxyz = wxyz
        self.gsobjs[id].scale = scale
        self.update(self.gsobjs[id])

    @torch.no_grad
    def add_obj(self, gs, parent=0):
        self.obj_num += 1
        self.gsobjs[self.obj_num] = GaussianObject(
            id=self.obj_num, gs=gs, parent=self.gsobjs[parent]
        )
        self.gsobjs[parent].children.append(self.gsobjs[self.obj_num])
        self.update(self.gsobjs[self.obj_num])
        return self.obj_num

    # Editing
    @torch.no_grad
    def add_points(self, id, points_xyz, points_rgb):
        points_xyz = torch.tensor(points_xyz)
        points_rgb = torch.tensor(points_rgb)
        mask = self.gsobjs[id].add_points(points_xyz, points_rgb)
        self.update(self.gsobjs[id])
        return mask

    @torch.no_grad
    def remove_points(self, id, point_index):
        self.gsobjs[id].remove_points(point_index)
        self.update(self.gsobjs[id])

    @torch.no_grad
    def split(self, id, point_index):
        self.obj_num += 1
        print(
            "Before split:",
            np.max(point_index),
            self.gsobjs[id].gaussian.points_num,
        )
        new_gsobj = GaussianObject(
            id=self.obj_num,
            gs=self.gsobjs[id].split(point_index),
            parent=self.gsobjs[id].parent,
        )
        new_gsobj.parent = self.gsobjs[id].parent
        new_gsobj.parent.children.append(new_gsobj)

        print(
            "After split:",
            self.gsobjs[id].gaussian.points_num,
            new_gsobj.gaussian.points_num,
        )
        self.gsobjs[self.obj_num] = new_gsobj

        self.update(self.gsobjs[id])
        self.update(self.gsobjs[self.obj_num])

        return self.obj_num

    @torch.no_grad
    def merge_obj(self, id1, id2):
        gsobj1 = self.gsobjs[id1]
        gsobj2 = self.gsobjs[id2]

        world_transform1 = gsobj1.world_transform
        world_transform2 = gsobj2.world_transform

        transform = world_transform2.compose(world_transform1.inverse())
        scale, rotation_mat, translation = decompose_matrix(
            transform.get_matrix().squeeze().transpose(0, 1)
        )
        wxyz = pytorch3d.transforms.matrix_to_quaternion(rotation_mat)
        gsobj1.add_gsobj(gsobj2, translation=translation, wxyz=wxyz, scale=scale)
        self.remove_obj(id2)
        self.update(gsobj1)

    # Rendering
    def add_cam(self, cam_translation, cam_wxyz):
        cam = MiniCam.from_cam_params(
            cam_translation, cam_wxyz, 1, 1.2, 1.2, 1024, 1024
        )
        self.cams.append(cam)

    @torch.no_grad
    def render(self, cam, id=0, override_color=None):
        return self.gsobjs[id].render(cam, override_color)

    @torch.no_grad
    def render_video_with_cams(self, cams_id=None):
        if cams_id is None:
            cams_id = list(range(len(self.cams)))
        cams_extrinics = [
            self.cams[cid].world_view_transform.transpose(0, 1).cpu().numpy()
            for cid in cams_id
        ]
        pair_cams = zip(cams_extrinics[:-1], cams_extrinics[1:])
        new_cams_extrinics = []
        for st_cam, ed_cam in pair_cams:
            inter_cams = [
                interpolate_extrinsics(st_cam, ed_cam, alpha)
                for alpha in np.linspace(0, 1, 60)
            ]
            new_cams_extrinics.extend(inter_cams)
        renders = []
        for cam_extrinics in new_cams_extrinics:
            cam = MiniCam.from_c2w(np.linalg.inv(cam_extrinics), 1.2, 1.2, 1024, 1024)
            render = torch.clamp(self.render(cam)["render"], 0.0, 1.0)
            renders.append(np.array(cvt_tensor_img(render)))

        seq2video(renders, f"video/{time_string()}.mp4", 1024, 1024, 30)

    @torch.no_grad
    def render_mask_video_with_cams(self, cams_id=None):
        if cams_id is None:
            cams_id = list(range(len(self.cams)))
        cams_extrinics = [
            self.cams[cid].world_view_transform.transpose(0, 1).cpu().numpy()
            for cid in cams_id
        ]
        pair_cams = zip(cams_extrinics[:-1], cams_extrinics[1:])
        new_cams_extrinics = []
        for st_cam, ed_cam in pair_cams:
            inter_cams = [
                interpolate_extrinsics(st_cam, ed_cam, alpha)
                for alpha in np.linspace(0, 1, 60)
            ]
            new_cams_extrinics.extend(inter_cams)
        renders = []
        pt_imgs = []
        mask_imgs = []
        with open("point_xyz.log", "r") as f:
            point_prompt = json.load(f)["point_prompt"]
            point_prompt = np.array(point_prompt).reshape(-1, 3)
            ori_points_xyz = point_prompt[0::2]
            ori_points_xyz[:, 1] = -ori_points_xyz[:, 1]
        for cam_extrinics in tqdm(new_cams_extrinics):
            cam = MiniCam.from_c2w(np.linalg.inv(cam_extrinics), 1.2, 1.2, 1024, 1024)
            render = torch.clamp(self.render(cam)["render"], 0.0, 1.0)
            renders.append(np.array(cvt_tensor_img(render)))

            points_xyz = torch.tensor(ori_points_xyz).float().cuda()
            ori_points_xyz = points_xyz.clone()
            homo_cord = torch.ones((points_xyz.size(0), 1)).cuda()
            points_xyz = torch.cat((points_xyz, homo_cord), dim=1)

            # Project 3D points to screen space
            n_x = cam.image_width
            n_y = cam.image_height
            m_vp = torch.tensor(
                [
                    [n_x / 2, 0, 0, (n_x - 1) / 2],
                    [0, n_y / 2, 0, (n_y - 1) / 2],
                    [0, 0, 1, 0],
                    [0, 0, 0, 1],
                ]
            ).cuda()

            points_xyz = (
                m_vp
                @ cam.full_proj_transform.transpose(0, 1)
                @ points_xyz.unsqueeze(-1)
            )
            points_xyz = points_xyz.squeeze(-1)
            points_xyz = (points_xyz / points_xyz[:, -1:]).int()
            visible_mask = (
                (points_xyz[:, 0] >= 0)
                & (points_xyz[:, 0] < cam.image_width)
                & (points_xyz[:, 1] >= 0)
                & (points_xyz[:, 1] < cam.image_height)
            )
            # ori_points_xyz = ori_points_xyz[visible_mask]
            points_xyz = points_xyz[visible_mask]

            points_xy = points_xyz[:, :2].cpu().numpy()

            pt_img = (
                torch.tensor(
                    save_2dpoints_img(points_xy, cam.image_width, cam.image_height)
                ).cuda()
                / 255
            )
            pt_imgs.append(np.array(cvt_tensor_img(pt_img)))

            concave_hull = concave_hull_indexes(points_xy[:, :2])
            ch_img = (
                torch.tensor(
                    save_hull_img(
                        points_xy[concave_hull], cam.image_width, cam.image_height
                    )
                ).cuda()
                / 255
            )
            mask_imgs.append(np.array(cvt_tensor_img(ch_img)))

        seq2video(renders, f"video/renders.mp4", 1024, 1024, 30)
        seq2video(pt_imgs, f"video/points.mp4", 1024, 1024, 30)
        seq2video(mask_imgs, f"video/mask.mp4", 1024, 1024, 30)

    @torch.no_grad
    def render_obj_video(self, id=0, distance=None, coord_type="RFU"):
        n_views = 120
        azimuth_deg = torch.linspace(0.0, 360.0, n_views)
        elevation_deg = torch.full_like(azimuth_deg, 15)
        if distance is None:
            print(get_extent(self.gsobjs[id].gaussian._xyz) * 2)
            camera_distances = torch.full_like(
                elevation_deg, get_extent(self.gsobjs[id].gaussian._xyz) * 2
            )

        elevation = elevation_deg * math.pi / 180
        azimuth = azimuth_deg * math.pi / 180

        # convert spherical coordinates to cartesian coordinates
        # right hand coordinate system, x right, y forward, z up
        # elevation in (-90, 90), azimuth from +x to +y in (-180, 180)
        camera_positions = torch.stack(
            [
                torch.cos(elevation) * torch.cos(azimuth),
                torch.cos(elevation) * torch.sin(azimuth),
                torch.sin(elevation),
            ],
            dim=-1,
        )
        camera_positions = camera_positions * camera_distances[..., None]
        trans_matrix = torch.tensor(
            get_coordinate_trans_matrix(coord_type, "RFU")
        ).float()
        # print(trans_matrix.type(), camera_positions.type())
        camera_positions = (trans_matrix @ camera_positions[..., None]).squeeze(-1)

        origin = self.gsobjs[id].gaussian._xyz.mean(dim=0).cpu()
        camera_positions += origin

        # default scene center at origin
        center = torch.zeros_like(camera_positions) + origin

        up_dir = trans_matrix @ np.array([0, 0, 1])
        print(up_dir)
        # default camera up direction as +z
        up = torch.as_tensor(up_dir, dtype=torch.float32)[None, :].repeat(1, 1)

        fovy_deg = torch.full_like(elevation_deg, 70)
        fovy = fovy_deg * math.pi / 180

        lookat = F.normalize(center - camera_positions, dim=-1)
        right = F.normalize(torch.cross(lookat, up), dim=-1)
        up = F.normalize(torch.cross(right, lookat), dim=-1)

        # GS use RDF for camera coordinate
        c2w3x4 = torch.cat(
            [torch.stack([right, -up, lookat], dim=-1), camera_positions[:, :, None]],
            dim=-1,
        )
        c2w = torch.cat([c2w3x4, torch.zeros_like(c2w3x4[:, :1])], dim=1)
        c2w[:, 3, 3] = 1.0

        # get directions by dividing directions_unit_focal by focal length
        # focal_length = 0.5 * 1024 / torch.tan(0.5 * fovy)
        # c2w_3dgs = []
        renders = []
        cams = []

        w, h = 1024, 1024

        for cid in range(n_views):
            # cam = MiniCam.from_cam_params(center_position[id], ,1,c2w_single, fovy[0], fovy[0], 1024, 1024)
            cam = MiniCam.from_c2w(c2w[cid].numpy(), fovy[0], fovy[0], w, h)
            cams.append(cam)
            render = torch.clamp(self.render(cam, id=id)["render"], 0.0, 1.0)
            renders.append(np.array(cvt_tensor_img(render)))
            tensor_save_img(
                render,
                f"video/{cid}.png",
            )
        seq2video(renders, f"video/{time_string()}.mp4", w, h, 30)

    # AI Editing
    def sds_optimize():
        self.gaussian.update_learning_rate(self.true_global_step)

        if self.true_global_step > 500:
            self.guidance.set_min_max_steps(
                min_step_percent=0.02, max_step_percent=0.55
            )

        self.gaussian.update_learning_rate(self.true_global_step)

        out = self(batch)

        prompt_utils = self.prompt_processor()
        images = out["comp_rgb"]

        guidance_eval = self.true_global_step % 200 == 0
        # guidance_eval = False

        guidance_out = self.guidance(
            images,
            prompt_utils,
            **batch,
            rgb_as_latents=False,
            guidance_eval=guidance_eval,
        )

        loss = 0.0

        loss = loss + guidance_out["loss_sds"] * self.C(self.cfg.loss["lambda_sds"])

        loss_sparsity = (out["opacity"] ** 2 + 0.01).sqrt().mean()
        self.log("train/loss_sparsity", loss_sparsity)
        loss += loss_sparsity * self.C(self.cfg.loss.lambda_sparsity)

        opacity_clamped = out["opacity"].clamp(1.0e-3, 1.0 - 1.0e-3)
        loss_opaque = binary_cross_entropy(opacity_clamped, opacity_clamped)
        self.log("train/loss_opaque", loss_opaque)
        loss += loss_opaque * self.C(self.cfg.loss.lambda_opaque)
        if guidance_eval:
            self.guidance_evaluation_save(
                out["comp_rgb"].detach()[: guidance_out["eval"]["bs"]],
                guidance_out["eval"],
            )
        for name, value in self.cfg.loss.items():
            self.log(f"train_params/{name}", self.C(value))

        return {"loss": loss}

    def optimize(self, img_ids):
        with torch.no_grad():
            cams = []
            gt_imgs = []
            points_rgb = []

            editing_gsobj = self.gsobjs[self.editing_obj_id]

            points_rgb_num = torch.zeros(
                editing_gsobj.gaussian.mask.sum(), dtype=torch.int
            ).cuda()
            points_rgb = torch.zeros(
                (editing_gsobj.gaussian.mask.sum(), 3), dtype=torch.float
            ).cuda()

            for img_id in img_ids:
                _ = self.edited_imgs[img_id]
                img = _["img"]
                points_xy = _["points_xy"]
                visibility_mask = _["visibility_mask"]
                cams.append(self.cams[_["cam_id"]])
                gt_imgs.append(img)
                points_rgb_num[visibility_mask] += 1
                points_rgb[visibility_mask] += (
                    img[:, points_xy[:, 1], points_xy[:, 0]].permute(1, 0).cuda()
                )

            points_rgb = points_rgb / points_rgb_num.unsqueeze(-1)
            new_rgb = torch.zeros((editing_gsobj.gaussian.points_num, 3)).cuda()
            new_rgb[editing_gsobj.gaussian.mask] = points_rgb
            # new_rgb = new_rgb.float() / 255
            # editing_gsobj.gaussian.set_rgb(new_rgb, editing_gsobj.gaussian.mask)
            # if self.edit_status == 0:
            # pass
            editing_gsobj.gaussian.set_opacity(
                torch.ones(editing_gsobj.gaussian.points_num) / 2,
                editing_gsobj.gaussian.mask,
            )
            self.update(editing_gsobj)
        self.train(
            self.gsobjs[self.editing_context_id],
            self.gsobjs[self.editing_obj_id],
            cams,
            gt_imgs,
        )

    def inpaint(
        self, id, text_prompt, points_xyz, point_rgb, cam_translation, cam_wxyz
    ):
        cam = MiniCam.from_cam_params(cam_translation, cam_wxyz, 1, 1, 1, 1024, 1024)
        gsobj = self.gsobjs[id]

        render_package = gsobj.render(cam)
        render_rgb = render_package["render"]
        tensor_save_img(render_rgb, "render.png")

        points_xyz = torch.tensor(points_xyz).float().cuda()
        ori_points_xyz = points_xyz.clone()
        homo_cord = torch.ones((points_xyz.size(0), 1)).cuda()
        points_xyz = torch.cat((points_xyz, homo_cord), dim=1)

        # Project 3D points to screen space
        n_x = cam.image_width
        n_y = cam.image_height
        m_vp = torch.tensor(
            [
                [n_x / 2, 0, 0, (n_x - 1) / 2],
                [0, n_y / 2, 0, (n_y - 1) / 2],
                [0, 0, 1, 0],
                [0, 0, 0, 1],
            ]
        ).cuda()

        points_xyz = (
            m_vp @ cam.full_proj_transform.transpose(0, 1) @ points_xyz.unsqueeze(-1)
        )
        points_xyz = points_xyz.squeeze(-1)
        points_xyz = (points_xyz / points_xyz[:, -1:]).int()
        visible_mask = (
            (points_xyz[:, 0] >= 0)
            & (points_xyz[:, 0] < cam.image_width)
            & (points_xyz[:, 1] >= 0)
            & (points_xyz[:, 1] < cam.image_height)
        )
        ori_points_xyz = ori_points_xyz[visible_mask]
        points_xyz = points_xyz[visible_mask]

        points_xy = points_xyz[:, :2].cpu().numpy()

        pt_img = (
            torch.tensor(
                save_2dpoints_img(points_xy, cam.image_width, cam.image_height)
            ).cuda()
            / 255
        )

        tensor_save_img(0.5 * render_rgb + 0.5 * torch.tensor(pt_img), "blend_pt.png")

        # Get 2D Mask by getting convex hull
        # convex_hull = ConvexHull(points_xy)

        concave_hull = concave_hull_indexes(points_xy[:, :2])
        ch_img = (
            torch.tensor(
                save_hull_img(
                    points_xy[concave_hull], cam.image_width, cam.image_height
                )
            ).cuda()
            / 255
        )

        tensor_save_img(0.5 * render_rgb + 0.5 * ch_img, "blend_ch.png")

        # get inpainted image
        # from PIL import Image
        # inpainted_img = torchvision.transforms.functional.to_tensor(
        #     Image.open("inpaint.png")
        # )
        inpainted_img = get_inpainted_img(render_rgb, ch_img, text_prompt)
        tensor_save_img(inpainted_img, "inpaint.png")

        # Project image pixels back to point cloud
        point_rgb = inpainted_img[:, points_xy[:, 1], points_xy[:, 0]].permute(1, 0)

        pt_img = (
            torch.tensor(
                save_2dpoints_img(
                    points_xy,
                    cam.image_width,
                    cam.image_height,
                    (point_rgb[..., [2, 1, 0]] * 255).tolist(),
                    "color_points.jpg",
                )
            ).cuda()
            / 255
        )
        tensor_save_img(0.5 * render_rgb + 0.5 * pt_img, "blend_color_pt.png")

        # Add colored points to GS Object
        self.add_points(ori_points_xyz.cpu(), point_rgb.cpu())

        cams = [cam]
        gt_imgs = [inpainted_img]

        self.train(gsobj, gsobj, mask, cams, gt_imgs)

    # @torch.no_grad
    # def add_cam(self, cam_translation, cam_wxyz, N=3, sd=True):
    #     cam = MiniCam.from_cam_params(
    #         cam_translation, cam_wxyz, 1, 1.2, 1.2, 1024, 1024
    #     )
    #     self.cams.append(cam)
    #     if sd:
    #         context_gsobj = self.gsobjs[self.editing_context_id]
    #         editing_gsobj = self.gsobjs[self.editing_obj_id]

    #         context_target_mask = self.get_context_target_mask(
    #             context_gsobj.id, editing_gsobj.id
    #         )

    #         # tmp_mask = torch.zeros(context_gsobj.gaussian.points_num).bool().cuda()
    #         # print(context_target_mask.sum())
    #         # print(editing_gsobj.gaussian.mask.sum())
    #         # tmp_mask[torch.nonzero(context_target_mask).squeeze()[editing_gsobj.gaussian.mask]] = 1
    #         # print(tmp_mask.sum())
    #         # context_gsobj.gaussian.mask = tmp_mask

    #         # render_package = context_gsobj.render(cam)
    #         # ch_img = render_package["masks"]

    #         editing_gsobj.gaussian.set_opacity(
    #             torch.zeros(editing_gsobj.gaussian.points_num),
    #             editing_gsobj.gaussian.mask,
    #         )
    #         self.update(editing_gsobj)
    #         render_package = context_gsobj.render(cam)

    #         editing_gsobj.gaussian.set_opacity(
    #             torch.ones(editing_gsobj.gaussian.points_num),
    #             editing_gsobj.gaussian.mask,
    #         )
    #         self.update(editing_gsobj)

    #         render_rgb = render_package["render"]
    #         tensor_save_img(render_rgb, "render.png")

    #         points_xyz = context_gsobj.gaussian.get_xyz[context_target_mask][
    #             editing_gsobj.gaussian.mask
    #         ]

    #         points_xy, visibility_mask = world2screen(points_xyz, cam)

    #         pt_img = (
    #             torch.tensor(
    #                 save_2dpoints_img(points_xy, cam.image_width, cam.image_height)
    #             ).cuda()
    #             / 255
    #         )

    #         tensor_save_img(
    #             0.5 * render_rgb + 0.5 * torch.tensor(pt_img), "blend_pt.png"
    #         )

    #         # Get 2D Mask by getting hull
    #         # convex_hull = ConvexHull(points_xy)
    #         concave_hull = concave_hull_indexes(points_xy[:, :2])
    #         ch_img = (
    #             torch.tensor(
    #                 save_hull_img(
    #                     points_xy[concave_hull], cam.image_width, cam.image_height
    #                 )
    #             ).cuda()
    #             / 255
    #         )
    #         tensor_save_img(0.5 * render_rgb + 0.5 * ch_img, "blend_ch.png")

    #         # inpainted_imgs = get_inpainted_imgs(render_rgb, ch_img, self.text_prompt, N)
    #         inpainted_imgs = get_inpainted_imgs_online(
    #             render_rgb, ch_img, self.text_prompt, N
    #         )
    #         # inpainted_imgs = get_inpainted_imgs_lama(render_rgb, ch_img, N)

    #         # files = ["inpaint_0.png", "inpaint_1.png", "inpaint_2.png"]
    #         # inpainted_imgs = [load_image(file) for file in files]

    #         for idx, _ in enumerate(inpainted_imgs):
    #             tensor_save_img(_, f"inpaint_{idx}.png")

    #         inpainted_imgs = [
    #             {
    #                 "img": img,
    #                 "cam_id": len(self.cams) - 1,
    #                 "points_xy": points_xy,
    #                 "visibility_mask": visibility_mask,
    #             }
    #             for img in inpainted_imgs
    #         ]

    #         self.edited_imgs += inpainted_imgs

    #         return list(range(len(self.edited_imgs)))[-N:]

    #     # Project image pixels back to point cloud
    #     # point_rgb = inpainted_img[:, points_xy[:, 1], points_xy[:, 0]].permute(1, 0)

    #     # pt_img = (
    #     #     torch.tensor(
    #     #         save_2dpoints_img(
    #     #             points_xy,
    #     #             cam.image_width,
    #     #             cam.image_height,
    #     #             (point_rgb[..., [2, 1, 0]] * 255).tolist(),
    #     #             "color_points.jpg",
    #     #         )
    #     #     ).cuda()
    #     #     / 255
    #     # )
    #     # tensor_save_img(0.5 * render_rgb + 0.5 * pt_img, "blend_color_pt.png")

    def train(self, context: GaussianObject, target: GaussianObject, cams, gt_imgs):
        if not target.is_leaf:
            print(f"Target {target.id} is not leaf")
            return
        if not self.is_decendant(target, context):
            print(f"Target {target.id} is not a decendant of Context {context.id}")
            return

        ema_loss_for_log = 0.0
        progress_bar = tqdm(range(0, target.opt.iterations), desc="Training progress")

        iter_start = torch.cuda.Event(enable_timing=True)
        iter_end = torch.cuda.Event(enable_timing=True)

        gt_imgs = [_.cuda() for _ in gt_imgs]

        context_target_mask = self.get_context_target_mask(context.id, target.id)

        init_points_xyz = target.gaussian._xyz[target.gaussian.mask].detach().clone()

        target.gaussian.reset_generation()

        with torch.no_grad():
            target.gaussian.spatial_lr_scale = np.array(
                get_extent(target.gaussian._xyz[target.gaussian.mask])
                * 1.5
                # .cpu()
                # .detach()
                # .numpy()
            )
            target.configure_optimizers()

        for iteration in range(target.opt.iterations):
            iter_start.record()
            target.gaussian.update_learning_rate(iteration)

            idx = randint(0, len(cams) - 1)
            cam = cams[idx]
            gt_img = gt_imgs[idx]

            self.update(target)

            render_pkg = gaussian_renderer.render(
                cam, context.gaussian, target.pipe, target.background_tensor
            )
            image, viewspace_point_tensor, visibility_filter, radii = (
                render_pkg["render"],
                render_pkg["viewspace_points"],
                render_pkg["visibility_filter"],
                render_pkg["radii"],
            )

            Ll1 = l1_loss(image, gt_img)
            loss = (1.0 - target.opt.lambda_dssim) * Ll1 + target.opt.lambda_dssim * (
                1.0 - ssim(image, gt_img)
            )

            new_points_xyz = target.gaussian._xyz[target.gaussian.mask]
            # print(init_points_xyz.shape, new_points_xyz.shape)
            distances = torch.cdist(new_points_xyz, init_points_xyz, p=2)
            min_distances, _ = torch.min(distances, dim=1)
            # print(min_distances.min(), min_distances.max())
            regularization_loss = min_distances.mean()
            # print(loss, regularization_loss)
            loss += 0.1 * regularization_loss
            # anchor_out = target.gaussian.anchor_loss()
            # loss += (
            #     1 * anchor_out["loss_anchor_color"]
            #     + 100 * anchor_out["loss_anchor_pos"]
            # + 1 * anchor_out["loss_anchor_opacity"]
            # + 100 * anchor_out["loss_anchor_scale"]
            # )

            loss.backward()

            iter_end.record()
            with torch.no_grad():
                grad = viewspace_point_tensor.grad[context_target_mask]
                viewspace_point_tensor = viewspace_point_tensor[context_target_mask]
                viewspace_point_tensor.grad = grad
                visibility_filter = visibility_filter[context_target_mask]
                radii = radii[context_target_mask]

                # Progress bar
                ema_loss_for_log = 0.4 * loss.item() + 0.6 * ema_loss_for_log
                progress_bar.set_postfix({"Loss": f"{ema_loss_for_log:.{7}f}"})
                progress_bar.update(1)
                if iteration == target.opt.iterations:
                    progress_bar.close()

                # Log and save
                # training_report(
                #     tb_writer,
                #     iteration,
                #     Ll1,
                #     loss,
                #     l1_loss,
                #     iter_start.elapsed_time(iter_end),
                #     testing_iterations,
                #     scene,
                #     render,
                #     (pipe, background),
                # )
                # Densification
                if iteration < target.opt.densify_until_iter:
                    # Keep track of max radii in image-space for pruning
                    target.gaussian.max_radii2D[visibility_filter] = torch.max(
                        target.gaussian.max_radii2D[visibility_filter],
                        radii[visibility_filter],
                    )
                    target.gaussian.add_densification_stats(
                        viewspace_point_tensor, visibility_filter
                    )

                    if (
                        iteration > target.opt.densify_from_iter
                        and iteration % target.opt.densification_interval == 0
                    ):
                        size_threshold = (
                            10
                            if iteration > target.opt.opacity_reset_interval
                            else None
                        )
                        target.gaussian.densify_and_prune(
                            target.opt.densify_grad_threshold,
                            1,
                            0.005,
                            target.gaussian.spatial_lr_scale,
                            size_threshold,
                        )
                        target.gaussian.update_anchor()
                        self.update(target)
                        context_target_mask = self.get_context_target_mask(
                            context.id, target.id
                        )

                    # reset opacity
                    if iteration % target.opt.opacity_reset_interval == 0:
                        target.gaussian.set_opacity(
                            torch.ones(target.gaussian.points_num) * 0.01,
                            target.gaussian.mask,
                        )

                # fields = [
                #     "_xyz",
                #     "_features_dc",
                #     "_features_rest",
                #     "_opacity",
                #     "_scaling",
                #     "_rotation",
                # ]
                # for f in fields:
                #     try:
                #         print(
                #             f, getattr(target.gaussian, f).grad[~target.gaussian.mask].sum()
                #         )
                #     except Exception:
                #         pass

                # Optimizer step
                if iteration < target.opt.iterations:
                    target.gaussian.optimizer.step()
                    target.gaussian.optimizer.zero_grad(set_to_none=True)

    def get_context_target_mask(self, context_id, target_id):
        mask = torch.ones(self.gsobjs[target_id].gaussian.points_num, dtype=torch.bool)
        pt = target_id
        while pt != context_id:
            parent_id = self.gsobjs[pt].parent.id
            new_mask = torch.zeros(
                self.gsobjs[parent_id].gaussian.points_num,
                dtype=torch.bool,
            )
            new_mask[self.gsobjs[parent_id].gaussian._id == pt] = mask
            mask = new_mask
            pt = parent_id
        return mask.cuda()

    def train_guidance(self, context: GaussianObject, target: GaussianObject, cams):
        if not target.is_leaf:
            print(f"Target {target.id} is not leaf")
            return
        if not self.is_decendant(target, context):
            print(f"Target {target.id} is not a decendant of Context {context.id}")
            return

        with torch.no_grad():
            ori_imgs = []
            prompt_utils = StableDiffusionPromptProcessor(
                {
                    "pretrained_model_name_or_path": "runwayml/stable-diffusion-v1-5",
                    "prompt": self.text_prompt,
                }
            )()
            guidance = InstructPix2PixGuidance(
                OmegaConf.create({"min_step_percent": 0.02, "max_step_percent": 0.98})
            )
            for idx, cam in enumerate(cams):
                rendering = gaussian_renderer.render(
                    cam, context.gaussian, target.pipe, target.background_tensor
                )["render"]
                ori_imgs.append(rendering)
                tensor_save_img(rendering, f"preview/{idx}.png")
            gt_imgs = {}

        ema_loss_for_log = 0.0
        progress_bar = tqdm(range(0, target.opt.iterations), desc="Training progress")

        iter_start = torch.cuda.Event(enable_timing=True)
        iter_end = torch.cuda.Event(enable_timing=True)

        context_target_mask = self.get_context_target_mask(context.id, target.id)

        init_points_xyz = target.gaussian._xyz[target.gaussian.mask].detach().clone()

        target.gaussian.reset_generation()

        with torch.no_grad():
            print(target.gaussian.mask)
            target.gaussian.spatial_lr_scale = (
                (get_extent(target.gaussian._xyz[target.gaussian.mask]) * 1.5)
                .cpu()
                .detach()
                .numpy()
            )
            target.configure_optimizers()
            for param_group in target.gaussian.optimizer.param_groups:
                if param_group["name"] == "f_dc":
                    param_group["lr"] *= 20

        for iteration in range(target.opt.iterations):
            iter_start.record()

            target.gaussian.update_learning_rate(iteration)

            idx = randint(0, len(cams) - 1)
            cam = cams[idx]

            self.update(target)
            render_pkg = gaussian_renderer.render(
                cam, context.gaussian, target.pipe, target.background_tensor
            )
            image, viewspace_point_tensor, visibility_filter, radii = (
                render_pkg["render"],
                render_pkg["viewspace_points"],
                render_pkg["visibility_filter"],
                render_pkg["radii"],
            )
            if iteration < target.opt.iterations / 2:
                # guidance.cfg.guidance_scale = 15
                # guidance.cfg.condition_scale = 0.8
                flag = iteration % 200 == 0
            else:
                guidance.cfg.guidance_scale = 7.5
                guidance.cfg.condition_scale = 1.5
                flag = iteration % 50 == 0
            if idx not in gt_imgs.keys() or flag:
                with torch.no_grad():
                    gt_imgs[idx] = guidance(
                        image.unsqueeze(0).permute(0, 2, 3, 1),
                        ori_imgs[idx].unsqueeze(0).permute(0, 2, 3, 1),
                        prompt_utils,
                    )["edit_images"][0].permute(2, 0, 1)
                    gt_img = gt_imgs[idx]
                    tensor_save_img(gt_img, f"in2n/{idx}.png")
                    tensor_save_img(gt_img, "in2n.png")
                    tensor_save_img(ori_imgs[idx], "in2n_ori.png")
            else:
                gt_img = gt_imgs[idx]

            Ll1 = l1_loss(image, gt_img)
            loss = (1.0 - target.opt.lambda_dssim) * Ll1 + target.opt.lambda_dssim * (
                1.0 - ssim(image, gt_img)
            )

            # new_points_xyz = target.gaussian._xyz[target.gaussian.mask]
            # print(init_points_xyz.shape, new_points_xyz.shape)
            # distances = torch.cdist(target.gaussian._xyz[target.gaussian.mask], init_points_xyz, p=2)
            # min_distances, _ = torch.min(distances, dim=1)
            # # print(min_distances.min(), min_distances.max())
            # regularization_loss = min_distances.mean()
            # # print(loss, regularization_loss)
            # loss += 0.1 * regularization_loss

            # anchor_out = target.gaussian.anchor_loss()
            # loss += (
            #     1 * anchor_out["loss_anchor_color"]
            #     + 100 * anchor_out["loss_anchor_pos"]
            #     # + 1 * anchor_out["loss_anchor_opacity"]
            #     # + 100 * anchor_out["loss_anchor_scale"]
            # )

            loss.backward()

            fields = [
                # "_xyz",
                # "_features_rest",
                # "_scaling",
                # "_rotation",
            ]
            for field in fields:
                getattr(target.gaussian, field).grad = None

            iter_end.record()
            with torch.no_grad():
                grad = viewspace_point_tensor.grad[context_target_mask]
                viewspace_point_tensor = viewspace_point_tensor[context_target_mask]
                viewspace_point_tensor.grad = grad
                visibility_filter = visibility_filter[context_target_mask]
                radii = radii[context_target_mask]

                # Progress bar
                ema_loss_for_log = 0.4 * loss.item() + 0.6 * ema_loss_for_log
                progress_bar.set_postfix({"Loss": f"{ema_loss_for_log:.{7}f}"})
                progress_bar.update(1)
                if iteration == target.opt.iterations:
                    progress_bar.close()

                #         # Densification
                if iteration < target.opt.densify_until_iter:
                    # Keep track of max radii in image-space for pruning
                    target.gaussian.max_radii2D[visibility_filter] = torch.max(
                        target.gaussian.max_radii2D[visibility_filter],
                        radii[visibility_filter],
                    )
                    target.gaussian.add_densification_stats(
                        viewspace_point_tensor, visibility_filter
                    )

                    if (
                        iteration > target.opt.densify_from_iter
                        and iteration % target.opt.densification_interval == 0
                    ):
                        size_threshold = (
                            10
                            if iteration > target.opt.opacity_reset_interval
                            else None
                        )
                        target.gaussian.densify_and_prune(
                            target.opt.densify_grad_threshold,
                            1,
                            0.005,
                            target.gaussian.spatial_lr_scale,
                            size_threshold,
                        )
                        target.gaussian.update_anchor()
                        self.update(target)
                        context_target_mask = self.get_context_target_mask(
                            context.id, target.id
                        )

                    # reset opacity
                    if iteration % target.opt.opacity_reset_interval == 0:
                        target.gaussian.set_opacity(
                            torch.ones(target.gaussian.points_num) * 0.01,
                            target.gaussian.mask,
                        )

                #         # fields = [
                #         #     "_xyz",
                #         #     "_features_dc",
                #         #     "_features_rest",
                #         #     "_opacity",
                #         #     "_scaling",
                #         #     "_rotation",
                #         # ]
                #         # for f in fields:
                #         #     try:
                #         #         print(
                #         #             f, getattr(target.gaussian, f).grad[~target.gaussian.mask].sum()
                #         #         )
                #         #     except Exception:
                #         #         pass

                # Optimizer step
                if iteration < target.opt.iterations:
                    target.gaussian.optimizer.step()
                    target.gaussian.optimizer.zero_grad(set_to_none=True)

        big_points_ws = (
            target.gaussian.get_scaling.max(dim=1).values
            > 0.1 * target.gaussian.spatial_lr_scale
        )
        print(big_points_ws.sum())
        target.gaussian.prune_points(big_points_ws)
        target.gaussian.remove_grad_mask()
        target.gaussian.apply_grad_mask()
