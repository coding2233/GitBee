#include "git_process.h"
#include "../dbg_log.h"

#include <cstdio>
#include <cstring>
#include <sstream>
#include <fstream>
#include <cstdlib>
#include <filesystem>
#include <thread>

#ifndef _WIN32
#include <unistd.h>
#endif

#ifdef _WIN32
#include <windows.h>
#else
#include <sys/wait.h>
#include <unistd.h>
#endif

static std::string EscapePath(const std::string& path)
{
    std::string escaped = path;
    size_t pos = 0;
    while ((pos = escaped.find('\"', pos)) != std::string::npos) {
        escaped.replace(pos, 1, "\\\"");
        pos += 2;
    }
    return escaped;
}

#ifdef _WIN32

static std::string s_gitExe;

static const char* GetGitExe()
{
    if (!s_gitExe.empty()) return s_gitExe.c_str();
    const char* known[] = {
        "C:\\Program Files\\Git\\bin\\git.exe",
        "C:\\Program Files (x86)\\Git\\bin\\git.exe",
        "C:\\Git\\bin\\git.exe",
    };
    for (auto* p : known) {
        if (GetFileAttributesA(p) != INVALID_FILE_ATTRIBUTES) {
            s_gitExe = p;
            return s_gitExe.c_str();
        }
    }
    s_gitExe = "git.exe";
    return s_gitExe.c_str();
}

static std::wstring Utf8ToWide(const std::string& s)
{
    int len = MultiByteToWideChar(CP_UTF8, 0, s.c_str(), -1, NULL, 0);
    if (len <= 0) return {};
    std::wstring ws(len - 1, 0);
    MultiByteToWideChar(CP_UTF8, 0, s.c_str(), -1, &ws[0], len);
    return ws;
}

static GitResult ExecGit(const std::string& repoPath,
                          const std::vector<std::string>& args)
{
    GitResult result;

    std::string cmdLine = "\"" + std::string(GetGitExe()) + "\" -C \"" +
                          EscapePath(repoPath) + "\"";
    for (auto& a : args) {
        cmdLine += " \"";
        cmdLine += EscapePath(a);
        cmdLine += "\"";
    }

    HANDLE hOutRd, hOutWr, hErrRd, hErrWr;
    SECURITY_ATTRIBUTES sa = { sizeof(sa), NULL, TRUE };
    CreatePipe(&hOutRd, &hOutWr, &sa, 0);
    CreatePipe(&hErrRd, &hErrWr, &sa, 0);

    STARTUPINFOW si = { sizeof(si) };
    si.dwFlags = STARTF_USESTDHANDLES;
    si.hStdOutput = hOutWr;
    si.hStdError  = hErrWr;

    PROCESS_INFORMATION pi = {};
    std::wstring wCmdLine = Utf8ToWide(cmdLine);

    if (!CreateProcessW(NULL, &wCmdLine[0],
                        NULL, NULL, TRUE,
                        CREATE_NO_WINDOW, NULL, NULL, &si, &pi))
    {
        CloseHandle(hOutRd); CloseHandle(hOutWr);
        CloseHandle(hErrRd); CloseHandle(hErrWr);
        DWORD gle = GetLastError();
        result.err = "Git is not installed or not found in PATH";
        LOG_ERROR("CreateProcessW failed (gle=%lu) cmd=%s", gle, cmdLine.c_str());
        return result;
    }

    CloseHandle(hOutWr);
    CloseHandle(hErrWr);

    char buf[4096];
    DWORD n;
    while (ReadFile(hOutRd, buf, sizeof(buf), &n, NULL) && n > 0)
        result.out.append(buf, n);
    CloseHandle(hOutRd);

    while (ReadFile(hErrRd, buf, sizeof(buf), &n, NULL) && n > 0)
        result.err.append(buf, n);
    CloseHandle(hErrRd);

    WaitForSingleObject(pi.hProcess, INFINITE);
    DWORD exitCode;
    GetExitCodeProcess(pi.hProcess, &exitCode);
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);

    result.ok = (exitCode == 0);

    while (!result.out.empty() &&
           (result.out.back() == '\n' || result.out.back() == '\r'))
        result.out.pop_back();

    LOG_DEBUG("[%s] git %s  (in %s)",
              result.ok ? "OK" : "FAIL",
              cmdLine.c_str(),
              repoPath.empty() ? "<global>" : repoPath.c_str());
    if (!result.ok && !result.err.empty()) {
        LOG_WARN("  stderr: %s", result.err.c_str());
    }

    return result;
}

