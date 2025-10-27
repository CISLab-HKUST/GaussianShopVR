#include "GaussianSplatting.h"
#include "GaussianSplatting.h"
#include "CudaKernels.h"
#include <cuda_runtime.h>
#include <rasterizer.h>
#include <fstream>
#include <string>
#include <sstream>
#include <iostream>
#include <chrono>
#include <iomanip>
#include <ctime>
#include <cstring>
#ifdef _WIN32
#include <direct.h>
#define CREATE_DIR(dir) _mkdir(dir)
#else
#include <sys/stat.h>
#define CREATE_DIR(dir) mkdir(dir, 0777)
#endif
using namespace std;

typedef Eigen::Matrix<int, 3, 1, Eigen::DontAlign> Vector3i;

inline float sigmoid(const float m1) { return 1.0f / (1.0f + exp(-m1)); }

inline std::function<char *(size_t N)> resizeFunctional(void **ptr, size_t &S)
{
	auto lambda = [ptr, &S](size_t N)
	{
		if (N > S)
		{
			if (*ptr)
				CUDA_SAFE_CALL(cudaFree(*ptr));
			CUDA_SAFE_CALL(cudaMalloc(ptr, 2 * N));
			S = 2 * N;
		}
		return reinterpret_cast<char *>(*ptr);
	};
	return lambda;
}
float *cuda_copy_memory(const float *source_cuda, size_t size_in_bytes)
{
	float *dest_cuda = nullptr;
	CUDA_SAFE_CALL(cudaMalloc((void **)&dest_cuda, size_in_bytes));
	CUDA_SAFE_CALL(cudaMemcpy(dest_cuda, source_cuda, size_in_bytes, cudaMemcpyDeviceToDevice));
	return dest_cuda;
}
template <typename T>
float *append_cuda(float *cuda, size_t sz, vector<T> &data)
{
	// debug_log << "append_cuda" << std::endl;
	float *ncuda = nullptr;
	size_t snb = sizeof(T) * data.size();
	size_t size = sizeof(T) * sz;

	CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&ncuda, size + snb));
	if (cuda != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaMemcpy(ncuda, cuda, size, cudaMemcpyDeviceToDevice));
	}
	CUDA_SAFE_CALL_ALWAYS(cudaMemcpy(((char *)ncuda) + size, data.data(), snb, cudaMemcpyHostToDevice));
	if (cuda != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree(cuda));
	}
	return ncuda;
}

template <typename T>
float *remove_point_cuda(float *cuda, size_t new_count, std::vector<size_t> keep_indices)
{
	float *new_cuda = nullptr;
	CUDA_SAFE_CALL(cudaMalloc((void **)&new_cuda, new_count * sizeof(T)));

	if (cuda != nullptr)
	{
		size_t *d_keep_indices;
		CUDA_SAFE_CALL(cudaMalloc((void **)&d_keep_indices, keep_indices.size() * sizeof(size_t)));
		CUDA_SAFE_CALL(cudaMemcpy(d_keep_indices, keep_indices.data(), keep_indices.size() * sizeof(size_t), cudaMemcpyHostToDevice));

		cuda_remove_point(new_cuda, cuda, d_keep_indices, keep_indices.size(), sizeof(T));
		CUDA_SAFE_CALL(cudaGetLastError());
		CUDA_SAFE_CALL(cudaDeviceSynchronize());

		CUDA_SAFE_CALL(cudaFree(d_keep_indices));
	}

	CUDA_SAFE_CALL(cudaFree(cuda));
	return new_cuda;
}
template <typename T>
float *split_point_cuda(float *cuda, size_t sz, std::vector<size_t> keep_indices, std::vector<size_t> splited_indices)
{
	float *new_cuda = nullptr;
	CUDA_SAFE_CALL(cudaMalloc((void **)&new_cuda, sz * sizeof(T)));

	if (cuda != nullptr)
	{
		size_t *d_keep_indices;
		CUDA_SAFE_CALL(cudaMalloc((void **)&d_keep_indices, keep_indices.size() * sizeof(size_t)));
		CUDA_SAFE_CALL(cudaMemcpy(d_keep_indices, keep_indices.data(), keep_indices.size() * sizeof(size_t), cudaMemcpyHostToDevice));

		size_t *d_splited_indices;
		CUDA_SAFE_CALL(cudaMalloc((void **)&d_splited_indices, splited_indices.size() * sizeof(size_t)));
		CUDA_SAFE_CALL(cudaMemcpy(d_splited_indices, splited_indices.data(), splited_indices.size() * sizeof(size_t), cudaMemcpyHostToDevice));

		cuda_remove_point(new_cuda, cuda, d_keep_indices, keep_indices.size(), sizeof(T));
		cuda_append_point((char *)new_cuda + keep_indices.size() * sizeof(T), cuda, d_splited_indices, splited_indices.size(), sizeof(T));
		CUDA_SAFE_CALL(cudaGetLastError());
		CUDA_SAFE_CALL(cudaDeviceSynchronize());

		CUDA_SAFE_CALL(cudaFree(d_keep_indices));
	}

	CUDA_SAFE_CALL(cudaFree(cuda));
	cuda = nullptr;
	return new_cuda;
}

template <typename T>
float *remove_cuda(float *cuda, size_t sz, size_t pos, size_t nb)
{
	if (cuda == nullptr)
	{
		return nullptr;
	}

	float *ncuda = nullptr;
	size_t snb = sizeof(T) * nb;   // snb is the size of model to be removed
	size_t spos = sizeof(T) * pos; // spos is the total size before the model to be removed
	size_t size = sizeof(T) * sz;  // size is the total size of all models
	CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&ncuda, size - snb));
	if (spos > 0)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaMemcpy(ncuda, cuda, spos, cudaMemcpyDeviceToDevice));
	}
	if (spos + snb < size)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaMemcpy(((char *)ncuda + spos), ((char *)cuda) + spos + snb, size - snb - spos, cudaMemcpyDeviceToDevice));
	}
	CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)cuda));
	return ncuda;
}

// Gaussian Splatting data structure
template <int D>
struct RichPoint
{
	Pos pos;
	float n[3];
	SHs<D> shs;
	float opacity;
	Scale scale;
	Rot rot;
};

template <int D>
int loadPly(const char *filename, std::vector<Pos> &pos, std::vector<SHs<3>> &shs, std::vector<float> &opacities, std::vector<Scale> &scales, std::vector<Rot> &rot, std::vector<float> &isSelected, Vector3f &minn, Vector3f &maxx) throw(std::bad_exception);
void writePly(const char *filename, const std::vector<Pos> &pos, const std::vector<SHs<3>> &shs, const std::vector<float> &opacities, const std::vector<Scale> &scales, const std::vector<Rot> &rot);

