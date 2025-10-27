#
# Copyright (C) 2023, Inria
# GRAPHDECO research group, https://team.inria.fr/graphdeco
# All rights reserved.
#
# This software is free for non-commercial, research and evaluation use
# under the terms of the LICENSE.md file.
#
# For inquiries contact  george.drettakis@inria.fr
#

import torch
from torch import nn
import numpy as np
from gaussianshopvr.utils.graphics_utils import (
    getWorld2View2,
    getProjectionMatrix,
    focal2fov,
    fov2focal,
    getWorld2View2_tensor,
)

from gaussianshopvr.utils.graphics_utils import get_c2w_from_wxyz_trans


class Camera(nn.Module):
    def __init__(
        self,
        colmap_id,
        R,
        T,
        FoVx,
        FoVy,
        image,
        gt_alpha_mask,
        image_name,
        uid,
        trans=np.array([0.0, 0.0, 0.0]),
        scale=1.0,
        data_device="cuda",
    ):
        super(Camera, self).__init__()

        self.uid = uid
        self.colmap_id = colmap_id
        self.R = R
        self.T = T
        self.FoVx = FoVx
        self.FoVy = FoVy
        self.image_name = image_name

        try:
            self.data_device = torch.device(data_device)
        except Exception as e:
            print(e)
            print(
                f"[Warning] Custom device {data_device} failed, fallback to default cuda device"
            )
            self.data_device = torch.device("cuda")

        self.original_image = image.clamp(0.0, 1.0).to(self.data_device)
        self.image_width = self.original_image.shape[2]
        self.image_height = self.original_image.shape[1]

        self.gt_alpha_mask = gt_alpha_mask

        if gt_alpha_mask is not None:
            self.original_image *= gt_alpha_mask.to(self.data_device)
        else:
            self.original_image *= torch.ones(
                (1, self.image_height, self.image_width), device=self.data_device
            )

        self.zfar = 100.0
        self.znear = 0.01

        self.trans = trans
        self.scale = scale

        self.world_view_transform = (
            torch.tensor(getWorld2View2(R, T, trans, scale)).transpose(0, 1).cuda()
        )
        self.projection_matrix = (
            getProjectionMatrix(
                znear=self.znear, zfar=self.zfar, fovX=self.FoVx, fovY=self.FoVy
            )
            .transpose(0, 1)
            .cuda()
        )
        self.full_proj_transform = (
            self.world_view_transform.unsqueeze(0).bmm(
                self.projection_matrix.unsqueeze(0)
            )
        ).squeeze(0)
        self.camera_center = self.world_view_transform.inverse()[3, :3]


class Simple_Camera(nn.Module):
    def __init__(
        self,
        colmap_id,
        R,
        T,
        FoVx,
        FoVy,
        h,
        w,
        image_name,
        uid,
        trans=np.array([0.0, 0.0, 0.0]),
        scale=1.0,
        data_device="cuda",
        qvec=None,
    ):
        super(Simple_Camera, self).__init__()

        self.uid = uid
        self.colmap_id = colmap_id
        self.R = R
        self.T = T
        self.FoVx = FoVx
        self.FoVy = FoVy
        self.image_name = image_name
        self.qvec = qvec

        try:
            self.data_device = torch.device(data_device)
        except Exception as e:
            print(e)
            print(
                f"[Warning] Custom device {data_device} failed, fallback to default cuda device"
            )
            self.data_device = torch.device("cuda")

        self.image_width = w
        self.image_height = h

        self.zfar = 100.0
        self.znear = 0.01

        self.trans = trans
        self.scale = scale

        self.world_view_transform = (
            torch.tensor(getWorld2View2(R, T, trans, scale)).transpose(0, 1).cuda()
        )
        self.projection_matrix = (
            getProjectionMatrix(
                znear=self.znear, zfar=self.zfar, fovX=self.FoVx, fovY=self.FoVy
            )
            .transpose(0, 1)
            .cuda()
        )
        self.full_proj_transform = (
            self.world_view_transform.unsqueeze(0).bmm(
                self.projection_matrix.unsqueeze(0)
            )
        ).squeeze(0)
        self.camera_center = self.world_view_transform.inverse()[3, :3]

    def HW_scale(self, h, w):
        return Simple_Camera(
            self.colmap_id,
            self.R,
            self.T,
            self.FoVx,
            self.FoVy,
            h,
            w,
            self.image_name,
            self.uid,
            qvec=self.qvec,
        )


