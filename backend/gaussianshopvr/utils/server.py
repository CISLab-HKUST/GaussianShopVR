import numpy as np
from plyfile import PlyData, PlyElement
import os
from datetime import datetime

# from scipy.spatial.transform import Rotation as R
from viser.transforms import SE3, SO3
from gaussianshopvr.utils.coordinate_utils import get_coordinate_trans_matrix


def time_string():
    return datetime.now().strftime("%m%d_%H%M%S")


def unity2canonical(points, euler_angles):
    points = np.array(points)
    mean = np.mean(points, axis=0)
    max = np.max(points - mean, axis=0).max()
    scale = 0.8 / max

    transform = SE3.from_rotation_and_translation(
        SO3.from_rpy_radians(*np.deg2rad(euler_angles)), mean
    ).inverse()
    points = transform.apply(points)
    points *= scale

    # Unity coordinate to OpenGL coordinate
    RUF2RFU = get_coordinate_trans_matrix("RUF", "RFU")
    points = (RUF2RFU @ points[..., None]).squeeze(-1)

    return points, transform, scale


def read_coordinates(file_path):
    with open(file_path, "r") as file:
        points = [line.strip().split(",") for line in file]
        points = [(float(x), float(y), float(z)) for x, y, z in points]
    return points


# Load pure points from ply file
def pp_load_ply(path):
    # Load the PLY file
    ply_data = PlyData.read(path)

    # Extract the vertex data
    vertex_data = ply_data["vertex"].data

    # Convert the vertex data to a NumPy array
    points = np.array(
        [[vertex["x"], vertex["y"], vertex["z"]] for vertex in vertex_data]
    )
    return points


# Save pure points to ply file
def pp_save_ply(points, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)

    vertex = np.array(
        [(point[0], point[1], point[2]) for point in points],
        dtype=[("x", "f4"), ("y", "f4"), ("z", "f4")],
    )

    # Create PlyElement
    vertex_element = PlyElement.describe(vertex, "vertex")

    # Write to a PLY file
    ply_data = PlyData([vertex_element])
    ply_data.write(path)


def write_obj_file(points, output_path):
    with open(output_path, "w") as file:
        file.write("# OBJ file\n")
        for point in points:
            file.write(f"v {point[0]} {point[1]} {point[2]}\n")


def convert_txt_to_obj(txt_file, obj_file, euler_angles=(0, 0, 0)):
    points = read_coordinates(txt_file)
    adjusted_points, _, _ = unity2canonical(points, euler_angles)
    write_obj_file(adjusted_points, obj_file)


def convert_txt_to_ply(txt_file, ply_file, euler_angles=(0, 0, 0)):
    points = read_coordinates(txt_file)
    adjusted_points, _, _ = unity2canonical(points, euler_angles)
    pp_save_ply(adjusted_points, ply_file)


if __name__ == "__main__":
    # Usage example
    txt_file = "PointCloud_20240707_190446.txt"
    convert_txt_to_ply(txt_file, "obj_1.ply")
    convert_txt_to_ply(txt_file, "obj_2.ply", (-4.342, 137.411, 4.135))