#endif // _WIN32

#ifndef _WIN32

static std::string GetTempDir()
{
    const char* tmp = std::getenv("TMPDIR");
    if (!tmp) tmp = "/tmp";
    return tmp;
}

static std::string MakeErrorPath()
{
    static int counter = 0;
    counter++;
    return GetTempDir() + "/gitbee_err_" + std::to_string(getpid()) +
           "_" + std::to_string(counter) + ".txt";
}

static std::string BuildCommand(const std::string& repoPath,
                                const std::vector<std::string>& args,
                                const std::string& errPath)
{
    std::string escapedPath = EscapePath(repoPath);
    std::string cmd = "LC_ALL=C git -C \"" + escapedPath + "\"";
    for (const auto& arg : args) {
        cmd += " \"";
        cmd += EscapePath(arg);
        cmd += "\"";
    }
    cmd += " 2>\"" + errPath + "\"";
    return cmd;
}

#endif

GitResult GitProcess::Execute(const std::string& repoPath,
                              const std::vector<std::string>& args)
{
#ifdef _WIN32
    return ExecGit(repoPath, args);
#else
    GitResult result;
    std::string errPath = MakeErrorPath();
    std::string cmd = BuildCommand(repoPath, args, errPath);

    LOG_DEBUG("exec: %s", cmd.c_str());

    FILE* pipe = popen(cmd.c_str(), "r");
    if (!pipe) {
        int saved_errno = errno;
        if (saved_errno == ENOENT)
            result.err = "Git is not installed or not found in PATH";
        else
            result.err = std::string(strerror(saved_errno));
        LOG_ERROR("popen failed (errno=%d): %s", saved_errno, result.err.c_str());
        return result;
    }

    std::string output;
    char buf[4096];
    size_t n;
    while ((n = fread(buf, 1, sizeof(buf), pipe)) > 0)
        output.append(buf, n);

    int status = pclose(pipe);
    result.ok = (status != -1 && WIFEXITED(status) && WEXITSTATUS(status) == 0);

    std::ifstream errFile(errPath);
    if (errFile.is_open()) {
        std::stringstream errBuf;
        errBuf << errFile.rdbuf();
        result.err = errBuf.str();
        errFile.close();
    }
    remove(errPath.c_str());

    while (!output.empty() &&
           (output.back() == '\n' || output.back() == '\r'))
        output.pop_back();
    result.out = std::move(output);

    if (!result.ok && !result.err.empty()) {
        LOG_WARN("git command had errors: %s", result.err.c_str());
    }

    return result;
#endif
}

// ---- Shell script execution ----

std::string GitProcess::GetShellPath()
{
#ifdef _WIN32
    // Try common Git Bash locations
    const char* paths[] = {
        "C:\\Program Files\\Git\\bin\\bash.exe",
        "C:\\Program Files (x86)\\Git\\bin\\bash.exe",
        "C:\\Git\\bin\\bash.exe",
    };
    for (auto* p : paths) {
        if (GetFileAttributesA(p) != INVALID_FILE_ATTRIBUTES)
            return p;
    }
    // Fallback: hope it's in PATH
    return "bash";
#else
    return "/bin/bash";
#endif
}