void GaussianSplattingRenderer::SetModelCrop(int model, float *box_min, float *box_max)
{
	for (std::list<SplatModel>::iterator it = models.begin(); it != models.end(); ++it)
	{
		if (it->index == model)
		{
			it->_boxmin = Vector3f(box_min);
			it->_boxmax = Vector3f(box_max);
			break;
		}
	}
}
std::ofstream GaussianSplattingRenderer::debug_log;

GaussianSplattingRenderer::GaussianSplattingRenderer()
{
	if (!debug_log.is_open())
	{
		debug_log.open("gaussian_splatting_debug.log", std::ios::out);
	}
	debug_log << "Created new GaussianSplattingRenderer instance" << std::endl;
}
void GaussianSplattingRenderer::SetTwoColors(float *selectedColor, float *unselectedColor)
{
	CUDA_SAFE_CALL(cudaMemcpy((char *)(selectedColor_cuda), selectedColor, sizeof(float) * 4, cudaMemcpyHostToDevice));
	CUDA_SAFE_CALL(cudaMemcpy((char *)(unselectedColor_cuda), unselectedColor, sizeof(float) * 4, cudaMemcpyHostToDevice));
}
void GaussianSplattingRenderer::SetPointSize(float pointSize)
{
	this->pointSize = pointSize;
}
void GaussianSplattingRenderer::SetShowCenter(bool _show_centers)
{
	this->show_centers = _show_centers;
}
void GaussianSplattingRenderer::SetDepthCutoff(float depthcutoff)
{
	this->depthcutoff = depthcutoff;
}
int GaussianSplattingRenderer::SelectPointsInSphere(float *center, float radius)
{
	isSelecting = true;
	CUDA_SAFE_CALL(cudaMemcpy(selectionCenter, center, sizeof(float) * 3, cudaMemcpyHostToDevice));
	selectionRadius = radius;
	return 0;
}
void GaussianSplattingRenderer::StopSelection()
{
	isSelecting = false;
}
void GaussianSplattingRenderer::ClearSelection()
{
	if (isSelected_cuda != nullptr)
	{
		const std::lock_guard<std::mutex> lock(cuda_mtx);
		CUDA_SAFE_CALL(cudaMemset(isSelected_cuda, 0, sizeof(float) * count));
	}
	// this->isClearSelection = isClearSelection;
}
void GaussianSplattingRenderer::SetEraseSelection(bool isEraseSelection)
{
	this->isEraseSelection = isEraseSelection;
}
void GaussianSplattingRenderer::GetModelCrop(int model, float *box_min, float *box_max)
{
	for (std::list<SplatModel>::iterator it = models.begin(); it != models.end(); ++it)
	{
		if (it->index == model)
		{
			box_min[0] = it->_scenemin.x();
			box_min[1] = it->_scenemin.y();
			box_min[2] = it->_scenemin.z();
			box_max[0] = it->_scenemax.x();
			box_max[1] = it->_scenemax.y();
			box_max[2] = it->_scenemax.z();
			break;
		}
	}
}

int GaussianSplattingRenderer::GetNbSplat()
{
	return count;
}

void GaussianSplattingRenderer::Load(const char *file)
{
	count_cpu = 0;

	// Load the PLY data (AoS) to the GPU (SoA)
	if (_sh_degree == 1)
	{
		count_cpu = loadPly<1>(file, pos, shs, opacity, scale, rot, isSelected, _scenemin, _scenemax);
	}
	else if (_sh_degree == 2)
	{
		count_cpu = loadPly<2>(file, pos, shs, opacity, scale, rot, isSelected, _scenemin, _scenemax);
	}
	else if (_sh_degree == 3)
	{
		count_cpu = loadPly<3>(file, pos, shs, opacity, scale, rot, isSelected, _scenemin, _scenemax);
	}
}

int GaussianSplattingRenderer::CopyToCuda()
{
	if (count_cpu == 0)
	{
		return 0;
	}

	const std::lock_guard<std::mutex> lock(cuda_mtx);
	debug_log << "CopyToCuda" << std::endl;
	// Register new model
	model_idx += 1;
	models.push_back({model_idx, count_cpu, false, _scenemin, _scenemax, _scenemin, _scenemax});

	pos_cuda = append_cuda(pos_cuda, count, pos);
	rot_cuda = append_cuda(rot_cuda, count, rot);
	shs_cuda = append_cuda(shs_cuda, count, shs);
	shs_origin_cuda = append_cuda(shs_origin_cuda, count, shs);
	opacity_cuda = append_cuda(opacity_cuda, count, opacity);
	scale_cuda = append_cuda(scale_cuda, count, scale);

	isSelected_cuda = append_cuda(isSelected_cuda, count, isSelected);
	// set new size with the appened model
	count += count_cpu;

	// Working buffer or fixed data
	// can be fully reallocated
	if (background_cuda != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)background_cuda));
	}
	if (rect_cuda != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)rect_cuda));
	}
	CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&background_cuda, 3 * sizeof(float)));
	CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&rect_cuda, 2 * count * sizeof(int)));

	bool white_bg = false;
	float bg[3] = {white_bg ? 1.f : 0.f, white_bg ? 1.f : 0.f, white_bg ? 1.f : 0.f};
	CUDA_SAFE_CALL_ALWAYS(cudaMemcpy(background_cuda, bg, 3 * sizeof(float), cudaMemcpyHostToDevice));

	AllocateRenderContexts();

	// Update count and return new model index
	return model_idx;
}

void GaussianSplattingRenderer::RemoveModel(int model)
{
	const std::lock_guard<std::mutex> lock(cuda_mtx);
	debug_log << "remove model:" << model << std::endl;
	size_t start = 0;
	std::list<SplatModel>::iterator mit = models.end();
	for (std::list<SplatModel>::iterator it = models.begin(); it != models.end(); ++it)
	{
		if (it->index == model)
		{
			mit = it;
			break;
		}
		start += it->size;
	}
	debug_log << "mit:" << mit->index << std::endl;
	if (mit != models.end())
	{
		model_idx -= 1;
		size_t size = mit->size; // the size of model to be removed
		pos_cuda = remove_cuda<Pos>(pos_cuda, count, start, size);
		rot_cuda = remove_cuda<Rot>(rot_cuda, count, start, size);
		shs_cuda = remove_cuda<SHs<3>>(shs_cuda, count, start, size);
		opacity_cuda = remove_cuda<float>(opacity_cuda, count, start, size);
		scale_cuda = remove_cuda<Scale>(scale_cuda, count, start, size);
		isSelected_cuda = remove_cuda<float>(isSelected_cuda, count, start, size);
		count -= size;
		models.erase(mit);
		// int new_index = 1;
		// for (auto &model : models)
		// {
		// 	model.index = new_index;
		// 	new_index++;
		// }
		debug_log << "model removed, model left: " << models.size() << std::endl;
		// Working buffer or fixed data
		// can be fully reallocated
		if (background_cuda != nullptr)
		{
			CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)background_cuda));
		}
		if (rect_cuda != nullptr)
		{
			CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)rect_cuda));
		}
		CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&background_cuda, 3 * sizeof(float)));
		CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&rect_cuda, 2 * count * sizeof(int)));

		bool white_bg = false;
		float bg[3] = {white_bg ? 1.f : 0.f, white_bg ? 1.f : 0.f, white_bg ? 1.f : 0.f};
		CUDA_SAFE_CALL_ALWAYS(cudaMemcpy(background_cuda, bg, 3 * sizeof(float), cudaMemcpyHostToDevice));
		debug_log << "AllocateRenderContexts" << std::endl;
		AllocateRenderContexts();
		debug_log << "AllocateRenderContexts done" << std::endl;
	}
	else
	{
		debug_log << "Model index not found." << std::endl;
		throw std::runtime_error("Model index not found.");
	}
}

