#include "CudaKernels.h"
#include "auxiliary.h"
__global__ void fill_kernel(int width, int height, float value, cudaSurfaceObject_t surface)
{
    uint32_t x = threadIdx.x + blockDim.x * blockIdx.x;
    uint32_t y = threadIdx.y + blockDim.y * blockIdx.y;

    if (x < width && y < height)
    {
        float4 rgba;
        rgba.x = value * x / width * y / height;
        rgba.y = value;
        rgba.z = value * (width - x) / width * (height - y) / height;
        rgba.w = value;
        surf2Dwrite(rgba, surface, (int)sizeof(float4) * x, y, cudaBoundaryModeClamp);
    }
}

__global__ void fill_depth_kernel(int width, int height, float value, cudaSurfaceObject_t surface)
{
    uint32_t x = threadIdx.x + blockDim.x * blockIdx.x;
    uint32_t y = threadIdx.y + blockDim.y * blockIdx.y;

    if (x < width && y < height)
    {
        float r = value * x / width * y / height;
        surf2Dwrite(r, surface, (int)sizeof(float) * x, y, cudaBoundaryModeClamp);
    }
}

__global__ void copy_depth_kernel(int width, int height, cudaSurfaceObject_t source, cudaSurfaceObject_t cible)
{
    uint32_t x = threadIdx.x + blockDim.x * blockIdx.x;
    uint32_t y = threadIdx.y + blockDim.y * blockIdx.y;

    if (x < width && y < height)
    {
        float r;
        surf2Dread(&r, source, (int)sizeof(float) * x, y, cudaBoundaryModeClamp);
        surf2Dwrite(r, cible, (int)sizeof(float) * x, y, cudaBoundaryModeClamp);
    }
}

__global__ void splat_to_texture_kernel(int width, int height, int channel, float *InData, cudaSurfaceObject_t surface)
{
    uint32_t x = threadIdx.x + blockDim.x * blockIdx.x;
    uint32_t y = threadIdx.y + blockDim.y * blockIdx.y;

    if (x < width && y < height)
    {

        // Flip y
        uint32_t y_flip = height - 1 - y;

        if (channel == 4)
        {
            float4 rgba;
            rgba.x = InData[0 * width * height + (y_flip * width + x)];
            rgba.y = InData[1 * width * height + (y_flip * width + x)];
            rgba.z = InData[2 * width * height + (y_flip * width + x)];
            rgba.w = InData[3 * width * height + (y_flip * width + x)];

            surf2Dwrite(rgba, surface, (int)sizeof(float4) * x, y, cudaBoundaryModeClamp);
        }

        if (channel == 1)
        {
            float r = InData[0 * width * height + (y_flip * width + x)];
            surf2Dwrite(r, surface, (int)sizeof(float) * x, y, cudaBoundaryModeClamp);
        }
    }
}
__global__ void remove_point(float *new_cuda, const float *cuda, const size_t *keep_indices, size_t num_keep, size_t point_size)
{
    size_t idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < num_keep)
    {
        size_t original_idx = keep_indices[idx];
        memcpy((char *)new_cuda + idx * point_size, (char *)cuda + original_idx * point_size, point_size);
    }
}

__global__ void append_point(char *new_cuda_mid, const float *cuda, const size_t *splited_indices, size_t num_splited, size_t point_size)
{
    size_t idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < num_splited)
    {
        size_t original_idx = splited_indices[idx];
        memcpy((char *)new_cuda_mid + idx * point_size, (char *)cuda + original_idx * point_size, point_size);
    }
}

__device__ void computeColorFromSH(
    int idx,
    int deg,
    int max_coeffs,
    const float *means,  // xyz positions
    const float *campos, // camera position
    const float *shs,
    float *result, // output RGB color
    bool *clamped)
{
    float pos[3] = {means[idx * 3], means[idx * 3 + 1], means[idx * 3 + 2]};
    float dir[3] = {
        pos[0] - campos[0],
        pos[1] - campos[1],
        pos[2] - campos[2]};

    float len = sqrtf(dir[0] * dir[0] + dir[1] * dir[1] + dir[2] * dir[2]);
    dir[0] /= len;
    dir[1] /= len;
    dir[2] /= len;

    float *sh = (float *)(shs + idx * max_coeffs * 3);
    result[0] = SH_C0 * sh[0]; // R
    result[1] = SH_C0 * sh[1]; // G
    result[2] = SH_C0 * sh[2]; // B

    for (int c = 0; c < 3; c++)
    {
        result[c] += 0.5f;
        clamped[3 * idx + c] = (result[c] < 0) || (result[c] > 1);
        result[c] = fmaxf(result[c], 0.0f);
        result[c] = fminf(result[c], 1.0f);
    }
}

__device__ void computeSHFromColor(
    const float *color,
    const float *pos,
    const float *campos,
    const float *shs_origin,
    float *shs,
    int deg,
    int max_coeffs)
{
    float dir[3] = {
        pos[0] - campos[0],
        pos[1] - campos[1],
        pos[2] - campos[2]};

    float len = sqrtf(dir[0] * dir[0] + dir[1] * dir[1] + dir[2] * dir[2]);
    dir[0] /= len;
    dir[1] /= len;
    dir[2] /= len;

    float x = dir[0];
    float y = dir[1];
    float z = dir[2];

    float xx = x * x, yy = y * y, zz = z * z;
    float xy = x * y, yz = y * z, xz = x * z;

    float adjusted_color[3] = {
        color[0] - 0.5f,
        color[1] - 0.5f,
        color[2] - 0.5f};

    // Create temporary array for SH coefficients
    float temp_shs[48] = {0};
    // Copy original SH coefficients to temp array
    memcpy(temp_shs, shs_origin, max_coeffs * 3 * sizeof(float));

    for (int c = 0; c < 3; c++)
    {
        temp_shs[c] = adjusted_color[c] / SH_C0;
    }

    // Copy the computed coefficients to the output array
    memcpy(shs, temp_shs, max_coeffs * 3 * sizeof(float));
}

