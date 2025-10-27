import pytorch3d
import torch
import math
import numpy as np
from typing import NamedTuple
from scipy.spatial.transform import Rotation, Slerp


def get_c2w_from_wxyz_trans(wxyz, trans):
    R = pytorch3d.transforms.quaternion_to_matrix(torch.tensor(wxyz))
    c2w = torch.zeros((4, 4))
    c2w[:3, :3] = R
    c2w[:3, 3] = torch.tensor(trans)
    c2w[3, 3] = 1.0
    return c2w.numpy()


def get_extent(xyz):
    center = torch.mean(xyz, dim=0, keepdim=True)
    dist = torch.norm(xyz - center, dim=1, keepdim=True)
    diagonal = torch.max(dist).item()
    return diagonal


def world2screen(points_xyz, cam):
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

    return points_xy, visible_mask


def decompose_matrix(matrix):
    # Ensure the matrix is of shape [4, 4]
    assert matrix.shape == (4, 4), "Matrix must be a 4x4 transformation matrix."

    # Normalize the matrix if [3, 3] is not 1
    if matrix[3, 3] != 1:
        matrix = matrix / matrix[3, 3]

    # Extract translation (the last column)
    translation = matrix[:3, 3]

    # Extract the 3x3 rotation-scale matrix
    rotation_scale_matrix = matrix[:3, :3]

    # Compute scale (the norm of each row of the 3x3 matrix)
    scale = torch.norm(rotation_scale_matrix, dim=1)

    # Normalize the rotation matrix to remove scale from it
    rotation_matrix = rotation_scale_matrix / scale.view(3, 1)

    return scale, rotation_matrix, translation


class BasicPointCloud(NamedTuple):
    points: np.array
    colors: np.array
    normals: np.array


def geom_transform_points(points, transf_matrix):
    P, _ = points.shape
    ones = torch.ones(P, 1, dtype=points.dtype, device=points.device)
    points_hom = torch.cat([points, ones], dim=1)
    points_out = torch.matmul(points_hom, transf_matrix.unsqueeze(0))

    denom = points_out[..., 3:] + 0.0000001
    return (points_out[..., :3] / denom).squeeze(dim=0)


def getWorld2View(R, t):
    Rt = np.zeros((4, 4))
    Rt[:3, :3] = R.transpose()
    Rt[:3, 3] = t
    Rt[3, 3] = 1.0
    return np.float32(Rt)


def getWorld2View2(R, t, translate=np.array([0.0, 0.0, 0.0]), scale=1.0):
    Rt = np.zeros((4, 4))
    Rt[:3, :3] = R.transpose()
    Rt[:3, 3] = t
    Rt[3, 3] = 1.0

    C2W = np.linalg.inv(Rt)
    cam_center = C2W[:3, 3]
    cam_center = (cam_center + translate) * scale
    C2W[:3, 3] = cam_center
    Rt = np.linalg.inv(C2W)
    return np.float32(Rt)


def getWorld2View2_tensor(R, t, translate=torch.tensor([0.0, 0.0, 0.0]), scale=1.0):
    Rt = torch.zeros((4, 4))
    Rt[:3, :3] = R.transpose(0, 1)
    Rt[:3, 3] = t
    Rt[3, 3] = 1.0

    C2W = torch.linalg.inv(Rt)
    cam_center = C2W[:3, 3]
    cam_center = (cam_center + translate) * scale
    C2W[:3, 3] = cam_center
    Rt = torch.linalg.inv(C2W)
    return Rt.float()


def getProjectionMatrix(znear, zfar, fovX, fovY):
    tanHalfFovY = math.tan((fovY / 2))
    tanHalfFovX = math.tan((fovX / 2))

    top = tanHalfFovY * znear
    bottom = -top
    right = tanHalfFovX * znear
    left = -right

    P = torch.zeros(4, 4, dtype=torch.float32)

    z_sign = 1.0

    P[0, 0] = 2.0 * znear / (right - left)
    P[1, 1] = 2.0 * znear / (top - bottom)
    P[0, 2] = (right + left) / (right - left)
    P[1, 2] = (top + bottom) / (top - bottom)
    P[3, 2] = z_sign
    P[2, 2] = z_sign * zfar / (zfar - znear)
    P[2, 3] = -(zfar * znear) / (zfar - znear)
    return P


def fov2focal(fov, pixels):
    return pixels / (2 * math.tan(fov / 2))


def focal2fov(focal, pixels):
    return 2 * math.atan(pixels / (2 * focal))


def interpolate_extrinsics(E1, E2, alpha):
    # Extract rotations and translations
    R1, t1 = E1[:3, :3], E1[:3, 3]
    R2, t2 = E2[:3, :3], E2[:3, 3]

    # Convert to Rotation objects
    rot1 = Rotation.from_matrix(R1)
    rot2 = Rotation.from_matrix(R2)

    # Define key times and rotations
    key_times = [0, 1]
    key_rots = Rotation.concatenate([rot1, rot2])

    # Build a Slerp interpolator
    slerp = Slerp(key_times, key_rots)

    # Evaluate at alpha
    R_interp = slerp([alpha]).as_matrix()[0]

    # Linear for translations
    t_interp = (1 - alpha) * t1 + alpha * t2

    # Reassemble extrinsic
    E_interp = np.eye(4)
    E_interp[:3, :3] = R_interp
    E_interp[:3, 3] = t_interp
    return E_interp