int *GaussianSplattingRenderer::GetSelectedPoints(int model)
{
	size_t model_start = 0;
	size_t model_size = 0;
	bool found_model = false;

	for (const auto &m : models)
	{
		if (m.index == model)
		{
			model_size = m.size;
			found_model = true;
			break;
		}
		model_start += m.size;
	}

	if (!found_model)
	{
		debug_log << "Model " << model << " not found" << std::endl;
		return nullptr;
	}

	std::vector<float> isSelected_host(count);
	CUDA_SAFE_CALL(cudaMemcpy(isSelected_host.data(), isSelected_cuda, count * sizeof(float), cudaMemcpyDeviceToHost));
	std::vector<int> selected_indices;
	for (size_t i = 0; i < model_size; i++)
	{
		if (isSelected_host[model_start + i] > 0.5f)
		{
			selected_indices.push_back(static_cast<int>(i));
		}
	}

	int *result = new int[selected_indices.size() + 1];
	result[0] = static_cast<int>(selected_indices.size());
	for (size_t i = 0; i < selected_indices.size(); i++)
	{
		result[i + 1] = selected_indices[i];
	}

	return result;
}

void GaussianSplattingRenderer::RemoveSelectedPoints(int model)
{
	const std::lock_guard<std::mutex> lock(cuda_mtx);

	size_t model_start = 0;
	size_t model_size = 0;
	bool found_model = false;

	for (const auto &m : models)
	{
		if (m.index == model)
		{
			model_size = m.size;
			found_model = true;
			break;
		}
		model_start += m.size;
	}

	if (!found_model)
	{
		debug_log << "Model " << model << " not found" << std::endl;
		return;
	}

	std::vector<float> isSelected_host(count);
	CUDA_SAFE_CALL(cudaMemcpy(isSelected_host.data(), isSelected_cuda, count * sizeof(float), cudaMemcpyDeviceToHost));

	size_t new_count = 0;
	std::vector<size_t> keep_indices;

	for (size_t i = 0; i < model_start; i++)
	{
		keep_indices.push_back(i);
		new_count++;
	}

	for (size_t i = model_start; i < model_start + model_size; i++)
	{
		if (isSelected_host[i] < 0.5f)
		{
			keep_indices.push_back(i);
			new_count++;
		}
	}

	for (size_t i = model_start + model_size; i < count; i++)
	{
		keep_indices.push_back(i);
		new_count++;
	}

	if (new_count == count)
		return;

	pos_cuda = remove_point_cuda<Pos>(pos_cuda, new_count, keep_indices);
	rot_cuda = remove_point_cuda<Rot>(rot_cuda, new_count, keep_indices);
	shs_cuda = remove_point_cuda<SHs<3>>(shs_cuda, new_count, keep_indices);
	opacity_cuda = remove_point_cuda<float>(opacity_cuda, new_count, keep_indices);
	scale_cuda = remove_point_cuda<Scale>(scale_cuda, new_count, keep_indices);
	isSelected_cuda = remove_point_cuda<float>(isSelected_cuda, new_count, keep_indices);
	for (auto &m : models)
	{
		if (m.index == model)
		{
			m.size = m.size - (count - new_count);
			break;
		}
	}

	count = new_count;
	AllocateRenderContexts();
}
void GaussianSplattingRenderer::RemoveLastModel()
{
	RemoveModel(model_idx);
}
const char *GaussianSplattingRenderer::SplitSelectedPoints(int model)
{
	const std::lock_guard<std::mutex> lock(cuda_mtx);

	std::vector<float> isSelected_host(count);
	CUDA_SAFE_CALL(cudaMemcpy(isSelected_host.data(), isSelected_cuda, count * sizeof(float), cudaMemcpyDeviceToHost));
	debug_log << "count: " << count << std::endl;
	size_t new_count = 0;
	std::vector<size_t> keep_indices;
	std::vector<size_t> splited_indices;

	size_t model_start = 0;
	size_t model_size = 0;
	bool found_model = false;

	for (const auto &m : models)
	{
		if (m.index == model)
		{
			model_size = m.size;
			found_model = true;
			break;
		}
		model_start += m.size;
	}

	if (!found_model)
	{
		debug_log << "Model " << model << " not found" << std::endl;
		return "";
	}

	for (size_t i = 0; i < model_start; i++)
	{
		keep_indices.push_back(i);
		new_count++;
	}

	for (size_t i = model_start; i < model_start + model_size; i++)
	{
		if (isSelected_host[i] < 0.5f)
		{
			keep_indices.push_back(i);
			new_count++;
		}
	}

	for (size_t i = model_start + model_size; i < count; i++)
	{
		keep_indices.push_back(i);
		new_count++;
	}

	for (size_t i = model_start; i < model_start + model_size; i++)
	{
		if (isSelected_host[i] > 0.5f)
		{
			splited_indices.push_back(i);
		}
	}
	debug_log << "new_count: " << new_count << std::endl;
	if (new_count == count)
		return "";

	model_idx += 1;
	for (auto &m : models)
	{
		if (m.index == model)
		{
			debug_log << "m.id: " << m.index << std::endl;
			debug_log << "m.size: " << m.size << std::endl;
			m.size = m.size - (count - new_count);
			debug_log << "m.size: " << m.size << std::endl;
			break;
		}
	}
	debug_log << "model_idx " << model_idx << std::endl;
	debug_log << "(int)splited_indices.size() " << (int)splited_indices.size() << std::endl;
	models.push_back({model_idx, (int)splited_indices.size(), true, _scenemin, _scenemax, _scenemin, _scenemax});
	pos_cuda = split_point_cuda<Pos>(pos_cuda, count, keep_indices, splited_indices);
	rot_cuda = split_point_cuda<Rot>(rot_cuda, count, keep_indices, splited_indices);
	shs_cuda = split_point_cuda<SHs<3>>(shs_cuda, count, keep_indices, splited_indices);
	opacity_cuda = split_point_cuda<float>(opacity_cuda, count, keep_indices, splited_indices);
	scale_cuda = split_point_cuda<Scale>(scale_cuda, count, keep_indices, splited_indices);
	isSelected_cuda = split_point_cuda<float>(isSelected_cuda, count, keep_indices, splited_indices);
	int splited_count = (int)splited_indices.size();
	int keep_count = (int)keep_indices.size();
	pos_host.resize(splited_count);
	rot_host.resize(splited_count);
	shs_host.resize(splited_count);
	opacity_host.resize(splited_count);
	scale_host.resize(splited_count);
	// cudaMemcpy(pos_host.data(), pos_cuda + keep_count * 3, splited_count * sizeof(Pos), cudaMemcpyDeviceToHost);
	// cudaMemcpy(rot_host.data(), rot_cuda + keep_count * 4, splited_count * sizeof(Rot), cudaMemcpyDeviceToHost);
	// cudaMemcpy(shs_host.data(), shs_cuda + keep_count * 48, splited_count * sizeof(SHs<3>), cudaMemcpyDeviceToHost);
	// cudaMemcpy(opacity_host.data(), opacity_cuda + keep_count, splited_count * sizeof(float), cudaMemcpyDeviceToHost);
	// cudaMemcpy(scale_host.data(), scale_cuda + keep_count * 3, splited_count * sizeof(Scale), cudaMemcpyDeviceToHost);

	CUDA_SAFE_CALL(cudaMemcpy(pos_host.data(), (char *)pos_cuda + keep_count * sizeof(Pos), splited_count * sizeof(Pos), cudaMemcpyDeviceToHost));
	CUDA_SAFE_CALL(cudaMemcpy(rot_host.data(), (char *)rot_cuda + keep_count * sizeof(Rot), splited_count * sizeof(Rot), cudaMemcpyDeviceToHost));
	CUDA_SAFE_CALL(cudaMemcpy(shs_host.data(), (char *)shs_cuda + keep_count * sizeof(SHs<3>), splited_count * sizeof(SHs<3>), cudaMemcpyDeviceToHost));
	CUDA_SAFE_CALL(cudaMemcpy(opacity_host.data(), (char *)opacity_cuda + keep_count * sizeof(float), splited_count * sizeof(float), cudaMemcpyDeviceToHost));
	CUDA_SAFE_CALL(cudaMemcpy(scale_host.data(), (char *)scale_cuda + keep_count * sizeof(Scale), splited_count * sizeof(Scale), cudaMemcpyDeviceToHost));
	debug_log << "write ply" << std::endl;
	// debug_log << "\nFirst 3 points data:" << std::endl;

	// for (int i = 0; i < std::min(3, splited_count); i++)
	// {

	// 	debug_log << "\nPoint " << i << ":" << std::endl;
	// 	// 中文
	// 	//  Position (Vector3f)
	// 	debug_log << "Position: ("
	// 			  << pos_host[i][0] << ", "
	// 			  << pos_host[i][1] << ", "
	// 			  << pos_host[i][2] << ")" << std::endl;

	// 	// Rotation (float[4])
	// 	debug_log << "Rotation: ("
	// 			  << rot_host[i].rot[0] << ", "
	// 			  << rot_host[i].rot[1] << ", "
	// 			  << rot_host[i].rot[2] << ", "
	// 			  << rot_host[i].rot[3] << ")" << std::endl;

	// 	// SH Coefficients (float array)
	// 	debug_log << "SH Coefficients:" << std::endl;
	// 	for (int j = 0; j < (3 + 1) * (3 + 1) * 3; j++)
	// 	{ // D=3, so 16*3=48 coefficients
	// 		debug_log << "  sh[" << j << "]: " << shs_host[i].shs[j] << std::endl;
	// 	}

	// 	// Opacity (float)
	// 	debug_log << "Opacity: " << opacity_host[i] << std::endl;

	// 	// Scale (float[3])
	// 	debug_log << "Scale: ("
	// 			  << scale_host[i].scale[0] << ", "
	// 			  << scale_host[i].scale[1] << ", "
	// 			  << scale_host[i].scale[2] << ")" << std::endl;
	// }
	// Get current time and format it
	auto now = std::chrono::system_clock::now();
	auto in_time_t = std::chrono::system_clock::to_time_t(now);
	std::stringstream ss;
	ss << std::put_time(std::localtime(&in_time_t), "%Y%m%d_%H%M%S");
	debug_log << "ss.str(): " << ss.str() << std::endl;
	// Construct the filename
	try
	{
		if (current_result)
		{
			debug_log << "delete current_result" << std::endl;
			// delete[] current_result;
			current_result = nullptr;
		}
		debug_log << "origin filename: " << std::endl;
		filename = std::string("outputs/splited_output_") + ss.str() + ".ply";
		current_result = new char[filename.length() + 1];
		strcpy(current_result, filename.c_str());
		debug_log << "Successfully created filename: " << current_result << std::endl;
	}
	catch (const std::bad_alloc &e)
	{
		debug_log << "Memory allocation failed during filename creation: " << e.what() << std::endl;
		return "";
	}
	catch (const std::length_error &e)
	{
		debug_log << "String length error during filename creation: " << e.what() << std::endl;
		return "";
	}
	catch (const std::exception &e)
	{
		debug_log << "Unexpected error during filename creation: " << e.what() << std::endl;
		return "";
	}
	catch (...)
	{

		debug_log << "Unknown error occurred during filename creation" << std::endl;
		return "";
	}
	if (pos_host.empty() || shs_host.empty() || opacity_host.empty() ||
		scale_host.empty() || rot_host.empty())
	{
		debug_log << "Error: Empty data vectors" << std::endl;
	}

	if (pos_host.size() != shs_host.size() ||
		pos_host.size() != opacity_host.size() ||
		pos_host.size() != scale_host.size() ||
		pos_host.size() != rot_host.size())
	{
		debug_log << "Error: Inconsistent vector sizes" << std::endl;
	}
	try
	{
		debug_log << filename << std::endl;
		writePly(current_result, pos_host, shs_host, opacity_host, scale_host, rot_host);
	}
	catch (const std::exception &e)
	{
		debug_log << "Error writing PLY file: " << e.what() << std::endl;
		return nullptr;
	}
	// Write splited data to PLY

	debug_log << "remove model" << std::endl;
	debug_log << "current_result " << current_result << std::endl;
	return current_result;
}

