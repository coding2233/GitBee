#ifndef __IMGUI_IMPL_SDL_OPENGL3_H__
#define __IMGUI_IMPL_SDL_OPENGL3_H__


#include <iostream>
#include <GL/glew.h>
#include <SDL.h>
#include <stdio.h>

#include "imgui.h"
#include "imgui_impl_sdl.h"
#include "imgui_impl_opengl3.h"

#include "lua.hpp"

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

#if _WIN32
#pragma comment(linker, "/export:luaL_newstate=luaL_newstate")
#pragma comment(linker, "/export:luaL_openlibs=luaL_openlibs")
#pragma comment(linker, "/export:lua_close=lua_close")
#pragma comment(linker, "/export:luaL_loadfile=luaL_loadfile")
#pragma comment(linker, "/export:lua_getfield=lua_getfield")


// basic stack manipulation
#pragma comment(linker, "/export:lua_gettop=lua_gettop")
#pragma comment(linker, "/export:lua_settop=lua_settop")
#pragma comment(linker, "/export:lua_pushvalue=lua_pushvalue")
#pragma comment(linker, "/export:lua_remove=lua_remove")
#pragma comment(linker, "/export:lua_insert=lua_insert")
#pragma comment(linker, "/export:lua_replace=lua_replace")
#pragma comment(linker, "/export:lua_checkstack=lua_checkstack")
#pragma comment(linker, "/export:lua_xmove=lua_xmove")


#pragma comment(linker, "/export:lua_call=lua_call")
#pragma comment(linker, "/export:lua_pcall=lua_pcall")
#pragma comment(linker, "/export:lua_cpcall=lua_cpcall")
#pragma comment(linker, "/export:lua_load=lua_load")

#pragma comment(linker, "/export:lua_dump=lua_dump")

#endif


#endif
