#ifndef __IMGUI_IMPL_SDL_OPENGL3_H__
#define __IMGUI_IMPL_SDL_OPENGL3_H__

#include <iostream>
#include <GL/glew.h>
#include <SDL.h>
#include <stdio.h>

#include "imgui.h"
#include "imgui_impl_sdl.h"
#include "imgui_impl_opengl3.h"

#if defined _WIN32 || defined __CYGWIN__
#define API __declspec(dllexport)
#elif __GNUC__
#define API  __attribute__((__visibility__("default")))
#else
#define API
#endif

#if defined __cplusplus
#define EXTERN extern "C"
#else
#define EXTERN extern
#endif

#define EXPORT_API EXTERN API

typedef struct ImGuiContext* (*IMGUI_INIT_CALLBACK)();
typedef void (*IMGUI_DRAW_CALLBACK)();
// typedef void (*IMGUI_Free_CALLBACK)();

EXPORT_API int Create(const char* title,IMGUI_INIT_CALLBACK imgui_init_cb,IMGUI_DRAW_CALLBACK imgui_draw_cb);
// EXPORT_API void RenderDrawData(struct ImDrawData* draw_data);

#endif
