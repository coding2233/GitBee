add_requires("conan::sdl/2.26.0",{alias="sdl"})
add_requires("conan::imgui/cci.20220621+1.88.docking",{alias="imgui"})
add_requires("conan::glew/2.2.0",{alias="glew"})
add_requires("conan::opengl/system",{alias="opengl"})
-- add_requires("stb")
-- add_requires("luajit")

target("iiso3")
    set_kind("shared")
    set_languages("cxx17")
    -- add_defines("LUA_BUILD_AS_DLL","LUA_LIB")
    add_files("src/*.cpp")
    -- add_includedirs("imgui/include")
    add_packages("sdl","glew","opengl","imgui")

target("test")
    set_kind("binary")
    set_languages("cxx17")
    add_files("test/main.cpp")
    add_includedirs("src")
    add_packages("sdl","glew","opengl","imgui")
    add_deps("iiso3")

-- 生成clion可识别的 compile_commands
-- xmake project -k compile_commands
-- xmake f -p windows -a x64