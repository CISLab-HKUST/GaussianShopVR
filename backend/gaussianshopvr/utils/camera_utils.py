import math
from types import SimpleNamespace
from viser import transforms as tf
import numpy as np
from gaussianshopvr.core.cameras import MiniCam, Camera
from gaussianshopvr.core.cameras import Camera
from gaussianshopvr.utils.general_utils import PILtoTorch
from gaussianshopvr.utils.graphics_utils import fov2focal


def cam_viser2colmap(viser_cam, img_size, FoV_scale=1):
    # W2C = tf.SE3.from_rotation_and_translation(
    #     tf.SO3(viser_cam.wxyz), viser_cam.position
    # ).inverse()
    # R = W2C.rotation().as_matrix().transpose()
    # T = W2C.translation()
    FoVy = viser_cam.fov * FoV_scale
    FoVx = 2 * math.atan(math.tan(FoVy / 2) * viser_cam.aspect)
    return MiniCam.from_cam_params(
        viser_cam.position,
        viser_cam.wxyz,
        1,
        FoVx,
        FoVy,
        height=img_size[0],
        width=img_size[1],
    )
    # null_image = torch.ones((3, *img_size))
    # return Camera(0, R, T, FoVx, FoVy, null_image, None, "", 0)


def cam_colmap2viser(colmap_cam):
    C2W = tf.SE3.from_rotation_and_translation(
        tf.SO3.from_matrix(np.transpose(colmap_cam.R)), colmap_cam.T
    ).inverse()
    wxyz = C2W.rotation().wxyz
    position = C2W.translation()
    return SimpleNamespace(wxyz=wxyz, position=position)


def loadCam(args, id, cam_info, resolution_scale):
    orig_w, orig_h = cam_info.image.size

    if args.resolution in [1, 2, 4, 8]:
        resolution = round(orig_w / (resolution_scale * args.resolution)), round(
            orig_h / (resolution_scale * args.resolution)
        )
    else:  # should be a type that converts to float
        if args.resolution == -1:
            if orig_w > 1600:
                global WARNED
                if not WARNED:
                    print(
                        "[ INFO ] Encountered quite large input images (>1.6K pixels width), rescaling to 1.6K.\n "
                        "If this is not desired, please explicitly specify '--resolution/-r' as 1"
                    )
                    WARNED = True
                global_down = orig_w / 1600
            else:
                global_down = 1
        else:
            global_down = orig_w / args.resolution

        scale = float(global_down) * float(resolution_scale)
        resolution = (int(orig_w / scale), int(orig_h / scale))

    resized_image_rgb = PILtoTorch(cam_info.image, resolution)

    gt_image = resized_image_rgb[:3, ...]
    loaded_mask = None

    if resized_image_rgb.shape[1] == 4:
        loaded_mask = resized_image_rgb[3:4, ...]

    return Camera(
        colmap_id=cam_info.uid,
        R=cam_info.R,
        T=cam_info.T,
        FoVx=cam_info.FovX,
        FoVy=cam_info.FovY,
        image=gt_image,
        gt_alpha_mask=loaded_mask,
        image_name=cam_info.image_name,
        uid=id,
        data_device=args.data_device,
    )


def cameraList_from_camInfos(cam_infos, resolution_scale, args):
    camera_list = []

    for id, c in enumerate(cam_infos):
        camera_list.append(loadCam(args, id, c, resolution_scale))

    return camera_list


def camera_to_JSON(id, camera: Camera):
    Rt = np.zeros((4, 4))
    Rt[:3, :3] = camera.R.transpose()
    Rt[:3, 3] = camera.T
    Rt[3, 3] = 1.0

    W2C = np.linalg.inv(Rt)
    pos = W2C[:3, 3]
    rot = W2C[:3, :3]
    serializable_array_2d = [x.tolist() for x in rot]
    camera_entry = {
        "id": id,
        "img_name": camera.image_name,
        "width": camera.width,
        "height": camera.height,
        "position": pos.tolist(),
        "rotation": serializable_array_2d,
        "fy": fov2focal(camera.FovY, camera.height),
        "fx": fov2focal(camera.FovX, camera.width),
    }
    return camera_entry