const char *GaussianSplattingRenderer::SaveAllModels()
{
	const std::lock_guard<std::mutex> lock(cuda_mtx);

	// Get current time for folder name
	auto now = std::chrono::system_clock::now();
	auto in_time_t = std::chrono::system_clock::to_time_t(now);
	std::stringstream folder_ss;
	folder_ss << "saved_projects/save_" << std::put_time(std::localtime(&in_time_t), "%Y%m%d_%H%M%S");

	// Create directories if they don't exist
	std::string folder_path = folder_ss.str();
	CREATE_DIR(folder_path.c_str());
	if (save_project_path != nullptr)
	{
		save_project_path = nullptr;
	}
	save_project_path = new char[folder_path.length() + 1];
	strcpy(save_project_path, folder_path.c_str());
	size_t start_idx = 0;
	for (const auto &model : models)
	{
		// Calculate model size and prepare vectors for this model's data
		size_t model_size = model.size;
		std::vector<Pos> model_pos(model_size);
		std::vector<SHs<3>> model_shs(model_size);
		std::vector<float> model_opacity(model_size);
		std::vector<Scale> model_scale(model_size);
		std::vector<Rot> model_rot(model_size);

		// Copy data from GPU to CPU for this model
		CUDA_SAFE_CALL(cudaMemcpy(model_pos.data(),
								  (char *)pos_cuda + start_idx * sizeof(Pos),
								  model_size * sizeof(Pos),
								  cudaMemcpyDeviceToHost));

		CUDA_SAFE_CALL(cudaMemcpy(model_shs.data(),
								  (char *)shs_cuda + start_idx * sizeof(SHs<3>),
								  model_size * sizeof(SHs<3>),
								  cudaMemcpyDeviceToHost));

		CUDA_SAFE_CALL(cudaMemcpy(model_opacity.data(),
								  (char *)opacity_cuda + start_idx * sizeof(float),
								  model_size * sizeof(float),
								  cudaMemcpyDeviceToHost));

		CUDA_SAFE_CALL(cudaMemcpy(model_scale.data(),
								  (char *)scale_cuda + start_idx * sizeof(Scale),
								  model_size * sizeof(Scale),
								  cudaMemcpyDeviceToHost));

		CUDA_SAFE_CALL(cudaMemcpy(model_rot.data(),
								  (char *)rot_cuda + start_idx * sizeof(Rot),
								  model_size * sizeof(Rot),
								  cudaMemcpyDeviceToHost));

		// Create filename for this model
		std::stringstream file_ss;
		file_ss << folder_path << "/" << model.index << ".ply";

		// Write the PLY file
		try
		{
			writePly(file_ss.str().c_str(),
					 model_pos,
					 model_shs,
					 model_opacity,
					 model_scale,
					 model_rot);

			debug_log << "Successfully saved model " << model.index
					  << " to " << file_ss.str() << std::endl;
		}
		catch (const std::exception &e)
		{
			debug_log << "Error saving model " << model.index << ": "
					  << e.what() << std::endl;
		}

		// Update start index for next model
		start_idx += model_size;
	}
	return save_project_path;
}
void GaussianSplattingRenderer::SaveModel(int model)
{
	const std::lock_guard<std::mutex> lock(cuda_mtx);

	// Find the specified model
	size_t model_start = 0;
	size_t model_size = 0;
	bool found_model = false;

	for (const auto &m : models)
	{
		if (m.index == model)
		{
			model_size = m.size;
			found_model = true;
			break;
		}
		model_start += m.size;
	}

	if (!found_model)
	{
		debug_log << "Model " << model << " not found" << std::endl;
		return;
	}

	// Create directories if they don't exist
	// CREATE_DIR("saved_preset");

	// Prepare vectors for this model's data
	std::vector<Pos> model_pos(model_size);
	std::vector<SHs<3>> model_shs(model_size);
	std::vector<float> model_opacity(model_size);
	std::vector<Scale> model_scale(model_size);
	std::vector<Rot> model_rot(model_size);

	// Copy data from GPU to CPU for this model
	CUDA_SAFE_CALL(cudaMemcpy(model_pos.data(),
							  (char *)pos_cuda + model_start * sizeof(Pos),
							  model_size * sizeof(Pos),
							  cudaMemcpyDeviceToHost));

	CUDA_SAFE_CALL(cudaMemcpy(model_shs.data(),
							  (char *)shs_cuda + model_start * sizeof(SHs<3>),
							  model_size * sizeof(SHs<3>),
							  cudaMemcpyDeviceToHost));

	CUDA_SAFE_CALL(cudaMemcpy(model_opacity.data(),
							  (char *)opacity_cuda + model_start * sizeof(float),
							  model_size * sizeof(float),
							  cudaMemcpyDeviceToHost));

	CUDA_SAFE_CALL(cudaMemcpy(model_scale.data(),
							  (char *)scale_cuda + model_start * sizeof(Scale),
							  model_size * sizeof(Scale),
							  cudaMemcpyDeviceToHost));

	CUDA_SAFE_CALL(cudaMemcpy(model_rot.data(),
							  (char *)rot_cuda + model_start * sizeof(Rot),
							  model_size * sizeof(Rot),
							  cudaMemcpyDeviceToHost));

	// Get current time for filename
	auto now = std::chrono::system_clock::now();
	auto in_time_t = std::chrono::system_clock::to_time_t(now);
	std::stringstream ss;
	ss << "saved_presets/preset_" << std::put_time(std::localtime(&in_time_t), "%Y%m%d_%H%M%S") << ".ply";

	// Store the filename for return
	if (current_result != nullptr)
	{
		// delete[] current_result;
		current_result = nullptr;
	}
	std::string filename = ss.str();
	current_result = new char[filename.length() + 1];
	strcpy(current_result, filename.c_str());

	// Write the PLY file
	try
	{
		writePly(current_result,
				 model_pos,
				 model_shs,
				 model_opacity,
				 model_scale,
				 model_rot);

		debug_log << "Successfully saved model " << model
				  << " to " << current_result << std::endl;
	}
	catch (const std::exception &e)
	{
		debug_log << "Error saving model " << model << ": "
				  << e.what() << std::endl;
		return;
	}

	// return current_result;
}
void GaussianSplattingRenderer::BeginColorAdjust()
{
	CUDA_SAFE_CALL(cudaFree(shs_origin_cuda));
	shs_origin_cuda = cuda_copy_memory(shs_cuda, count * 16 * 3 * sizeof(float));
}
void GaussianSplattingRenderer::ColorAdjustFromCuda(float *rArray, float *gArray, float *bArray)
{
	CUDA_SAFE_CALL(cudaMemcpy(rArray_cuda, rArray, sizeof(float) * 256, cudaMemcpyHostToDevice));
	CUDA_SAFE_CALL(cudaMemcpy(gArray_cuda, gArray, sizeof(float) * 256, cudaMemcpyHostToDevice));
	CUDA_SAFE_CALL(cudaMemcpy(bArray_cuda, bArray, sizeof(float) * 256, cudaMemcpyHostToDevice));
	float campos[3] = {0.0f, 0.0f, 0.0f};
	debug_log << "ColorAdjustFromCuda" << std::endl;

	CUDA_SAFE_CALL(cudaFree(shs_cuda));
	shs_cuda = ModifySelectedPointsColor(
		shs_origin_cuda,
		isSelected_cuda,
		pos_cuda,
		campos,
		rArray_cuda,
		gArray_cuda,
		bArray_cuda,
		count,
		_sh_degree,
		16);
}
void GaussianSplattingRenderer::EndColorAdjust()
{
	CUDA_SAFE_CALL(cudaFree(shs_origin_cuda));
}
void GaussianSplattingRenderer::CreateRenderContext(int idx)
{

	const std::lock_guard<std::mutex> lock(cuda_mtx);

	// Resize the buffers
	geom[idx] = new AllocFuncBuffer;
	binning[idx] = new AllocFuncBuffer;
	img[idx] = new AllocFuncBuffer;
	renData[idx] = new RenderData;

	// Alloc
	geom[idx]->bufferFunc = resizeFunctional(&geom[idx]->ptr, geom[idx]->allocd);
	binning[idx]->bufferFunc = resizeFunctional(&binning[idx]->ptr, binning[idx]->allocd);
	img[idx]->bufferFunc = resizeFunctional(&img[idx]->ptr, img[idx]->allocd);

	// Alloc cuda ressource for view model
	AllocateRenderContexts();
}

