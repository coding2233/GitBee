#include "git_process.h"

#include <cstdio>
#include <cstring>
#include <sstream>
#include <fstream>
#include <cstdlib>

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

    STARTUPINFOA si = { sizeof(si) };
    si.dwFlags = STARTF_USESTDHANDLES;
    si.hStdOutput = hOutWr;
    si.hStdError  = hErrWr;

    PROCESS_INFORMATION pi = {};
    std::vector<char> cmdBuf(cmdLine.begin(), cmdLine.end());
    cmdBuf.push_back('\0');

    if (!CreateProcessA(NULL, cmdBuf.data(), NULL, NULL, TRUE,
                        CREATE_NO_WINDOW, NULL, NULL, &si, &pi))
    {
        CloseHandle(hOutRd); CloseHandle(hOutWr);
        CloseHandle(hErrRd); CloseHandle(hErrWr);
        result.err = "Git is not installed or not found in PATH";
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

    FILE* pipe = popen(cmd.c_str(), "r");
    if (!pipe) {
        if (errno == ENOENT)
            result.err = "Git is not installed or not found in PATH";
        else
            result.err = "Failed to execute command: " +
                         std::string(strerror(errno));
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
    return result;
#endif
}
