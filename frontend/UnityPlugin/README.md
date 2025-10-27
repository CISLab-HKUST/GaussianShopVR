# Gaussian Splatting Unity Plugin

Unity plugin with original Cuda Rasterizer.

## Compilation

Requirements: 
* Visual studio with c++ app development workload
* Cuda, don't forget to install visual studio integration, and update your nvidia driver if needed
* Cmake

Launch this command.
```sh
cmake -G "Visual Studio 17" . -B build
cmake --build build --config Release -j 4
```

## Installation

Copy `build/gaussiansplatting.dll` to unity project in `Assets\GaussianSplattingPlugin\Plugins`.