void GaussianSplattingRenderer::RemoveRenderContext(int idx)
{
	const std::lock_guard<std::mutex> lock(cuda_mtx);

	// freee cuda resources
	if (geom.at(idx)->ptr != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)geom.at(idx)->ptr));
	}
	if (binning.at(idx)->ptr != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)binning.at(idx)->ptr));
	}
	if (img.at(idx)->ptr != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)img.at(idx)->ptr));
	}

	geom.erase(idx);
	binning.erase(idx);
	img.erase(idx);

	if (renData.at(idx)->view_cuda != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)renData.at(idx)->view_cuda));
	}
	if (renData.at(idx)->proj_cuda != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)renData.at(idx)->proj_cuda));
	}
	if (renData.at(idx)->model_sz != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)renData.at(idx)->model_sz));
	}
	if (renData.at(idx)->model_active != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)renData.at(idx)->model_active));
	}
	if (renData.at(idx)->cam_pos_cuda != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)renData.at(idx)->cam_pos_cuda));
	}
	if (renData.at(idx)->boxmin != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)renData.at(idx)->boxmin));
	}
	if (renData.at(idx)->boxmax != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)renData.at(idx)->boxmax));
	}
	if (renData.at(idx)->frustums != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)renData.at(idx)->frustums));
	}
	if (renData.at(idx)->model_mat != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)renData.at(idx)->model_mat));
	}

	RenderData *data = renData.at(idx);
	renData.erase(idx);
	delete data;
}

