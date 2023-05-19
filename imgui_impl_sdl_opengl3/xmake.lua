add_requires("libsdl",{alias="sdl"})
--add_requires("imgui v1.89.5",{configs = {opengl3=true,sdl2_no_renderer=true}})
add_requires("glew")
add_requires("opengl")
add_requires("luajit",{configs = {shared = true}})
add_requires("libgit2", {configs = {shared = true}})
-- add_requires("stb")
-- add_requires("luajit")

target("iiso3")
    set_kind("shared")
    set_arch("x64")
-- add_defines("LUA_BUILD_AS_DLL","LUA_LIB")
    add_files("imgui/**.cpp", "src/*.cpp")
    add_includedirs("imgui","imgui/backends")
    add_packages("sdl","glew","opengl","luajit","libgit2")

--
target("test")
    set_kind("binary")
    set_arch("x64")
    add_files("test/main.cpp")
    add_includedirs("imgui","imgui/backends","src")
    add_packages("sdl","glew","opengl","luajit","libgit2")
    add_deps("iiso3")


