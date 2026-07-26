local gitbee_hash_input = os.getenv("GITBEE_VERSION") or ""
local gitbee_version = "1.0.0"
local gitbee_hash = gitbee_version
if gitbee_hash_input ~= "" then
    gitbee_version = gitbee_version .. "." .. gitbee_hash_input:sub(1, 10)
    gitbee_hash = gitbee_version
end
-- git hash is too long for NSIS VIFileVersion (needs X.X.X.X), so use a static version for metadata
set_version("1.0.0")
add_rules("mode.debug", "mode.release")

local has_volt = os.isfile("volt-ui/xmake.lua")
if has_volt then
    includes("volt-ui")
end

-- Auto-generate src/app_icon.h from bee.ico if Python is available
local function gen_app_icon()
    local ico = "bee.ico"
    local header = "src/app_icon.h"
    if os.isfile(ico) and os.isfile(header) then
        if os.isfile(ico) and os.isfile(header) and os.mtime(ico) <= os.mtime(header) then
            return
        end
    end
    if os.isfile(ico) then
        os.exec("python3 scripts/gen_icon_header.py 2>/dev/null || python scripts/gen_icon_header.py 2>/dev/null || true")
    end
end
gen_app_icon()

target("GitBee")
    set_kind("binary")
    set_languages("c++17")
    add_files("src/*.cpp")
    add_headerfiles("src/*.h")
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
        if os.isdir("scripts") then
            os.cp("scripts", target:targetdir())
        end
        if not is_plat("windows") then
            os.cp("assets/icons", target:targetdir())
            os.cp("assets/gitbee.desktop", target:targetdir())
        end
    end)

    if has_volt then
        add_deps("volt-ui")
        add_packages("libsdl3", "imgui")
        add_includedirs("src")
        add_includedirs("deps/libvterm/include")
        add_includedirs("deps/libvterm/src")
        add_files("src/app/*.cpp")
        add_files("src/ui/*.cpp")

        -- Terminal subsystem (explicit file list for control)
        add_files("src/terminal/TerminalEmulator.cpp")
        add_files("src/terminal/TerminalTab.cpp")
        add_files("src/terminal/TerminalManager.cpp")
        add_files("src/terminal/LocalPty.cpp")
        add_files("src/terminal/SshPty.cpp")
        add_files("src/terminal/KeyMapping.cpp")
        add_files("src/terminal/ConnectionStore.cpp")
        -- libvterm (C library, compiled as C)
        add_files("deps/libvterm/src/*.c")
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
                local name = path.filename(fp)
                if name ~= "test_gitcore.exe" and path.extension(fp) ~= ".lib" then
                    package:add("installfiles", fp, {rootdir = dir})
                end
            end
            -- Also include scripts directory
            local scriptsDir = path.join(dir, "scripts")
            if os.isdir(scriptsDir) then
                for _, fp in ipairs(os.files(path.join(scriptsDir, "**"))) do
                    package:add("installfiles", fp, {rootdir = dir})
                end
            end
        end)
end

if is_plat("macosx") then
    xpack("GitBee")
        set_formats("app", "dmg")
        set_basename("GitBee-" .. gitbee_hash)
        set_title("GitBee")
        set_author("GitBee")
        set_description("A GUI client for Git")
        set_homepage("https://github.com/wanderer-code/GitBee")
        set_iconfile("assets/icons/gitbee.icns")
        before_package(function (package)
            import("core.project.config")
            local buildir = config.get("buildir") or "build"
            local plat = config.get("plat") or "macosx"
            local arch = config.get("arch") or "arm64"
            local dir = path.join(buildir, plat, arch, "release")

            -- Copy binary
            for _, fp in ipairs(os.files(path.join(dir, "GitBee"))) do
                package:add("installfiles", fp, {rootdir = dir, subdir = "MacOS"})
            end

            -- Copy Info.plist
            package:add("installfiles", "assets/Info.plist", {rootdir = "assets", subdir = ""})

            -- Copy icon
            package:add("installfiles", "assets/icons/gitbee.icns", {rootdir = "assets/icons", subdir = "Resources"})

            -- Copy fonts
            for _, fp in ipairs(os.files(path.join(dir, "fonts", "**"))) do
                package:add("installfiles", fp, {rootdir = dir, subdir = "Resources"})
            end

            -- Copy scripts
            local scriptsDir = path.join(dir, "scripts")
            if os.isdir(scriptsDir) then
                for _, fp in ipairs(os.files(path.join(scriptsDir, "**"))) do
                    package:add("installfiles", fp, {rootdir = dir, subdir = "Resources"})
                end
            end
        end)
end

if is_plat("linux") then
    xpack("GitBee")
        set_formats("tar.gz", "appimage")
        set_basename("GitBee-linux-" .. gitbee_hash)
        set_title("GitBee")
        set_author("GitBee")
        set_description("A GUI client for Git")
        set_homepage("https://github.com/wanderer-code/GitBee")
        before_package(function (package)
            import("core.project.config")
            local buildir = config.get("buildir") or "build"
            local plat = config.get("plat") or "linux"
            local arch = config.get("arch") or "x86_64"
            local dir = path.join(buildir, plat, arch, "release")

            -- Copy binary
            for _, fp in ipairs(os.files(path.join(dir, "GitBee"))) do
                package:add("installfiles", fp, {rootdir = dir})
            end

            -- Copy fonts
            for _, fp in ipairs(os.files(path.join(dir, "fonts", "**"))) do
                package:add("installfiles", fp, {rootdir = dir})
            end

            -- Copy scripts
            local scriptsDir = path.join(dir, "scripts")
            if os.isdir(scriptsDir) then
                for _, fp in ipairs(os.files(path.join(scriptsDir, "**"))) do
                    package:add("installfiles", fp, {rootdir = dir})
                end
            end

            -- Copy desktop file
            package:add("installfiles", "assets/gitbee.desktop", {rootdir = "assets"})

            -- Copy icons
            for _, fp in ipairs(os.files("assets/icons/hicolor/**")) do
                package:add("installfiles", fp, {rootdir = "assets"})
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