void GaussianSplattingRenderer::AllocateRenderContexts()
{
	size_t nb_models = models.size();
	for (auto kv : renData)
	{
		RenderData *data = kv.second;
		// reallocate only if needed
		if (data->nb_model_allocated != nb_models)
		{
			data->nb_model_allocated = nb_models;

			// free last allocated ressources
			if (data->view_cuda != nullptr)
			{
				CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(data->view_cuda)));
			}
			if (data->proj_cuda != nullptr)
			{
				CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(data->proj_cuda)));
			}
			if (data->model_sz != nullptr)
			{
				CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(data->model_sz)));
			}
			if (data->model_active != nullptr)
			{
				CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(data->model_active)));
			}
			if (data->cam_pos_cuda != nullptr)
			{
				CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(data->cam_pos_cuda)));
			}
			if (data->boxmin != nullptr)
			{
				CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(data->boxmin)));
			}
			if (data->boxmax != nullptr)
			{
				CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(data->boxmax)));
			}
			if (data->frustums != nullptr)
			{
				CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(data->frustums)));
			}
			if (data->model_mat != nullptr)
			{
				CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(data->model_mat)));
			}

			// Create space for view parameters for each model
			CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(data->view_cuda), sizeof(Matrix4f) * nb_models));
			CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(data->proj_cuda), sizeof(Matrix4f) * nb_models));
			CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(data->model_sz), sizeof(int) * nb_models));
			CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(data->model_active), sizeof(int) * nb_models));
			CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(data->cam_pos_cuda), 3 * sizeof(float) * nb_models));
			CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(data->boxmin), 3 * sizeof(float) * nb_models));
			CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(data->boxmax), 3 * sizeof(float) * nb_models));
			CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(data->frustums), 6 * sizeof(float)));
			CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(data->model_mat), sizeof(Matrix4f) * nb_models));
		}
	}
	if (selectedColor_cuda != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(selectedColor_cuda)));
	}
	if (unselectedColor_cuda != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(unselectedColor_cuda)));
	}
	if (rArray_cuda != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(rArray_cuda)));
	}
	if (gArray_cuda != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(gArray_cuda)));
	}
	if (bArray_cuda != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(bArray_cuda)));
	}
	CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(selectedColor_cuda), sizeof(float) * 4));
	CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(unselectedColor_cuda), sizeof(float) * 4));
	CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(rArray_cuda), sizeof(float) * 256));
	CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(gArray_cuda), sizeof(float) * 256));
	CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(bArray_cuda), sizeof(float) * 256));
	if (selectionCenter != nullptr)
	{
		CUDA_SAFE_CALL_ALWAYS(cudaFree((void *)(selectionCenter)));
	}
	CUDA_SAFE_CALL_ALWAYS(cudaMalloc((void **)&(selectionCenter), sizeof(float) * 3));
}

void GaussianSplattingRenderer::SetActiveModel(int model, bool active)
{
	for (SplatModel &m : models)
	{
		if (m.index == model)
		{
			m.active = active;
		}
	}
}

void GaussianSplattingRenderer::SetEditParameters(int model, const Matrix4f &model_mat)
{

	models_mat[model] = model_mat;
}

