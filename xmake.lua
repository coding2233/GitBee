local gitbee_version = os.getenv("GITBEE_VERSION") or "1.0.0"
-- git hash is too long for NSIS VIFileVersion (needs X.X.X.X), so use a static version for metadata
set_version("1.0.0")
-- full git hash used for filename and GITBEE_VERSION define
local gitbee_hash = gitbee_version
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
    add_files("src/update/*.cpp")
    if is_plat("windows") then
        add_files("src/gitbee.rc")
    end

    add_defines('GITBEE_VERSION="' .. gitbee_hash .. '"')

    if is_plat("windows") then
        add_syslinks("winhttp", "urlmon")
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
        set_basename("GitBee-installer-" .. gitbee_hash)
        set_title("GitBee")
        set_author("GitBee")
        set_description("A GUI client for Git")
        set_homepage("https://github.com/wanderer-code/GitBee")
        set_iconfile("bee.ico")
        add_components("DesktopShortcut", "StartMenuShortcut", "LaunchAfterInstall")
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

xpack_component("DesktopShortcut")
    set_default(true)
    set_title("Create Desktop Shortcut")
    set_description("Create a shortcut to GitBee on your desktop")
    on_installcmd(function (component, batchcmds)
        batchcmds:rawcmd("nsis", [[
  SetOutPath "$INSTDIR"
  CreateShortCut "$DESKTOP\GitBee.lnk" "$INSTDIR\GitBee.exe"
]])
    end)
    on_uninstallcmd(function (component, batchcmds)
        batchcmds:rawcmd("nsis", [[
  Delete "$DESKTOP\GitBee.lnk"
]])
    end)

xpack_component("StartMenuShortcut")
    set_default(true)
    set_title("Create Start Menu Shortcut")
    set_description("Add GitBee shortcut to the Start Menu")
    on_installcmd(function (component, batchcmds)
        batchcmds:rawcmd("nsis", [[
  CreateDirectory "$SMPROGRAMS\GitBee"
  CreateShortCut "$SMPROGRAMS\GitBee\GitBee.lnk" "$INSTDIR\GitBee.exe"
]])
    end)
    on_uninstallcmd(function (component, batchcmds)
        batchcmds:rawcmd("nsis", [[
  RMDir /r "$SMPROGRAMS\GitBee"
]])
    end)

xpack_component("LaunchAfterInstall")
    set_default(true)
    set_title("Run GitBee after installation")
    set_description("Launch GitBee after the installer finishes")
    after_installcmd(function (component, batchcmds)
        batchcmds:rawcmd("nsis", [[
  ExecShell "" "$INSTDIR\GitBee.exe"
]])
    end)
