#pragma once

#include <string>
#include <cuda_runtime.h>
#include <exception>
#include <stdexcept>
#include <fstream>
using namespace std;

#define CUDA_SAFE_CALL_ALWAYS(A) \
	A;                           \
	cuda_error_throw(__FILE__, __LINE__);
#define CUDA_SAFE_CALL(A) \
	A;                    \
	cuda_error_throw(__FILE__, __LINE__);
#define CUDA_ERROR(A) cuda_error(A, __FILE__, __LINE__)

inline void cuda_error_throw(char *filename, int line) throw(std::bad_exception)
{
#if DEBUG || _DEBUG
	if (cudaPeekAtLastError() != cudaSuccess)
	{
		throw std::runtime_error(cudaGetErrorString(cudaGetLastError()));
	}
	cudaDeviceSynchronize();
	if (cudaPeekAtLastError() != cudaSuccess)
	{
		throw std::runtime_error(cudaGetErrorString(cudaGetLastError()));
	}
#endif
}

inline bool cuda_error(std::string &_message, char *filename, int line)
{
	if (cudaPeekAtLastError() != cudaSuccess)
	{
		_message.assign(cudaGetErrorString(cudaGetLastError()));
		return true;
	}
#if DEBUG || _DEBUG
	cudaDeviceSynchronize();
	if (cudaPeekAtLastError() != cudaSuccess)
	{
		_message.assign(cudaGetErrorString(cudaGetLastError()));
		return true;
	}
#endif
	return false;
}

void cuda_fill(int width, int height, float value, cudaSurfaceObject_t surface);
void cuda_fill_depth(int width, int height, float value, cudaSurfaceObject_t surface);
void cuda_copy_depth_kernel(int width, int height, cudaSurfaceObject_t source, cudaSurfaceObject_t cible);
void cuda_splat_to_texture(int width, int height, int channel, float *rgb, cudaSurfaceObject_t surface);
void cuda_remove_point(float *new_cuda, const float *cuda, const size_t *keep_indices, size_t num_keep, size_t point_size);
void cuda_append_point(char *new_cuda_mid, const float *cuda, const size_t *splited_indices, size_t num_splited, size_t point_size);
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
	int max_coeffs);
