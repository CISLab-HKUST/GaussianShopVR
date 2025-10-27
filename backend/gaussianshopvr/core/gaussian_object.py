import os
from datetime import datetime

import torch
from torch import nn
from argparse import ArgumentParser
from types import SimpleNamespace
from scipy.spatial import Delaunay
import pytorch3d

from gaussianshopvr.core.gaussian_model import SimpleGaussianModel, GaussianModel
from gaussianshopvr.utils.loss_utils import l1_loss, ssim
from gaussianshopvr.core.arguments import (
    ModelParams,
    PipelineParams,
    OptimizationParams,
)
from gaussianshopvr.core import gaussian_renderer
from gaussianshopvr.core.cameras import Camera
from gaussianshopvr.utils.graphics_utils import BasicPointCloud
from gaussianshopvr.utils.image_utils import tensor_save_img
from gaussianshopvr.utils.graphics_utils import get_extent


class GSObject:
    def __init__(
        self,
        id,
        parent=None,
        translation=[0, 0, 0],
        wxyz=[1, 0, 0, 0],
        scale=[1, 1, 1],
        is_leaf=False,
    ) -> None:
        self.id = int(id)
        self.parent = parent
        self.translation = translation
        self.wxyz = wxyz
        self.scale = scale
        self.is_leaf = is_leaf

    @property
    def info(self):
        # convert to Unity coordinate system
        translation = self.translation
        translation[1] = -translation[1]
        wxyz = self.wxyz
        wxyz[1] = -wxyz[1]
        wxyz[3] = -wxyz[3]
        return {
            "id": self.id,
            "parent": self.parent.id if self.parent else None,
            "scale": self.scale,
            "wxyz": wxyz,
            "translation": translation,
            "is_leaf": self.is_leaf,
        }

    # Scale transform
    @property
    def ts(self):
        return pytorch3d.transforms.Scale(*self.scale).cuda()

    # Rotation transform
    @property
    def tr(self):
        # pytorch3d uses row major ordering, but quaterion_to_matrix get col major matrix
        return pytorch3d.transforms.Rotate(
            pytorch3d.transforms.quaternion_to_matrix(
                torch.tensor(self.wxyz)
            ).transpose(-1, -2)
        ).cuda()

    # Translation transform
    @property
    def tt(self):
        return pytorch3d.transforms.Translate(*self.translation).cuda()

    # Transform, in the order of scale->rotation->translation
    @property
    def transform(self):
        return self.ts.compose(self.tr, self.tt)

    @property
    def world_transform(self):
        transform = self.transform
        pt = self.parent
        while pt != None:
            transform = transform.compose(pt.transform)
            pt = pt.parent
        return transform


