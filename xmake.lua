add_rules("mode.debug", "mode.release")

local has_volt = os.isfile("volt-ui/xmake.lua")
if has_volt then
    includes("volt-ui")
end

target("gitbee")
    set_kind("binary")
    set_languages("c++17")
    add_files("src/*.cpp")
    add_files("src/gitcore/*.cpp")

    if has_volt then
        add_deps("volt-ui")
        add_packages("libsdl3", "imgui")
        add_files("src/app/*.cpp")
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