void GaussianSplattingRenderer::Preprocess(int context, const std::map<int, Matrix4f> &view_mat, const std::map<int, Matrix4f> &proj_mat, const std::map<int, Vector3f> &position, Vector6f frumstums, float fovy, int width, int height)
{
	// view_mat.row(1) *= -1;
	// view_mat.row(2) *= -1;
	// proj_mat.row(1) *= -1;

	const std::lock_guard<std::mutex> lock(cuda_mtx);

	if (count == 0)
	{
		return;
	}

	float aspect_ratio = (float)width / (float)height;
	float tan_fovy = tan(fovy * 0.5f);
	float tan_fovx = tan_fovy * aspect_ratio;

	RenderData *rdata = renData.at(context);
	int nb_models = models.size();
	// debug_log << "nb_models: " << nb_models << std::endl;
	int midx = 0;
	for (const SplatModel &m : models)
	{
		int active = (m.active && view_mat.find(m.index) != view_mat.end()) ? 1 : 0;
		int msize = m.size;
		// debug_log << "rdata->model_sz " << msize << std::endl;
		// debug_log << "active: " << active << std::endl;
		CUDA_SAFE_CALL(cudaMemcpy((char *)(rdata->model_sz) + midx * sizeof(int), &msize, sizeof(int), cudaMemcpyHostToDevice));
		CUDA_SAFE_CALL(cudaMemcpy((char *)(rdata->model_active) + midx * sizeof(int), &active, sizeof(int), cudaMemcpyHostToDevice));
		CUDA_SAFE_CALL(cudaMemcpy((char *)(rdata->boxmin) + midx * sizeof(float) * 3, m._boxmin.data(), sizeof(float) * 3, cudaMemcpyHostToDevice));
		CUDA_SAFE_CALL(cudaMemcpy((char *)(rdata->boxmax) + midx * sizeof(float) * 3, m._boxmax.data(), sizeof(float) * 3, cudaMemcpyHostToDevice));
		if (active == 1)
		{

			CUDA_SAFE_CALL(cudaMemcpy((char *)(rdata->view_cuda) + midx * sizeof(Matrix4f), view_mat.at(m.index).data(), sizeof(Matrix4f), cudaMemcpyHostToDevice));
			CUDA_SAFE_CALL(cudaMemcpy((char *)(rdata->proj_cuda) + midx * sizeof(Matrix4f), proj_mat.at(m.index).data(), sizeof(Matrix4f), cudaMemcpyHostToDevice));
			CUDA_SAFE_CALL(cudaMemcpy((char *)(rdata->cam_pos_cuda) + midx * sizeof(float) * 3, position.at(m.index).data(), sizeof(float) * 3, cudaMemcpyHostToDevice));
			CUDA_SAFE_CALL(cudaMemcpy((char *)(rdata->model_mat) + midx * sizeof(Matrix4f), models_mat.at(m.index).data(), sizeof(Matrix4f), cudaMemcpyHostToDevice));
			// debug_log << "models_mat.at(m.index).data(): " << models_mat.at(m.index) << std::endl;
		}
		midx += 1;
	}
	CUDA_SAFE_CALL(cudaMemcpy((char *)(rdata->frustums), frumstums.data(), sizeof(float) * 6, cudaMemcpyHostToDevice));

	// Rasterize
	int *rects = _fastCulling ? rect_cuda : nullptr;
	rdata->num_rendered = CudaRasterizer::Rasterizer::forward_preprocess(
		geom.at(context)->bufferFunc,
		binning.at(context)->bufferFunc,
		img.at(context)->bufferFunc,
		count, _sh_degree, 16,
		background_cuda,
		width, height,
		pos_cuda,
		shs_cuda,
		nullptr,
		opacity_cuda,
		scale_cuda,
		_scalingModifier,
		rot_cuda,
		nullptr,
		rdata->view_cuda,
		rdata->proj_cuda,
		rdata->cam_pos_cuda,
		rdata->frustums,
		rdata->model_sz,
		rdata->model_active,
		nb_models,
		tan_fovx,
		tan_fovy,
		false,
		nullptr,
		rects,
		rdata->boxmin,
		rdata->boxmax,
		selectionCenter,
		selectionRadius,
		isSelected_cuda,
		rdata->model_mat,
		isSelecting,
		isEraseSelection);
}

void GaussianSplattingRenderer::Render(int context, float *image_cuda, float *depth_cuda, cudaSurfaceObject_t camera_depth_cuda, float fovy, int width, int height)
{
	if (count > 0 && renData.at(context)->num_rendered > 0)
	{

		RenderData *rdata = renData.at(context);

		const std::lock_guard<std::mutex> lock(cuda_mtx);

		float aspect_ratio = (float)width / (float)height;
		float tan_fovy = tan(fovy * 0.5f);
		float tan_fovx = tan_fovy * aspect_ratio;

		int *rects = _fastCulling ? rect_cuda : nullptr;

		CudaRasterizer::Rasterizer::forward_render(
			geom.at(context)->bufferFunc,
			binning.at(context)->bufferFunc,
			img.at(context)->bufferFunc,
			count, _sh_degree, 16,
			background_cuda,
			camera_depth_cuda,
			width, height,
			pos_cuda,
			shs_cuda,
			nullptr,
			opacity_cuda,
			scale_cuda,
			_scalingModifier,
			rot_cuda,
			nullptr,
			rdata->view_cuda,
			rdata->proj_cuda,
			rdata->cam_pos_cuda,
			tan_fovx,
			tan_fovy,
			false,
			image_cuda,
			depth_cuda,
			nullptr,
			rects,
			rdata->boxmin,
			rdata->boxmax,
			rdata->num_rendered,
			selectedColor_cuda,
			unselectedColor_cuda,
			show_centers,
			isSelected_cuda,
			pointSize,
			depthcutoff);
	}
	else
	{
		CUDA_SAFE_CALL(cudaMemset(image_cuda, 0, sizeof(float) * 4 * width * height));
		CUDA_SAFE_CALL(cudaMemset(depth_cuda, 0, sizeof(float) * width * height));
	}
}
float inverseSigmoid(float value)
{
	return log(value / (1.0f - value));
}

void restoreSHs(const std::vector<SHs<3>> &shs,
				std::vector<SHs<3>> &restoredShs)
{
	int SH_N = (3 + 1) * (3 + 1); // Assuming D = 3
	restoredShs.resize(shs.size());

	for (size_t k = 0; k < shs.size(); ++k)
	{
		restoredShs[k].shs[0] = shs[k].shs[0];
		restoredShs[k].shs[1] = shs[k].shs[1];
		restoredShs[k].shs[2] = shs[k].shs[2];

		for (int j = 1; j < SH_N; ++j)
		{
			restoredShs[k].shs[(j - 1) + 3] = shs[k].shs[j * 3 + 0];
			restoredShs[k].shs[(j - 1) + SH_N + 2] = shs[k].shs[j * 3 + 1];
			restoredShs[k].shs[(j - 1) + 2 * SH_N + 1] = shs[k].shs[j * 3 + 2];
		}
	}
}

