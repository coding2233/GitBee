set_version("1.0.0")
add_rules("mode.debug", "mode.release")

local has_volt = os.isfile("volt-ui/xmake.lua")
if has_volt then
    includes("volt-ui")
end

target("GitBee")
    set_kind("binary")
    set_languages("c++17")
    add_files("src/*.cpp")
    add_files("src/gitcore/*.cpp")
    if is_plat("windows") then
        add_files("src/gitbee.rc")
    end

    after_build(function (target)
        os.cp("fonts", target:targetdir())
    end)

    if has_volt then
        add_deps("volt-ui")
        add_packages("libsdl3", "imgui")
        add_includedirs("src")
        add_files("src/app/*.cpp")
        add_files("src/ui/*.cpp")
    end

    if is_mode("debug") then
        set_symbols("debug")
        set_optimize("none")
    elseif is_mode("release") then
        set_optimize("fast")
    end

target("test_gitcore")
    set_kind("binary")
    set_languages("c++17")
    add_includedirs("src/gitcore")
    add_files("src/gitcore/*.cpp")
    add_files("tests/*.cpp")

    if is_mode("debug") then
        set_symbols("debug")
        set_optimize("none")
    end

includes("@builtin/xpack")

if is_plat("windows") then
    xpack("GitBee")
        set_formats("nsis")
        set_basename("GitBee-installer")
        set_title("GitBee")
        set_author("GitBee")
        set_description("A GUI client for Git")
        set_homepage("https://github.com/wanderer-code/GitBee")
        set_iconfile("bee.ico")
        before_package(function (package)
            import("core.project.config")
            local buildir = config.get("buildir") or "build"
            local plat = config.get("plat") or "windows"
            local arch = config.get("arch") or "x64"
            local dir = path.join(buildir, plat, arch, "release")
            for _, fp in ipairs(os.files(path.join(dir, "**"))) do
                package:add("installfiles", fp, {rootdir = dir})
            end
        end)
end