__global__ void modifySelectedPointsColorKernel(
    const float *shs,
    float *new_shs,
    const float *isSelected,
    const float *pos,
    const float *campos,
    const float *rArray,
    const float *gArray,
    const float *bArray,
    int P,
    int deg,
    int max_coeffs)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;

    if (idx >= P || isSelected[idx] < 0.5)
    {
        memcpy(new_shs + idx * max_coeffs * 3, shs + idx * max_coeffs * 3, max_coeffs * 3 * sizeof(float));
        return;
    }

    // memcpy(new_shs + idx * max_coeffs * 3, shs + idx * max_coeffs * 3, max_coeffs * 3 * sizeof(float));

    bool clamped[3];
    float currentColor[3];
    computeColorFromSH(
        idx, deg, max_coeffs,
        pos + idx * 3,
        campos,
        shs,
        currentColor,
        clamped);

    float newColor[3];
    newColor[0] = rArray[int(currentColor[0] * 255.0f)] / 255.0f;
    newColor[1] = gArray[int(currentColor[1] * 255.0f)] / 255.0f;
    newColor[2] = bArray[int(currentColor[2] * 255.0f)] / 255.0f;

    computeSHFromColor(
        newColor,
        pos + idx * 3,
        campos,
        shs + idx * max_coeffs * 3,
        new_shs + idx * max_coeffs * 3,
        deg,
        max_coeffs);
}

template <typename T>
T div_round_up(T val, T divisor)
{
    return (val + divisor - 1) / divisor;
}

void cuda_fill(int width, int height, float value, cudaSurfaceObject_t surface)
{
    const dim3 threads = {16, 16, 1};
    const dim3 blocks = {div_round_up<uint32_t>((uint32_t)width, threads.x), div_round_up<uint32_t>((uint32_t)height, threads.y), 1};
    fill_kernel<<<blocks, threads>>>(width, height, value, surface);
}

void cuda_fill_depth(int width, int height, float value, cudaSurfaceObject_t surface)
{
    const dim3 threads = {16, 16, 1};
    const dim3 blocks = {div_round_up<uint32_t>((uint32_t)width, threads.x), div_round_up<uint32_t>((uint32_t)height, threads.y), 1};
    fill_depth_kernel<<<blocks, threads>>>(width, height, value, surface);
}

void cuda_copy_depth_kernel(int width, int height, cudaSurfaceObject_t source, cudaSurfaceObject_t cible)
{
    const dim3 threads = {16, 16, 1};
    const dim3 blocks = {div_round_up<uint32_t>((uint32_t)width, threads.x), div_round_up<uint32_t>((uint32_t)height, threads.y), 1};
    copy_depth_kernel<<<blocks, threads>>>(width, height, source, cible);
}

void cuda_splat_to_texture(int width, int height, int channel, float *rgb, cudaSurfaceObject_t surface)
{
    const dim3 threads = {16, 16, 1};
    const dim3 blocks = {div_round_up((uint32_t)width, threads.x), div_round_up((uint32_t)height, threads.y), 1};
    splat_to_texture_kernel<<<blocks, threads>>>(width, height, channel, rgb, surface);
}

void cuda_remove_point(float *new_cuda, const float *cuda, const size_t *keep_indices, size_t num_keep, size_t point_size)
{
    int blockSize = 256;
    int numBlocks = (num_keep + blockSize - 1) / blockSize;

    remove_point<<<numBlocks, blockSize>>>(new_cuda, cuda, keep_indices, num_keep, point_size);
}

void cuda_append_point(char *new_cuda_mid, const float *cuda, const size_t *splited_indices, size_t num_splited, size_t point_size)
{
    int blockSize = 256;
    int numBlocks = (num_splited + blockSize - 1) / blockSize;

    append_point<<<numBlocks, blockSize>>>(new_cuda_mid, cuda, splited_indices, num_splited, point_size);
}

float *ModifySelectedPointsColor(
    const float *shs_cuda,
    const float *isSelected_cuda,
    const float *pos_cuda,
    const float *campos,
    const float *rArray_cuda,
    const float *gArray_cuda,
    const float *bArray_cuda,
    int P,
    int deg,
    int max_coeffs)
{
    const int blockSize = 256;
    const int numBlocks = (P + blockSize - 1) / blockSize;
    float *new_shs_cuda = nullptr;
    CUDA_SAFE_CALL(cudaMalloc((void **)&new_shs_cuda, P * max_coeffs * 3 * sizeof(float)));
    modifySelectedPointsColorKernel<<<numBlocks, blockSize>>>(
        shs_cuda,
        new_shs_cuda,
        isSelected_cuda,
        pos_cuda,
        campos,
        rArray_cuda,
        gArray_cuda,
        bArray_cuda,
        P,
        deg,
        max_coeffs);
    // CUDA_SAFE_CALL(cudaFree(shs_cuda));

    CUDA_SAFE_CALL(cudaGetLastError());
    CUDA_SAFE_CALL(cudaDeviceSynchronize());
    return new_shs_cuda;
}