class C2W_Camera(nn.Module):
    def __init__(
        self,
        c2w,
        FoVy,
        height,
        width,
        trans=torch.tensor([0.0, 0.0, 0.0]),
        scale=1.0,
        data_device="cuda",
        azimuth=None,
        elevation=None,
        dist=None,
    ):
        super(C2W_Camera, self).__init__()
        FoVx = focal2fov(fov2focal(FoVy, height), width)
        # FoVx = focal2fov(fov2focal(FoVy, width), height)

        R = c2w[:3, :3]
        T = c2w[:3, 3]

        self.R = R.float()
        self.T = T.float()
        self.FoVx = FoVx
        self.FoVy = FoVy
        self.image_height = height
        self.image_width = width

        try:
            self.data_device = torch.device(data_device)
        except Exception as e:
            print(e)
            print(
                f"[Warning] Custom device {data_device} failed, fallback to default cuda device"
            )
            self.data_device = torch.device("cuda")

        self.azimuth = azimuth
        self.elevation = elevation
        self.dist = dist
        self.zfar = 100.0
        self.znear = 0.01

        self.trans = trans.float()
        self.scale = scale

        self.world_view_transform = (
            getWorld2View2_tensor(R, T).transpose(0, 1).float().cuda()
        )
        self.projection_matrix = (
            getProjectionMatrix(
                znear=self.znear, zfar=self.zfar, fovX=self.FoVx, fovY=self.FoVy
            )
            .transpose(0, 1)
            .float()
            .cuda()
        )
        self.full_proj_transform = (
            (
                self.world_view_transform.unsqueeze(0).bmm(
                    self.projection_matrix.unsqueeze(0)
                )
            )
            .squeeze(0)
            .float()
        )
        self.camera_center = self.world_view_transform.inverse()[3, :3].float()
        # print('self.camera_center',self.camera_center)


class MiniCam:
    def __init__(
        self,
        width,
        height,
        FoVy,
        FoVx,
        znear,
        zfar,
        world_view_transform,
        full_proj_transform,
    ):
        self.image_width = width
        self.image_height = height
        self.FoVy = FoVy
        self.FoVx = FoVx
        self.znear = znear
        self.zfar = zfar
        self.world_view_transform = world_view_transform
        self.full_proj_transform = full_proj_transform
        view_inv = torch.inverse(self.world_view_transform.transpose(0, 1))
        self.camera_center = view_inv[:3, 3]
        self.R = np.array(view_inv[:3, :3].transpose(0, 1).clone().detach().cpu())
        self.T = np.array(self.camera_center.clone().detach().cpu())

    @classmethod
    def from_cam_params(
        cls,
        translation,
        wxyz,
        scale,
        FoVx,
        FoVy,
        height,
        width,
        znear=0.01,
        zfar=100.0,
    ):
        c2w = get_c2w_from_wxyz_trans(wxyz, translation)
        return cls.from_c2w(c2w, FoVx, FoVy, height, width, znear, zfar)

    @classmethod
    def from_c2w(cls, c2w, FoVx, FoVy, height, width, znear=0.01, zfar=100.0):
        w2c = np.linalg.inv(c2w)

        # R is stored transposed due to 'glm' in CUDA code
        R = w2c[:3, :3].transpose()
        T = w2c[:3, 3]

        world_view_transform = (
            torch.tensor(getWorld2View2(R, T)).transpose(0, 1).float().cuda()
        )
        projection_matrix = (
            getProjectionMatrix(znear=znear, zfar=zfar, fovX=FoVx, fovY=FoVy)
            .transpose(0, 1)
            .float()
            .cuda()
        )
        full_proj_transform = (
            world_view_transform.unsqueeze(0)
            .bmm(projection_matrix.unsqueeze(0))
            .squeeze(0)
            .float()
        )
        return cls(
            width,
            height,
            FoVy,
            FoVx,
            znear,
            zfar,
            world_view_transform,
            full_proj_transform,
        )
