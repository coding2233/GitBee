#ifndef __IMGUI_IMPL_SDL_OPENGL3_H__
#define __IMGUI_IMPL_SDL_OPENGL3_H__


#include <iostream>
#include <GL/glew.h>
#include <SDL.h>
#include <stdio.h>

#include "imgui.h"
#include "imgui_impl_sdl.h"
#include "imgui_impl_opengl3.h"

#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"
#include "imgui_markdown.h"

#include "lua.hpp"

#if defined _WIN32 || defined __CYGWIN__
#include "windows.h"
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
typedef void (*WINDOW_EVENT_CALLBACK)(int event_type);
// typedef void (*IMGUI_Free_CALLBACK)();

EXPORT_API SDL_Window* CreateSdlWindow(const char* title, int window_width, int window_height, Uint32 window_flags);
EXPORT_API int CreateRender(SDL_Window* window, IMGUI_INIT_CALLBACK imgui_init_cb, IMGUI_DRAW_CALLBACK imgui_draw_cb, WINDOW_EVENT_CALLBACK window_event_cb);
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

#if _WIN32
//luaxlib
#pragma comment(linker, "/export:luaL_newstate=luaL_newstate")
#pragma comment(linker, "/export:luaL_openlibs=luaL_openlibs")
#pragma comment(linker, "/export:lua_close=lua_close")
#pragma comment(linker, "/export:luaL_loadfile=luaL_loadfile")
#pragma comment(linker, "/export:luaL_loadbuffer=luaL_loadbuffer")
#pragma comment(linker, "/export:luaL_loadstring=luaL_loadstring")


// basic stack manipulation
#pragma comment(linker, "/export:lua_gettop=lua_gettop")
#pragma comment(linker, "/export:lua_settop=lua_settop")
#pragma comment(linker, "/export:lua_pushvalue=lua_pushvalue")
#pragma comment(linker, "/export:lua_remove=lua_remove")
#pragma comment(linker, "/export:lua_insert=lua_insert")
#pragma comment(linker, "/export:lua_replace=lua_replace")
#pragma comment(linker, "/export:lua_checkstack=lua_checkstack")
#pragma comment(linker, "/export:lua_xmove=lua_xmove")

//access functions (stack -> C)
#pragma comment(linker, "/export:lua_isnumber=lua_isnumber")
#pragma comment(linker, "/export:lua_isstring=lua_isstring")
#pragma comment(linker, "/export:lua_iscfunction=lua_iscfunction")
#pragma comment(linker, "/export:lua_isuserdata=lua_isuserdata")
#pragma comment(linker, "/export:lua_type=lua_type")
#pragma comment(linker, "/export:lua_typename=lua_typename")

#pragma comment(linker, "/export:lua_equal=lua_equal")
#pragma comment(linker, "/export:lua_rawequal=lua_rawequal")
#pragma comment(linker, "/export:lua_lessthan=lua_lessthan")

#pragma comment(linker, "/export:lua_tonumber=lua_tonumber")
#pragma comment(linker, "/export:lua_tointeger=lua_tointeger")
#pragma comment(linker, "/export:lua_toboolean=lua_toboolean")
#pragma comment(linker, "/export:lua_tolstring=lua_tolstring")
#pragma comment(linker, "/export:lua_objlen=lua_objlen")
#pragma comment(linker, "/export:lua_tocfunction=lua_tocfunction")
#pragma comment(linker, "/export:lua_touserdata=lua_touserdata")
#pragma comment(linker, "/export:lua_tothread=lua_tothread")
#pragma comment(linker, "/export:lua_topointer=lua_topointer")

//push functions (C -> stack)
#pragma comment(linker, "/export:lua_pushnil=lua_pushnil")
#pragma comment(linker, "/export:lua_pushnumber=lua_pushnumber")
#pragma comment(linker, "/export:lua_pushinteger=lua_pushinteger")
#pragma comment(linker, "/export:lua_pushlstring=lua_pushlstring")
#pragma comment(linker, "/export:lua_pushstring=lua_pushstring")
#pragma comment(linker, "/export:lua_pushvfstring=lua_pushvfstring")

#pragma comment(linker, "/export:lua_pushfstring=lua_pushfstring")
#pragma comment(linker, "/export:lua_pushcclosure=lua_pushcclosure")
#pragma comment(linker, "/export:lua_pushboolean=lua_pushboolean")
#pragma comment(linker, "/export:lua_pushlightuserdata=lua_pushlightuserdata")
#pragma comment(linker, "/export:lua_pushthread=lua_pushthread")

// get functions (Lua -> stack)
#pragma comment(linker, "/export:lua_gettable=lua_gettable")
#pragma comment(linker, "/export:lua_getfield=lua_getfield")
#pragma comment(linker, "/export:lua_rawget=lua_rawget")
#pragma comment(linker, "/export:lua_rawgeti=lua_rawgeti")
#pragma comment(linker, "/export:lua_createtable=lua_createtable")
#pragma comment(linker, "/export:lua_newuserdata=lua_newuserdata")
#pragma comment(linker, "/export:lua_getmetatable=lua_getmetatable")
#pragma comment(linker, "/export:lua_getfenv=lua_getfenv")

// set functions (stack -> Lua)
#pragma comment(linker, "/export:lua_settable=lua_settable")
#pragma comment(linker, "/export:lua_setfield=lua_setfield")
#pragma comment(linker, "/export:lua_rawset=lua_rawset")
#pragma comment(linker, "/export:lua_rawseti=lua_rawseti")
#pragma comment(linker, "/export:lua_setmetatable=lua_setmetatable")
#pragma comment(linker, "/export:lua_setfenv=lua_setfenv")

// `load' and `call' functions (load and run Lua code)
#pragma comment(linker, "/export:lua_call=lua_call")
#pragma comment(linker, "/export:lua_pcall=lua_pcall")
#pragma comment(linker, "/export:lua_cpcall=lua_cpcall")
#pragma comment(linker, "/export:lua_load=lua_load")

#pragma comment(linker, "/export:lua_dump=lua_dump")

//coroutine functions
#pragma comment(linker, "/export:lua_yield=lua_yield")
#pragma comment(linker, "/export:lua_resume=lua_resume")
#pragma comment(linker, "/export:lua_status=lua_status")

#endif


#endif
