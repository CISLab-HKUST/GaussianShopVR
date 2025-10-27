import numpy as np

dir_vecs = {
    "R": (1, 0, 0),
    "L": (-1, 0, 0),
    "U": (0, 1, 0),
    "D": (0, -1, 0),
    "F": (0, 0, -1),
    "B": (0, 0, 1),
}


def get_coordinate_RUB_matrix(coordinate_type: str) -> np.ndarray:
    assert (
        len(coordinate_type) == 3
    ), "Coordinate system type should be 3-character-long, e.g. RUB"
    for c in coordinate_type:
        assert (
            c in dir_vecs.keys()
        ), f"Unknown direction {c} - Should be in {dir_vecs.keys()}"
    trans_matrix = [dir_vecs[c] for c in coordinate_type]
    trans_matrix = np.array(trans_matrix).T
    assert np.linalg.det(trans_matrix) != 0, f"{coordinate_type} is invalid."
    return trans_matrix


def get_coordinate_trans_matrix(src_type, dest_type="RUB"):
    src_RUB_matrix = get_coordinate_RUB_matrix(src_type)
    dest_RUB_matrix = get_coordinate_RUB_matrix(dest_type)
    trans_matrix = np.linalg.inv(dest_RUB_matrix) @ src_RUB_matrix
    return trans_matrix


def convert_coordinate(src, src_type, dest_type="RUB"):
    src = np.array(src)
    trans_matrix = get_coordinate_trans_matrix(src_type, dest_type)
    return (src.T @ trans_matrix).T


if __name__ == "__main__":
    p = (1, 1, 1)
    p_p = convert_coordinate(p, "RUF", "RUB")
    print(p_p)
