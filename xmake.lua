includes("volt-ui")

add_rules("mode.debug", "mode.release")

target("gitbee")
    set_kind("binary")
    set_languages("c++17")
    add_deps("volt-ui")
    add_packages("libsdl3", "imgui")
    add_files("src/*.cpp")
    add_files("src/app/*.cpp")

    if is_mode("debug") then
        set_symbols("debug")
        set_optimize("none")
    elseif is_mode("release") then
        set_optimize("fast")
    end