void writePly(const char *filename,
			  const std::vector<Pos> &pos,
			  const std::vector<SHs<3>> &shs,
			  const std::vector<float> &opacities,
			  const std::vector<Scale> &scales,
			  const std::vector<Rot> &rot)
{
	std::ofstream outfile(filename, std::ios_base::binary);

	if (!outfile.good())
	{
		throw std::runtime_error("Unable to open file for writing: " + std::string(filename));
	}

	// Write PLY header
	outfile << "ply\n";
	outfile << "format binary_little_endian 1.0\n";
	outfile << "element vertex " << pos.size() << "\n";
	outfile << "property float x\n";
	outfile << "property float y\n";
	outfile << "property float z\n";
	outfile << "property float nx\n";
	outfile << "property float ny\n";
	outfile << "property float nz\n";
	outfile << "property float f_dc_0\n";
	outfile << "property float f_dc_1\n";
	outfile << "property float f_dc_2\n";
	for (int i = 0; i < 45; ++i)
	{
		outfile << "property float f_rest_" << i << "\n";
	}
	outfile << "property float opacity\n";
	outfile << "property float scale_0\n";
	outfile << "property float scale_1\n";
	outfile << "property float scale_2\n";
	outfile << "property float rot_0\n";
	outfile << "property float rot_1\n";
	outfile << "property float rot_2\n";
	outfile << "property float rot_3\n";
	outfile << "end_header\n";

	// Default normal vector
	float defaultNormal[3] = {0.0f, 0.0f, 1.0f};

	// Initialize restored data
	std::vector<SHs<3>> restoredShs;
	std::vector<float> restoredOpacities(opacities.size());
	std::vector<Scale> restoredScales(scales.size());
	std::vector<Rot> restoredRot = rot; // 假设旋转不需要还原

	// Restore SH coefficients
	restoreSHs(shs, restoredShs);

	// Restore scales
	for (size_t i = 0; i < scales.size(); ++i)
	{
		for (int j = 0; j < 3; ++j)
		{
			restoredScales[i].scale[j] = log(scales[i].scale[j]);
		}
	}

	// Restore opacities
	for (size_t i = 0; i < opacities.size(); ++i)
	{
		restoredOpacities[i] = inverseSigmoid(opacities[i]);
	}

	// Write PLY data
	for (size_t i = 0; i < pos.size(); ++i)
	{
		outfile.write(reinterpret_cast<const char *>(&pos[i]), sizeof(Pos));
		outfile.write(reinterpret_cast<const char *>(defaultNormal), sizeof(float) * 3);
		outfile.write(reinterpret_cast<const char *>(&restoredShs[i]), sizeof(SHs<3>));
		outfile.write(reinterpret_cast<const char *>(&restoredOpacities[i]), sizeof(float));
		outfile.write(reinterpret_cast<const char *>(&restoredScales[i]), sizeof(Scale));
		outfile.write(reinterpret_cast<const char *>(&restoredRot[i]), sizeof(Rot));
	}

	outfile.close();
}
// Load the Gaussians from the given file.
template <int D>
int loadPly(const char *filename,
			std::vector<Pos> &pos,
			std::vector<SHs<3>> &shs,
			std::vector<float> &opacities,
			std::vector<Scale> &scales,
			std::vector<Rot> &rot,
			std::vector<float> &isSelected,
			Vector3f &minn,
			Vector3f &maxx)
{

	std::ifstream infile(filename, std::ios_base::binary);

	if (!infile.good())
		throw std::runtime_error((stringstream() << "Unable to find model's PLY file, attempted:\n"
												 << filename)
									 .str());

	// "Parse" header (it has to be a specific format anyway)
	std::string buff;
	std::getline(infile, buff); // ply
	std::getline(infile, buff); // format binary_little_endian 1.0

	std::string dummy;
	std::getline(infile, buff); // element vertex 140647
	std::stringstream ss(buff);
	int lcount;
	ss >> dummy >> dummy >> lcount;

	while (std::getline(infile, buff))
		if (buff.compare("end_header") == 0)
			break;

	// Read all Gaussians at once (AoS)
	std::vector<RichPoint<D>> points(lcount);
	infile.read((char *)points.data(), lcount * sizeof(RichPoint<D>));

	// Resize our SoA data
	pos.resize(lcount);
	shs.resize(lcount);
	scales.resize(lcount);
	rot.resize(lcount);
	opacities.resize(lcount);
	isSelected.resize(lcount);
	for (int i = 0; i < lcount; i++)
	{
		isSelected[i] = 0;
	}
	// Gaussians are done training, they won't move anymore. Arrange
	// them according to 3D Morton order. This means better cache
	// behavior for reading Gaussians that end up in the same tile
	// (close in 3D --> close in 2D).
	minn = Vector3f(FLT_MAX, FLT_MAX, FLT_MAX);
	maxx = -minn;
	for (int i = 0; i < lcount; i++)
	{
		maxx = maxx.cwiseMax(points[i].pos);
		minn = minn.cwiseMin(points[i].pos);
	}
	// std::vector<std::pair<uint64_t, int>> mapp(lcount);
	// for (int i = 0; i < lcount; i++)
	// {
	// 	Vector3f rel = (points[i].pos - minn).array() / (maxx - minn).array();
	// 	Vector3f scaled = ((float((1 << 21) - 1)) * rel);
	// 	Vector3i xyz = scaled.cast<int>();

	// 	uint64_t code = 0;
	// 	for (int i = 0; i < 21; i++)
	// 	{
	// 		code |= ((uint64_t(xyz.x() & (1 << i))) << (2 * i + 0));
	// 		code |= ((uint64_t(xyz.y() & (1 << i))) << (2 * i + 1));
	// 		code |= ((uint64_t(xyz.z() & (1 << i))) << (2 * i + 2));
	// 	}

	// 	mapp[i].first = code;
	// 	mapp[i].second = i;
	// }
	// auto sorter = [](const std::pair<uint64_t, int> &a, const std::pair<uint64_t, int> &b)
	// {
	// 	return a.first < b.first;
	// };
	// std::sort(mapp.begin(), mapp.end(), sorter);

	// Move data from AoS to SoA
	int SH_N = (D + 1) * (D + 1);
	for (int k = 0; k < lcount; k++)
	{
		// int i = mapp[k].second;
		int i = k;
		pos[k] = points[i].pos;

		// Normalize quaternion
		float length2 = 0;
		for (int j = 0; j < 4; j++)
			length2 += points[i].rot.rot[j] * points[i].rot.rot[j];
		float length = sqrt(length2);
		for (int j = 0; j < 4; j++)
			rot[k].rot[j] = points[i].rot.rot[j] / length;

		// Exponentiate scale
		for (int j = 0; j < 3; j++)
			scales[k].scale[j] = exp(points[i].scale.scale[j]);

		// Activate alpha
		opacities[k] = sigmoid(points[i].opacity);

		shs[k].shs[0] = points[i].shs.shs[0];
		shs[k].shs[1] = points[i].shs.shs[1];
		shs[k].shs[2] = points[i].shs.shs[2];
		for (int j = 1; j < SH_N; j++)
		{
			shs[k].shs[j * 3 + 0] = points[i].shs.shs[(j - 1) + 3];
			shs[k].shs[j * 3 + 1] = points[i].shs.shs[(j - 1) + SH_N + 2];
			shs[k].shs[j * 3 + 2] = points[i].shs.shs[(j - 1) + 2 * SH_N + 1];
		}
	}
	// writePly("output_write.ply", pos, shs, opacities, scales, rot);
	return lcount;
}