class ContainerObject(GSObject):
    def __init__(
        self,
        id,
        gs=None,
        parent=None,
        translation=[0, 0, 0],
        wxyz=[1, 0, 0, 0],
        scale=[1, 1, 1],
        is_leaf=False,
    ):
        super().__init__(
            id=id,
            parent=parent,
            translation=translation,
            wxyz=wxyz,
            scale=scale,
            is_leaf=is_leaf,
        )
        self.children: list[ContainerObject | GaussianObject] = []
        self.cached = False
        self.cached_gaussian = None

        parser = ArgumentParser(description="ContainerObject parameters")
        self.pipe = PipelineParams(parser)
        self.opt = OptimizationParams(parser)
        self.background_tensor = torch.tensor(
            [1, 1, 1], dtype=torch.float32, device="cuda"
        )

    def __del__(self):
        print(f"Container Object {self.id} is deleted.")

    @property
    def gaussian(self):
        if not self.cached:
            new_gaussian = SimpleGaussianModel()
            if len(self.children) != 0:
                new_gaussian._xyz = torch.cat(
                    [
                        child.transform.transform_points(child.gaussian._xyz)
                        for child in self.children
                    ]
                )
                new_gaussian._scaling = torch.cat(
                    [
                        child.gaussian._scaling
                        + torch.log(torch.tensor(child.scale).cuda())
                        for child in self.children
                    ]
                )
                new_gaussian._rotation = torch.cat(
                    [
                        pytorch3d.transforms.quaternion_multiply(
                            torch.tensor(child.wxyz).cuda(),
                            child.gaussian._rotation,
                        )
                        for child in self.children
                    ]
                )
                new_gaussian._features_dc = torch.cat(
                    [child.gaussian._features_dc for child in self.children]
                )
                new_gaussian._features_rest = torch.cat(
                    [child.gaussian._features_rest for child in self.children]
                )
                new_gaussian._opacity = torch.cat(
                    [child.gaussian._opacity for child in self.children]
                )
                new_gaussian._id = torch.cat(
                    [
                        torch.fill(torch.ones(child.gaussian.points_num), child.id)
                        for child in self.children
                    ]
                )
            new_gaussian.mask = None
            self.cached_gaussian = new_gaussian
            self.cached = True

        return self.cached_gaussian

    @torch.no_grad
    def render(self, cam, override_color=None) -> dict[str, any]:
        render_pkg = gaussian_renderer.render(
            cam,
            self.gaussian,
            self.pipe,
            self.background_tensor,
            override_color=override_color,
        )
        image, viewspace_point_tensor, _, radii = (
            render_pkg["render"],
            render_pkg["viewspace_points"],
            render_pkg["visibility_filter"],
            render_pkg["radii"],
        )

        if self.gaussian.mask is not None:
            print(self.gaussian.mask.sum())
            semantic_map = gaussian_renderer.render(
                cam,
                self.gaussian,
                self.pipe,
                self.background_tensor,
                # override_color=torch.zeros(self.gaussian.points_num)[..., None].float().repeat(1, 3)
                override_color=self.gaussian.mask[..., None].float().repeat(1, 3),
            )["render"]
        else:
            semantic_map = image
        semantic_map = torch.norm(semantic_map, dim=0)
        semantic_map = semantic_map > 0.0  # 1, H, W
        semantic_map_viz = image.detach().clone()  # C, H, W

        semantic_map_viz = semantic_map_viz.permute(1, 2, 0)
        semantic_map_viz[semantic_map] = 0.50 * semantic_map_viz[
            semantic_map
        ] + 0.50 * torch.tensor([1.0, 0.0, 0.0], device="cuda")
        semantic_map_viz = semantic_map_viz.permute(2, 0, 1)
        semantic_map = semantic_map.unsqueeze(0)

        # render_pkg["sam_masks"] = []
        # render_pkg["point2ds"] = []
        # if sam:
        #     if hasattr(self, "points3d") and len(self.points3d) > 0:
        #         sam_output = self.sam_predict(image, cam)
        #         if sam_output is not None:
        #             render_pkg["sam_masks"].append(sam_output[0])
        #             render_pkg["point2ds"].append(sam_output[1])

        # self.gaussian.localize = False  # reverse

        render_pkg["semantic"] = semantic_map_viz  # C, H, W
        render_pkg["masks"] = semantic_map  # 1, H, W

        # depth = render_pkg["depth"]
        # tensor_save_img(depth, "depth.png")
        # tensor_save_img(render_pkg["alpha"], "alpha.png")

        return {
            **render_pkg,
        }


