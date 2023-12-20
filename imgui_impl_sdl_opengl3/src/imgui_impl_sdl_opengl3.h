#ifndef __IMGUI_IMPL_SDL_OPENGL3_H__
#define __IMGUI_IMPL_SDL_OPENGL3_H__


#include <iostream>
#include <GL/glew.h>
#include <SDL.h>
#include <stdio.h>

#include "imgui.h"
#include "imgui_impl_sdl.h"
#include "imgui_impl_opengl3.h"


#include "imgui_markdown.h"

#include "iiso3_export.h"


typedef struct ImGuiContext* (*IMGUI_INIT_CALLBACK)();
typedef void (*IMGUI_DRAW_CALLBACK)();
typedef void (*SDL_EVENT_CALLBACK)(int type,void* data);
// typedef void (*IMGUI_Free_CALLBACK)();

EXPORT_API SDL_Window* CreateSdlWindow(const char* title, int window_width, int window_height, Uint32 window_flags);
EXPORT_API int CreateRender(SDL_Window* window, IMGUI_INIT_CALLBACK imgui_init_cb, IMGUI_DRAW_CALLBACK imgui_draw_cb, SDL_EVENT_CALLBACK sdl_event_cb);
EXPORT_API void SDLSetWindowShow(SDL_Window* sdl_window);

// EXPORT_API void RenderDrawData(struct ImDrawData* draw_data);

//https://github.com/ocornut/imgui/wiki/Image-Loading-and-Displaying-Examples
//https://github.com/nothings/stb
//image loading/decoding from file/memory: JPG, PNG, TGA, BMP, PSD, GIF, HDR, PIC
EXPORT_API bool LoadTextureFromFile(const char* filename, GLuint* out_texture, int* out_width, int* out_height);
EXPORT_API bool LoadTextureFromMemory(const unsigned char* buffer, int size, GLuint* out_texture, int* out_width, int* out_height);
EXPORT_API void DeleteTexture(GLuint* out_texture);

typedef void MARKDOWN_HEADING_CALLBACK(int level,bool start);
EXPORT_API void MarkDown(const char* text, size_t size, ImGui::MarkdownImageCallback* image_callback, MARKDOWN_HEADING_CALLBACK* heading_callback);


EXPORT_API void SetClipboard(const char* text);

EXPORT_API bool ImGuiSpinner(const char* label, float radius, int thickness);


#endif
