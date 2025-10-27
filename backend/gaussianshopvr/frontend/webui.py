import sys
import os
import time
import random
import math
import asyncio
from types import SimpleNamespace

from argparse import ArgumentParser
import torchvision.transforms.functional
import viser
import viser.transforms as tf
import numpy as np
import torch
import torchvision
from PIL import Image

from gaussianshopvr.core.gaussian_shop import gaussianshopvr
from gaussianshopvr.utils.camera_utils import cam_viser2colmap, cam_colmap2viser
from gaussianshopvr.utils.image_utils import cvt_tensor_img


class WebUI:
    def __init__(self, cfg, gsshop=None) -> None:
        if gsshop is not None:
            self.gsshop = gsshop
        else:
            self.gsshop = gaussianshopvr(gs_source=cfg.gs_source, cam_dir=cfg.cam_dir)
        # self.gs_source = cfg.gs_source
        # self.cam_dir = cfg.cam_dir
        self.port = 8088
        self.server = viser.ViserServer(port=self.port)
        self.status = True

        self.resolution_limit = 512

        # self.viser_camera = SimpleNamespace(
        #     wxyz=np.array([-0.1759, 0.3398, 0.8204, -0.4247]),
        #     position=np.array([3, 3, 3]),
        #     fov=(
        #         self.gsshop.colmap_cameras[0].FoVy
        #         if len(self.gsshop.colmap_cameras)
        #         else 1
        #     ),
        #     look_at=np.zeros(3),
        #     up_direction=np.array([0, 0, 1]),
        # )

        self.camera_bookmarks = []

        self.add_gui_render_setting()
        self.add_gui_gshop_setting()
        self.add_gui_bookmarks()

        for idx, cam in enumerate(self.gsshop.colmap_cameras):
            self.add_one_camera_frame(cam_colmap2viser(cam), f"colmap/{idx}")

        @self.server.on_client_connect
        def _(client: viser.ClientHandle) -> None:
            client.camera.fov = (
                self.gsshop.colmap_cameras[0].FoVy
                if len(self.gsshop.colmap_cameras)
                else 1
            )
            client.camera.position = (0, -2, 0)
            client.camera.up_direction = (0, 0, 1)
            client.camera.look_at = np.zeros(3)

            # # print(client.camera.wxyz, client.camera.position, client.camera.look_at, client.camera.up_direction)
            # client.camera.wxyz = self.viser_camera.wxyz
            # client.camera.position = self.viser_camera.position
            # client.camera.fov = self.viser_camera.fov
            # client.camera.look_at = self.viser_camera.look_at
            # client.camera.up_direction = self.viser_camera.up_direction

            # @client.camera.on_update
            # def _(camera: viser.CameraHandle) -> None:
            #     self.viser_camera.wxyz = camera.wxyz
            #     self.viser_camera.position = camera.position
            #     self.viser_camera.fov = camera.fov
            #     self.viser_camera.look_at = camera.look_at
            #     self.viser_camera.up_direction = camera.up_direction

            #     for other_client in self.server.get_clients().values():
            #         if client.client_id != other_client.client_id:
            #             with other_client.atomic():
            #                 print(client.client_id, "->", other_client.client_id)
            #                 other_client.camera.wxyz = self.viser_camera.wxyz
            #                 other_client.camera.position = self.viser_camera.position
            #                 other_client.camera.fov = self.viser_camera.fov
            #                 other_client.camera.look_at = self.viser_camera.look_at
            #                 other_client.camera.up_direction = (
            #                     self.viser_camera.up_direction
            #                 )

        asyncio.ensure_future(self.render_loop())

    @property
    def client(self):
        clients = list(self.server.get_clients().items())
        if len(clients):
            return clients[0][1]
        else:
            return None

    @property
    def resolution(self):
        if self.client:
            return (
                self.resolution_limit,
                int(self.resolution_limit * self.client.camera.aspect),
            )
        else:
            return (self.resolution_limit, self.resolution_limit)

    # Camera setting
    @property
    def camera(self):
        if not self.client:
            return None
        else:
            return cam_viser2colmap(
                self.client.camera, self.resolution, self.FoV_slider.value
            )

    @camera.setter
    def camera(self, new_cam):
        self.camera_goto(self.client, new_cam, animation=False)

    async def start(self):
        assert not self.status, "Already rendering!"
        self.status = True

    def stop(self):
        assert self.status, "Already stop rendering!"
        self.status = False

    def camera_goto(self, client, target_camera, animation=True):
        T_world_current = tf.SE3.from_rotation_and_translation(
            tf.SO3(client.camera.wxyz), client.camera.position
        )
        T_world_target = tf.SE3.from_rotation_and_translation(
            tf.SO3(target_camera.wxyz), target_camera.position
        )
        T_world_target_look_at = T_world_target @ tf.SE3.from_translation(
            np.array([0.0, 0.0, 0.5])
        )
        T_current_target = T_world_current.inverse() @ T_world_target

        if animation:
            for j in range(20):
                T_world_set = T_world_current @ tf.SE3.exp(
                    T_current_target.log() * j / 19
                )

                with client.atomic():
                    client.camera.wxyz = T_world_set.rotation().wxyz
                    client.camera.position = T_world_set.translation()

                client.flush()
                time.sleep(0.3 / 20)
        else:
            with client.atomic():
                client.camera.wxyz = T_world_target.rotation().wxyz
                client.camera.position = T_world_target.translation()
            client.flush()

        # Mouse interactions should orbit around the target_camera origin.
        client.camera.look_at = T_world_target_look_at.translation()

    def add_one_camera_frame(self, camera, label):
        wxyz = camera.wxyz
        position = camera.position

        # breakpoint()
        frame = self.server.add_frame(
            label,
            wxyz=wxyz,
            position=position,
            visible=False,
        )

        @frame.on_click
        def _(event: viser.GuiEvent):
            self.camera_goto(event.client, frame)

    @torch.no_grad
    async def render_loop(self):
        while True:
            if self.status:
                out_img = self.prepare_output()
                if out_img is not None:
                    self.server.set_background_image(out_img, format="png")
                    self.server.flush()
            await asyncio.sleep(1.0 / 24)

    @torch.no_grad
    def prepare_output(self):
        if not self.camera:
            return None
        output = self.gsshop.render(self.camera)
        out_key = self.renderer_output.value

        if out_key == "rgb":
            if self.show_3D_mask.value:
                out_img = cvt_tensor_img(output["semantic"].clamp(0, 1))
            else:
                out_img = cvt_tensor_img(output["render"].clamp(0, 1))
        if out_key == "depth":
            out_img = cvt_tensor_img(output["depth"], True)
        if out_key == "alpha":
            out_img = cvt_tensor_img(output["alpha"], True)
        if out_key == "masks":
            out_img = cvt_tensor_img(output["masks"].float())
        if out_key == "semantic":
            out_img = cvt_tensor_img(output["semantic"].float())
        # print(out_img, np.array(out_img).max())
        # out_img.save(f"{out_key}.png")
        # if self.sam_enabled.value:
        #     if "sam_masks" in output and len(output["sam_masks"]) > 0:
        #         try:
        #             out_img = torchvision.utils.draw_segmentation_masks(
        #                 out_img, output["sam_masks"][0]
        #             )

        #             out_img = torchvision.utils.draw_keypoints(
        #                 out_img,
        #                 output["point2ds"][0][None, ...],
        #                 colors="blue",
        #                 radius=5,
        #             )
        #         except Exception as e:
        #             print(e)

        # if (
        #     self.draw_bbox.value
        #     and self.draw_flag
        #     and (self.left_up.value[0] < self.right_down.value[0])
        #     and (self.left_up.value[1] < self.right_down.value[1])
        # ):
        #     out_img[
        #         :,
        #         self.left_up.value[1] : self.right_down.value[1],
        #         self.left_up.value[0] : self.right_down.value[0],
        #     ] = 0
        # torchvision.transforms.functional.to_pil_image(out_img).save("glitch.png")
        return np.array(out_img.convert("RGB"))

    #  GUI setting
    def add_gui_render_setting(self):
        with self.server.add_gui_folder("Render Setting"):
            self.resolution_slider = self.server.add_gui_slider(
                "Resolution",
                min=384,
                max=4096,
                step=2,
                initial_value=self.resolution_limit,
            )

            @self.resolution_slider.on_update
            def _(_):
                self.resolution_limit = self.resolution_slider.value

            self.FoV_slider = self.server.add_gui_slider(
                "FoV Scaler", min=0.2, max=2, step=0.1, initial_value=1
            )
            self.renderer_output = self.server.add_gui_dropdown(
                "Renderer Output",
                ["rgb", "depth", "alpha", "masks", "semantic"],
            )
            self.save_button = self.server.add_gui_button("Save Gaussian")

    def add_gui_gshop_setting(self):
        with self.server.add_gui_folder("4DN"):
            self.unproject_mask_btn = self.server.add_gui_button(
                "Unproject Mask",
            )
            self.unproject_mask_mv = self.server.add_gui_button(
                "Unproject Mask Multiview",
            )
            self.show_3D_mask = self.server.add_gui_checkbox(
                "Show 3D Mask", initial_value=False
            )
            self.remove_masked_btn = self.server.add_gui_button(
                "Remove Masked Splats",
            )
            self.mark_conv_hull_btn = self.server.add_gui_button(
                "Mark Points in Convex Hull"
            )
            self.mask_thres_num = self.server.add_gui_number(
                "Mask Threshold", min=0.2, max=0.99999, step=0.00001, initial_value=0.9
            )
            self.gs_optimization_btn = self.server.add_gui_button("Start Optimization")

            @self.unproject_mask_btn.on_click
            def _(_):
                self.camera_goto(
                    self.client,
                    cam_colmap2viser(self.gsshop.colmap_cameras[71]),
                    animation=False,
                )
                mask = Image.open("./diff_mask.png")
                mask = torchvision.transforms.ToTensor()(mask)
                pad = (
                    self.resolution[0] - mask.shape[1],
                    self.resolution[1] - mask.shape[2],
                )
                mask = torch.nn.functional.pad(
                    mask,
                    (
                        pad[1] // 2,
                        pad[1] - pad[1] // 2,
                        pad[0] // 2,
                        pad[0] - pad[0] // 2,
                    ),
                )
                torchvision.transforms.functional.to_pil_image(mask).save("mask1.png")
                output = self.gsshop.render(self.camera)["render"]  # H W C
                out_img = output.clamp(0, 1)
                out_img = out_img.cpu().moveaxis(-1, 0)  # C H W
                # torchvision.transforms.functional.to_pil_image(out_img).save(
                #     "render.png"
                # )
                # torchvision.transforms.functional.to_pil_image(
                #     0.5 * mask + 0.5 * out_img
                # ).save("blend.png")
                self.gsshop.unproject_mask2d(self.camera, mask)
                self.gsshop.update_mask_with_threshold(self.mask_thres_num.value)

            @self.gs_optimization_btn.on_click
            def _(_):
                self.camera_goto(
                    self.client,
                    cam_colmap2viser(self.gsshop.colmap_cameras[71]),
                    animation=False,
                )
                mask = Image.open("./2.png")
                mask = torchvision.transforms.ToTensor()(mask)[:3, :, :]
                pad = (
                    self.resolution[0] - mask.shape[1],
                    self.resolution[1] - mask.shape[2],
                )
                mask = torch.nn.functional.pad(
                    mask,
                    (
                        pad[1] // 2,
                        pad[1] - pad[1] // 2,
                        pad[0] // 2,
                        pad[0] - pad[0] // 2,
                    ),
                )
                output = self.gsshop.render(self.camera)["rgb"].cpu()  # C H W
                out_img = output.clamp(0, 1)
                out_img = out_img.cpu().moveaxis(-1, 0)  # C H W

                self.gsshop.single_view_train(self.camera, mask.cuda())

            # @self.unproject_mask_mv.on_click
            # def _(_):
            #     cur_cam = self.colmap_cameras[71]
            #     mask = Image.open("./output.png").convert("L")
            #     mask = torchvision.transforms.ToTensor()(mask)
            #     mask = (
            #         torchvision.transforms.Resize((512, 512))(mask)
            #         .to(get_device())
            #         .squeeze()
            #     )
            #     depth = render(
            #         cur_cam, self.gaussian, self.pipe, self.background_tensor
            #     )["depth_3dgs"]
            #     # Find coordinates of white points
            #     white_points = torch.nonzero(mask)
            #     print(white_points.shape)
            #     # Calculate the centroid
            #     points2d = torch.mean(white_points.float(), dim=0) / 512

            #     points3d = []
            #     unprojected_points3d = unproject(cur_cam, points2d, depth)
            #     points3d += unprojected_points3d.unbind(0)

            #     # SAM
            #     masks = []
            #     weights = torch.zeros_like(self.gaussian._opacity)
            #     weights_cnt = torch.zeros_like(
            #         self.gaussian._opacity, dtype=torch.int32
            #     )

            #     total_view_num = len(self.colmap_cameras)
            #     random.seed(0)  # make sure same views
            #     view_index = random.sample(
            #         range(0, total_view_num),
            #         min(total_view_num, self.seg_cam_num.value),
            #     )

            #     os.makedirs("tmp", exist_ok=True)

            #     for idx in tqdm(view_index):
            #         cur_cam = self.colmap_cameras[idx]
            #         assert len(points3d) > 0
            #         points2ds = project(cur_cam, points3d)
            #         img = render(
            #             cur_cam, self.gaussian, self.pipe, self.background_tensor
            #         )["render"]
            #         to_pil_image(img.cpu()).save(f"./tmp/img_{idx}.jpg")

            #         self.sam_predictor.set_image(
            #             np.asarray(to_pil_image(img.cpu())),
            #         )
            #         self.sam_features[idx] = self.sam_predictor.features
            #         # print(points2ds)
            #         mask, _, _ = self.sam_predictor.predict(
            #             point_coords=points2ds.cpu().numpy(),
            #             point_labels=np.array([1] * points2ds.shape[0], dtype=np.int64),
            #             box=None,
            #             multimask_output=False,
            #         )
            #         mask = torch.from_numpy(mask).to(torch.bool).to(get_device())
            #         self.gaussian.apply_weights(
            #             cur_cam, weights, weights_cnt, mask.to(torch.float32)
            #         )
            #         masks.append(mask)
            #         mask = mask.cpu().numpy()[0]
            #         img = Image.fromarray(mask)
            #         img.save(f"./tmp/mask_{idx}.jpg")

            #     weights /= weights_cnt + 1e-7

            #     semantic_gaussian_mask = (weights > 0.5)[:, 0]
            #     self.gaussian.set_mask(semantic_gaussian_mask)
            #     self.gaussian.apply_grad_mask(semantic_gaussian_mask)

            @self.mark_conv_hull_btn.on_click
            def _(_):
                mask, dilated_mask = self.gsshop.get_convex_hull()
                self.gsshop.set_mask(mask)
                self.gsshop.remove_masked_splats()
                self.gsshop.set_mask(dilated_mask[~mask])

            @self.remove_masked_btn.on_click
            def _(_):
                self.gsshop.remove_masked_splats()

            @self.mask_thres_num.on_update
            def _(_):
                self.gsshop.update_mask_with_threshold(self.mask_thres_num.value)

    def add_gui_bookmarks(self):
        with self.server.add_gui_folder("Camera"):
            bookmark_dropdown = self.server.add_gui_dropdown(
                "Camera bookmarks", ["None"], disabled=True
            )

            def update_bookmark_dropdown():
                if len(self.camera_bookmarks):
                    bookmark_dropdown.options = [
                        f"{_+1}" for _ in range(len(self.camera_bookmarks))
                    ]
                    bookmark_dropdown.disabled = False
                else:
                    bookmark_dropdown.options = ["None"]
                    bookmark_dropdown.disabled = True

            bookmark_goto_button = self.server.add_gui_button("Go to")
            bookmark_add_button = self.server.add_gui_button("Add")
            bookmark_del_button = self.server.add_gui_button("Delete")

            @bookmark_dropdown.on_update
            def _(_):
                if not bookmark_dropdown.value:
                    bookmark_del_button.disabled = True

            @bookmark_add_button.on_click
            def _(e):
                cur_cam = SimpleNamespace(
                    wxyz=e.client.camera.wxyz, position=e.client.camera.position
                )
                self.camera_bookmarks.append(cur_cam)
                update_bookmark_dropdown()

            @bookmark_del_button.on_click
            def _(e):
                self.camera_bookmarks.pop(int(bookmark_dropdown.value) - 1)
                update_bookmark_dropdown()

            @bookmark_goto_button.on_click
            def _(e):
                self.camera_goto(
                    e.client,
                    target_camera=self.camera_bookmarks[
                        int(bookmark_dropdown.value) - 1
                    ],
                )


if __name__ == "__main__":
    parser = ArgumentParser()
    parser.add_argument("--gs_source", type=str, required=True)
    parser.add_argument("--cam_dir", type=str, required=True)
    args = parser.parse_args()
    webui = WebUI(args)
    webui.render_loop()