std::string GitProcess::GetUserScriptsDir()
{
    std::string dir;
#ifdef _WIN32
    char* appData = nullptr;
    size_t appDataLen = 0;
    if (_dupenv_s(&appData, &appDataLen, "APPDATA") == 0 && appData) {
        dir = std::string(appData) + "/GitBee/scripts";
        free(appData);
    }
#else
    const char* xdgHome = std::getenv("XDG_CONFIG_HOME");
    if (xdgHome)
        dir = std::string(xdgHome) + "/GitBee/scripts";
    else {
        const char* home = std::getenv("HOME");
        if (home)
            dir = std::string(home) + "/.config/GitBee/scripts";
    }
#endif
    return dir;
}

std::string GitProcess::GetDefaultScriptsDir()
{
    // Bundled scripts live next to the executable in a "scripts" subfolder
    std::string exeDir;
#ifdef _WIN32
    wchar_t exePath[MAX_PATH];
    if (GetModuleFileNameW(NULL, exePath, MAX_PATH)) {
        std::filesystem::path p(exePath);
        exeDir = p.parent_path().string();
    }
#else
    // Try /proc/self/exe or argv[0] — best-effort
    char buf[4096];
    ssize_t len = readlink("/proc/self/exe", buf, sizeof(buf) - 1);
    if (len > 0) {
        buf[len] = '\0';
        exeDir = std::filesystem::path(buf).parent_path().string();
    }
#endif
    if (exeDir.empty()) return "";
    return exeDir + "/scripts";
}

GitResult GitProcess::ExecuteScript(const std::string& scriptPath,
                                     const std::string& repoPath,
                                     const std::vector<std::string>& extraArgs)
{
    GitResult result;

    // Verify script exists
    std::error_code ec;
    if (!std::filesystem::exists(scriptPath, ec)) {
        result.err = "Script not found: " + scriptPath;
        LOG_ERROR("ExecuteScript: %s", result.err.c_str());
        return result;
    }

    std::string shell = GetShellPath();

    // Build argument list: script path, repo path, then extra args
    std::string cmd = "\"" + shell + "\" \"" + scriptPath + "\" \"" + repoPath + "\"";
    for (auto& a : extraArgs) {
        // Escape double-quotes in arguments
        std::string escaped = a;
        size_t pos = 0;
        while ((pos = escaped.find('"', pos)) != std::string::npos) {
            escaped.replace(pos, 1, "\\\"");
            pos += 2;
        }
        cmd += " \"" + escaped + "\"";
    }

    LOG_INFO("ExecuteScript: %s", cmd.c_str());

#ifdef _WIN32
    FILE* pipe = _popen(cmd.c_str(), "r");
#else
    FILE* pipe = popen(cmd.c_str(), "r");
#endif
    if (!pipe) {
#ifdef _WIN32
        char errBuf[256] = {};
        strerror_s(errBuf, sizeof(errBuf), errno);
        result.err = std::string("Failed to execute script: ") + errBuf;
        LOG_ERROR("ExecuteScript: popen failed: %s", errBuf);
#else
        result.err = std::string("Failed to execute script: ") + strerror(errno);
        LOG_ERROR("ExecuteScript: popen failed: %s", strerror(errno));
#endif
        return result;
    }

    std::string output;
    char buf[4096];
    size_t n;
    while ((n = fread(buf, 1, sizeof(buf), pipe)) > 0)
        output.append(buf, n);

#ifdef _WIN32
    int status = _pclose(pipe);
#else
    int status = pclose(pipe);
#endif
    result.ok = (status == 0);
    result.out = std::move(output);

    while (!result.out.empty() &&
           (result.out.back() == '\n' || result.out.back() == '\r'))
        result.out.pop_back();

    if (result.ok) {
        LOG_INFO("ExecuteScript succeeded: %s", scriptPath.c_str());
    } else {
        LOG_WARN("ExecuteScript failed (exit=%d): %s",
                 status, scriptPath.c_str());
        if (!result.out.empty())
            result.err = result.out;
    }

    return result;
}
