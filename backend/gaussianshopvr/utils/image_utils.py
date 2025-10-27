import torch
import torchvision.transforms as transforms
from PIL import Image
import cv2
import numpy as np
import requests
from io import BytesIO
from concurrent.futures import ThreadPoolExecutor, as_completed
import base64
import json

# from diffusers import AutoPipelineForInpainting

# from diffusers.utils import load_image, make_image_grid


# pipeline = AutoPipelineForInpainting.from_pretrained(
#     "diffusers/stable-diffusion-xl-1.0-inpainting-0.1",
#     torch_dtype=torch.float16,
#     variant="fp16",
# )
# pipeline.enable_model_cpu_offload()


# Function to load an image and convert it to a tensor
def load_image(img_path):
    img = Image.open(img_path).convert("RGB")
    return transforms.functional.to_tensor(img)


def cvt_tensor_img(img_tensor, norm=False):
    if norm:
        img_tensor = (img_tensor - img_tensor.min()) / (
            img_tensor.max() - img_tensor.min()
        )
    img_tensor = torch.clamp(img_tensor, 0.0, 1.0)
    img = transforms.functional.to_pil_image(img_tensor)
    return img


def tensor_save_img(img_tensor, path, norm=False):
    img = cvt_tensor_img(img_tensor, norm)
    img.save(path)


def get_diff_mask(img1, img2, threshold):
    # Ensure the images have the same dimensions
    if img1.shape != img2.shape:
        raise ValueError("Images do not have the same dimensions")

    # Calculate the absolute difference
    difference = torch.abs(img1 - img2)
    mask = (difference.sum(dim=0) > threshold).type(torch.float32)

    return mask


def get_diff_mask_from_files(
    img_path1, img_path2, threshold=0.18, output_path="diff_mask.png"
):
    img1 = load_image(img_path1)
    img2 = load_image(img_path2)
    diff_mask = get_diff_mask(img1, img2, threshold)

    transforms.ToPILImage()(diff_mask).convert("L").save(output_path, "PNG")


def save_2dpoints_img(points, width, height, point_colors=None, path="2dpoints.jpg"):
    if point_colors is None:
        point_colors = [(0, 255, 0)] * points.shape[0]
    image = np.zeros((width, height, 3), dtype="uint8")
    for point, point_color in zip(points, point_colors):
        image = cv2.circle(image, point, 5, point_color, -1)
    cv2.imwrite(path, image)
    return cv2.cvtColor(image, cv2.COLOR_BGR2RGB).transpose(2, 0, 1)


def save_hull_img(points, width, height, path="convex_hull.jpg"):
    image = np.zeros((width, height, 3), dtype="uint8")
    # image = cv2.fillConvexPoly(image, points, (255, 255, 255))
    points = points.reshape((-1, 1, 2))
    cv2.fillPoly(image, [points], color=(255, 255, 255))
    cv2.imwrite(path, image)
    return cv2.cvtColor(image, cv2.COLOR_BGR2RGB).transpose(2, 0, 1)


def get_inpainted_imgs(ori_img, mask, prompt, N):
    # generator = torch.Generator("cuda").manual_seed(92)
    prompt = prompt + "highly detailed, 8k"
    images = pipeline(
        prompt=prompt,
        image=ori_img,
        mask_image=mask,
        strength=0.99,
        num_images_per_prompt=N,
    ).images
    return [transforms.functional.pil_to_tensor(_).cuda().float() / 255 for _ in images]


def get_inpainted_imgs_online(ori_img, mask, prompt, N):
    ori_img = cvt_tensor_img(torch.tensor(ori_img))
    mask = cvt_tensor_img(torch.tensor(mask))

    def fetch():
        img_bytes = BytesIO()
        ori_img.save(img_bytes, format="jpeg")

        mask_bytes = BytesIO()
        mask.save(mask_bytes, format="jpeg")

        response = requests.post(
            f"https://api.stability.ai/v2beta/stable-image/edit/inpaint",
            headers={
                "authorization": f"Bearer sk-Y3YGsl3JqvLyMkeFfxmGuj9XPmTmvMHPTo3yXo7OHMUrVKSq",
                "accept": "image/*",
            },
            files={
                "image": img_bytes.getvalue(),
                "mask": mask_bytes.getvalue(),
            },
            data={
                "prompt": prompt,
                "output_format": "jpeg",
            },
        )

        if response.status_code == 200:
            bytes = BytesIO(response.content)
            return transforms.functional.to_tensor(Image.open(bytes))
            # with open("inpaint.png", "wb") as file:
            #     file.write(response.content)
        else:
            raise Exception(str(response.json()))

    imgs = []
    with ThreadPoolExecutor(max_workers=5) as executor:
        futures = [executor.submit(fetch) for _ in range(N)]

        for future in as_completed(futures):
            img = future.result()
            imgs.append(img)

    return imgs


def get_inpainted_imgs_lama(ori_img, mask, N):
    ori_img = (
        torch.clamp(torch.tensor(ori_img), 0, 1).cpu().numpy().transpose((1, 2, 0))
        * 255
    ).astype(np.uint8)[..., [2, 1, 0]]
    mask = (
        (1 - torch.clamp(torch.tensor(mask), 0, 1).cpu().numpy().transpose((1, 2, 0)))
        * 255
    ).astype(np.uint8)[..., [2, 1, 0]]

    def image2url(image):
        _, buffer = cv2.imencode(".png", image)
        image_url = base64.b64encode(buffer).decode("utf-8")
        return "data:image/png;base64," + image_url

    def url2image(url, channel=None):
        if url == "":
            return None
        resp = urllib.request.urlopen(url)
        image = np.asarray(bytearray(resp.read()), dtype="uint8")
        if channel == 3:  # by default read the images as colored images
            image = cv2.imdecode(image, cv2.IMREAD_COLOR)
        elif (
            channel == 1
        ):  # if optional argument color is given as False, then read as black-and-white image
            image = cv2.imdecode(image, cv2.IMREAD_GRAYSCALE)
        else:
            image = cv2.imdecode(image, cv2.IMREAD_UNCHANGED)
        return image

    ori_img = image2url(ori_img)
    mask = image2url(mask)

    def fetch():
        # img_bytes = BytesIO()
        # ori_img.save(img_bytes, format="jpeg")

        # mask_bytes = BytesIO()
        # mask.save(mask_bytes, format="jpeg")

        response = requests.post(
            f"http://localhost:9011",
            # headers={
            #     "authorization": f"Bearer sk-Y3YGsl3JqvLyMkeFfxmGuj9XPmTmvMHPTo3yXo7OHMUrVKSq",
            #     "accept": "image/*",
            # },
            # files={
            #     "image": img_bytes.getvalue(),
            #     "mask": mask_bytes.getvalue(),
            # },
            json={"image": ori_img, "alpha": mask},
        )

        if response.status_code == 200:
            # bytes = BytesIO(response.content)
            img_data = base64.b64decode(response.json()["image_url"][22:])
            img = Image.open(BytesIO(img_data))
            return transforms.functional.to_tensor(img)
            # with open("inpaint.png", "wb") as file:
            #     file.write(response.content)
        else:
            raise Exception(str(response.json()))

    imgs = []
    with ThreadPoolExecutor(max_workers=5) as executor:
        futures = [executor.submit(fetch) for _ in range(N)]

        for future in as_completed(futures):
            img = future.result()
            imgs.append(img)

    return imgs


if __name__ == "__main__":
    get_diff_mask_from_files("1.png", "2.png")