class GaussianObject(GSObject):
    def __init__(
        self,
        id,
        gs=None,
        parent=None,
        translation=[0, 0, 0],
        wxyz=[1, 0, 0, 0],
        scale=[1, 1, 1],
        is_leaf=True,
        **args,
    ):
        super().__init__(
            id=id,
            parent=parent,
            translation=translation,
            wxyz=wxyz,
            scale=scale,
            is_leaf=is_leaf,
        )

        parser = ArgumentParser(description="GaussianObject parameters")
        self.pipe = PipelineParams(parser)
        self.opt = OptimizationParams(parser)

        if isinstance(gs, GaussianModel):
            self.gaussian = gs
            self.gaussian._id = torch.full(
                (self.gaussian.points_num,), self.id, dtype=torch.int
            ).cuda()
        elif isinstance(gs, str):
            self.gaussian = GaussianModel()
            self.gaussian.load_ply(gs)
            self.gaussian._id = torch.full(
                (self.gaussian.points_num,), self.id, dtype=torch.int
            ).cuda()
        else:
            raise ValueError("Should be GS or file path of GS")

        self.gaussian.max_radii2D = torch.zeros(
            (self.gaussian.get_xyz.shape[0]), device="cuda"
        )
        self.background_tensor = torch.tensor(
            [1, 1, 1], dtype=torch.float32, device="cuda"
        )
        self.gaussian.spatial_lr_scale = get_extent(self.gaussian._xyz) * 1.5
        self.gaussian.mask = torch.zeros(
            self.gaussian.points_num, dtype=torch.bool
        ).cuda()
        self.configure_optimizers()

    def __del__(self):
        print(f"Gaussian Object {self.id} is deleted.")

    @torch.no_grad
    def render(self, cam, override_color) -> dict[str, any]:
        render_pkg = gaussian_renderer.render(
            cam, self.gaussian, self.pipe, self.background_tensor
        )
        image, viewspace_point_tensor, _, radii = (
            render_pkg["render"],
            render_pkg["viewspace_points"],
            render_pkg["visibility_filter"],
            render_pkg["radii"],
        )

        semantic_map = gaussian_renderer.render(
            cam,
            self.gaussian,
            self.pipe,
            self.background_tensor,
            # override_color=torch.zeros(self.gaussian.points_num)[..., None].float().repeat(1, 3)
            override_color=self.gaussian.mask[..., None].float().repeat(1, 3),
        )["render"]
        # semantic_map = image
        semantic_map = torch.norm(semantic_map, dim=0)
        semantic_map = semantic_map > 0.0  # 1, H, W
        semantic_map_viz = image.detach().clone()  # C, H, W

        semantic_map_viz = semantic_map_viz.permute(1, 2, 0)
        semantic_map_viz[semantic_map] = 0.50 * semantic_map_viz[
            semantic_map
        ] + 0.50 * torch.tensor([1.0, 0.0, 0.0], device="cuda")
        semantic_map_viz = semantic_map_viz.permute(2, 0, 1)
        semantic_map = semantic_map.unsqueeze(0)

        # render_pkg["sam_masks"] = []
        # render_pkg["point2ds"] = []
        # if sam:
        #     if hasattr(self, "points3d") and len(self.points3d) > 0:
        #         sam_output = self.sam_predict(image, cam)
        #         if sam_output is not None:
        #             render_pkg["sam_masks"].append(sam_output[0])
        #             render_pkg["point2ds"].append(sam_output[1])

        # self.gaussian.localize = False  # reverse

        render_pkg["semantic"] = semantic_map_viz  # C, H, W
        render_pkg["masks"] = semantic_map  # 1, H, W

        # depth = render_pkg["depth"]
        # tensor_save_img(depth, "depth.png")
        # tensor_save_img(render_pkg["alpha"], "alpha.png")

        return {
            **render_pkg,
        }

    @torch.no_grad
    def unproject_mask2d(self, cam: Camera, mask2d: torch.Tensor):
        self.weights = torch.zeros_like(self.gaussian._opacity)
        weights_cnt = torch.zeros_like(self.gaussian._opacity, dtype=torch.int32)

        self.gaussian.apply_weights(cam, self.weights, weights_cnt, mask2d.to("cuda"))

        self.weights /= weights_cnt + 1e-7

    @torch.no_grad
    def remove_masked_splats(self):
        self.gaussian.prune_with_mask()

    @torch.no_grad
    def add_points(self, point_xyz, point_rgb):
        new_gs = GaussianModel(sh_degree=0)
        pcd = BasicPointCloud(
            point_xyz, point_rgb, normals=torch.zeros((point_xyz.shape[0], 3))
        )
        new_gs.create_from_pcd(pcd, spatial_lr_scale=0.5)
        mask = self.gaussian.concat_gaussians(new_gs)
        self.gaussian.spatial_lr_scale = get_extent(self.gaussian._xyz) * 1.5
        self.configure_optimizers()
        del new_gs
        return mask

    @torch.no_grad
    def remove_points(self, point_index):
        mask = torch.zeros(self.gaussian.points_num, dtype=torch.bool)
        mask[point_index] = True
        self.gaussian.prune_points(mask)
        self.gaussian.spatial_lr_scale = get_extent(self.gaussian._xyz) * 1.5
        self.configure_optimizers()

    @torch.no_grad
    def add_gsobj(self, gsobj, translation, wxyz, scale):
        self.gaussian.concat_gaussians(gsobj.gaussian, translation, wxyz, scale)

    # @torch.no_grad
    # def remove_gsobj(self, gsobj):
    #     mask = self.gaussian._id == gsobj.id
    #     print(mask)
    #     self.gaussian.prune_points(mask)

    def update_mask_with_threshold(self, threshold):
        semantic_gaussian_mask = (self.weights > threshold)[:, 0]
        self.gaussian.set_mask(semantic_gaussian_mask)
        # self.gaussian.apply_grad_mask(semantic_gaussian_mask)

    @torch.no_grad
    def split(self, point_index):
        mask = torch.full((self.gaussian.points_num,), False, dtype=torch.bool)
        mask[point_index] = True

        (
            active_sh_degree,
            _xyz,
            _features_dc,
            _features_rest,
            _scaling,
            _rotation,
            _opacity,
            max_radii2D,
            xyz_gradient_accum,
            denom,
            opt_dict,
            spatial_lr_scale,
        ) = self.gaussian.capture()

        new_gs = GaussianModel()

        new_gs._xyz = nn.Parameter(_xyz[mask])
        new_gs._features_dc = nn.Parameter(_features_dc[mask])
        new_gs._features_rest = nn.Parameter(_features_rest[mask])
        new_gs._scaling = nn.Parameter(_scaling[mask])
        new_gs._rotation = nn.Parameter(_rotation[mask])
        new_gs._opacity = nn.Parameter(_opacity[mask])
        new_gs.max_radii2D = max_radii2D[mask]

        self.gaussian.prune_points(mask)

        return new_gs

    # Model Training
    def activate(self, mask=None):
        if mask is None:
            self.gaussian.mask = torch.ones(
                self.gaussian.points_num, dtype=torch.bool
            ).cuda()
        else:
            self.gaussian.mask = mask.cuda()
        self.gaussian.remove_grad_mask()
        self.gaussian.apply_grad_mask()

    def deactivate(self, mask=None):
        if mask is None:
            self.gaussian.mask = torch.zeros(
                self.gaussian.points_num, dtype=torch.bool
            ).cuda()
        else:
            self.gaussian.mask = torch.logical_and(self.gaussian.mask, ~mask).cuda()
        self.gaussian.remove_grad_mask()
        self.gaussian.apply_grad_mask()

    def configure_optimizers(self):
        max_it = 3000
        self.opt.iterations = max_it
        # self.opt = OmegaConf.create(vars(self.opt))
        # opt.update(self.training_args)
        # self.gaussian.spatial_lr_scale = self.colmap_cameras_extent
        self.position_lr_max_steps = max_it

        # self.opt.densification_interval = 5
        # self.opt.opacity_reset_interval = 10
        self.opt.densify_from_iter = 0
        self.opt.densify_until_iter = max_it

        self.opt.densification_interval = max_it // 300
        self.opt.opacity_reset_interval = max_it // 10
        # self.opt.densify_from_iter = max_it // 50
        # self.opt.densify_until_iter = max_it / 4 * 3
        self.gaussian.training_setup(self.opt)

    def prepare_output_and_logger(self, args=None):
        # if not self.model_path:
        self.model_path = os.path.join(
            "./output/", datetime.now().strftime("%Y%m%d_%H%S")
        )

        # Set up output folder
        print("Output folder: {}".format(self.model_path))
        os.makedirs(self.model_path, exist_ok=True)
        with open(os.path.join(self.model_path, "cfg_args"), "w") as cfg_log_f:
            cfg_log_f.write(str(SimpleNamespace(**vars(args))))
        # self.tb_writer = SummaryWriter(self.model_path)

    @torch.no_grad
    def get_convex_hull(self) -> tuple[torch.Tensor, torch.Tensor]:
        all_points = self.gaussian._xyz.detach().cpu().numpy()
        hull_points = all_points[self.gaussian.mask.cpu()]
        hull = Delaunay(hull_points)
        is_inside_hull = hull.find_simplex(all_points) >= 0

        centroid = np.mean(hull_points, axis=0)
        dilation_factor = 1.2
        dilated_points = centroid + dilation_factor * (hull_points - centroid)
        dilated_hull = Delaunay(dilated_points)
        is_inside_dilated_hull = dilated_hull.find_simplex(all_points) >= 0

        return (
            torch.tensor(
                is_inside_hull, dtype=torch.bool, device="cuda", requires_grad=False
            ),
            torch.tensor(
                is_inside_dilated_hull,
                dtype=torch.bool,
                device="cuda",
                requires_grad=False,
            ),
        )

    def training_report(
        tb_writer,
        iteration,
        Ll1,
        loss,
        l1_loss,
        elapsed,
        testing_iterations,
        scene,
        renderFunc,
        renderArgs,
    ):
        if tb_writer:
            tb_writer.add_scalar("train_loss_patches/l1_loss", Ll1.item(), iteration)
            tb_writer.add_scalar(
                "train_loss_patches/total_loss", loss.item(), iteration
            )
            tb_writer.add_scalar("iter_time", elapsed, iteration)

        # Report test and samples of training set
        if iteration in testing_iterations:
            torch.cuda.empty_cache()
            validation_configs = (
                {"name": "test", "cameras": scene.getTestCameras()},
                {
                    "name": "train",
                    "cameras": [
                        scene.getTrainCameras()[idx % len(scene.getTrainCameras())]
                        for idx in range(5, 30, 5)
                    ],
                },
            )

            for config in validation_configs:
                if config["cameras"] and len(config["cameras"]) > 0:
                    l1_test = 0.0
                    psnr_test = 0.0
                    for idx, viewpoint in enumerate(config["cameras"]):
                        image = torch.clamp(
                            renderFunc(viewpoint, scene.gaussians, *renderArgs)[
                                "render"
                            ],
                            0.0,
                            1.0,
                        )
                        gt_image = torch.clamp(
                            viewpoint.original_image.to("cuda"), 0.0, 1.0
                        )
                        if tb_writer and (idx < 5):
                            tb_writer.add_images(
                                config["name"]
                                + "_view_{}/render".format(viewpoint.image_name),
                                image[None],
                                global_step=iteration,
                            )
                            if iteration == testing_iterations[0]:
                                tb_writer.add_images(
                                    config["name"]
                                    + "_view_{}/ground_truth".format(
                                        viewpoint.image_name
                                    ),
                                    gt_image[None],
                                    global_step=iteration,
                                )
                        l1_test += l1_loss(image, gt_image).mean().double()
                        psnr_test += psnr(image, gt_image).mean().double()
                    psnr_test /= len(config["cameras"])
                    l1_test /= len(config["cameras"])
                    print(
                        "\n[ITER {}] Evaluating {}: L1 {} PSNR {}".format(
                            iteration, config["name"], l1_test, psnr_test
                        )
                    )
                    if tb_writer:
                        tb_writer.add_scalar(
                            config["name"] + "/loss_viewpoint - l1_loss",
                            l1_test,
                            iteration,
                        )
                        tb_writer.add_scalar(
                            config["name"] + "/loss_viewpoint - psnr",
                            psnr_test,
                            iteration,
                        )

            if tb_writer:
                tb_writer.add_histogram(
                    "scene/opacity_histogram", scene.gaussians.get_opacity, iteration
                )
                tb_writer.add_scalar(
                    "total_points", scene.gaussians.get_xyz.shape[0], iteration
                )
            torch.cuda.empty_cache()
