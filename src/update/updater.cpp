#include "updater.h"
#include "../dbg_log.h"
#include <cstdio>
#include <cstdlib>
#include <algorithm>

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <winhttp.h>
#include <urlmon.h>
#pragma comment(lib, "urlmon.lib")
#endif

namespace updater {

std::string GetCurrentVersion()
{
#ifdef GITBEE_VERSION
    return GITBEE_VERSION;
#else
    return "unknown";
#endif
}

#ifdef _WIN32

static std::string ReadResponse(HINTERNET hRequest)
{
    std::string result;
    char buf[4096];
    DWORD bytesRead = 0;
    while (WinHttpReadData(hRequest, buf, sizeof(buf) - 1, &bytesRead) && bytesRead > 0)
    {
        buf[bytesRead] = 0;
        result += buf;
        bytesRead = 0;
    }
    return result;
}

static std::string FindJsonString(const std::string& json, const std::string& key)
{
    std::string search = "\"" + key + "\"";
    size_t pos = json.find(search);
    if (pos == std::string::npos)
        return {};
    pos = json.find('"', pos + search.length());
    if (pos == std::string::npos)
        return {};
    size_t end = json.find('"', pos + 1);
    if (end == std::string::npos)
        return {};
    return json.substr(pos + 1, end - pos - 1);
}

static bool EndsWith(const std::string& str, const std::string& suffix)
{
    if (str.length() < suffix.length()) return false;
    return str.compare(str.length() - suffix.length(), suffix.length(), suffix) == 0;
}

Info CheckForUpdate()
{
    Info info;
    info.currentVersion = GetCurrentVersion();

    HINTERNET hSession = WinHttpOpen(L"GitBee-Update/1.0",
        WINHTTP_ACCESS_TYPE_DEFAULT_PROXY, NULL, NULL, 0);
    if (!hSession)
    {
        info.error = "Failed to initialize HTTP";
        LOG_ERROR("UpdateCheck: %s", info.error.c_str());
        return info;
    }

    HINTERNET hConnect = WinHttpConnect(hSession, L"github.com",
        INTERNET_DEFAULT_HTTPS_PORT, 0);
    if (!hConnect)
    {
        WinHttpCloseHandle(hSession);
        info.error = "Failed to connect";
        LOG_ERROR("UpdateCheck: %s", info.error.c_str());
        return info;
    }

    HINTERNET hRequest = WinHttpOpenRequest(hConnect, L"GET",
        L"/coding2233/GitBee/releases/download/prerelease/latest_version.txt",
        NULL, NULL, NULL, WINHTTP_FLAG_SECURE);
    if (!hRequest)
    {
        WinHttpCloseHandle(hConnect);
        WinHttpCloseHandle(hSession);
        info.error = "Failed to create request";
        LOG_ERROR("UpdateCheck: %s", info.error.c_str());
        return info;
    }

    if (!WinHttpSendRequest(hRequest, NULL, 0, NULL, 0, 0, 0))
    {
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        WinHttpCloseHandle(hSession);
        info.error = "Failed to send request";
        LOG_ERROR("UpdateCheck: %s", info.error.c_str());
        return info;
    }

    if (!WinHttpReceiveResponse(hRequest, NULL))
    {
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        WinHttpCloseHandle(hSession);
        info.error = "Failed to receive response";
        LOG_ERROR("UpdateCheck: %s", info.error.c_str());
        return info;
    }

    DWORD statusCode = 0;
    DWORD statusSize = sizeof(statusCode);
    WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
        NULL, &statusCode, &statusSize, NULL);

    std::string response = ReadResponse(hRequest);

    WinHttpCloseHandle(hRequest);
    WinHttpCloseHandle(hConnect);
    WinHttpCloseHandle(hSession);

    if (statusCode != 200)
    {
        info.error = "Failed to check for updates (HTTP " + std::to_string(statusCode) + ")";
        return info;
    }

    // Remove trailing whitespace
    while (!response.empty() && (response.back() == '\n' || response.back() == '\r' || response.back() == ' '))
        response.pop_back();

    if (response.empty())
    {
        info.error = "Empty version info";
        return info;
    }

    info.latestVersion = response;
    info.assetName = "GitBee-installer-" + response + ".exe";
    info.downloadUrl = "https://github.com/coding2233/GitBee/releases/download/prerelease/"
                       + info.assetName;
    info.available = (info.latestVersion != info.currentVersion
        && !info.downloadUrl.empty());

    return info;
}

static std::wstring Utf8ToWideStr(const std::string& s)
{
    int len = MultiByteToWideChar(CP_UTF8, 0, s.c_str(), -1, NULL, 0);
    if (len <= 0) return {};
    std::wstring ws(len - 1, 0);
    MultiByteToWideChar(CP_UTF8, 0, s.c_str(), -1, &ws[0], len);
    return ws;
}

bool DownloadInstaller(const std::string& url, const std::string& destPath)
{
    std::wstring wUrl = Utf8ToWideStr(url);
    std::wstring wDest = Utf8ToWideStr(destPath);
    if (wUrl.empty() || wDest.empty()) return false;

    LOG_INFO("DownloadInstaller: %s -> %s", url.c_str(), destPath.c_str());
    HRESULT hr = URLDownloadToFileW(NULL, wUrl.c_str(), wDest.c_str(), 0, NULL);
    bool ok = SUCCEEDED(hr);
    if (!ok)
        LOG_ERROR("DownloadInstaller failed: HRESULT=0x%08lx", (unsigned long)hr);
    else
        LOG_INFO("DownloadInstaller succeeded");
    return ok;
}

bool RunInstallerSilent(const std::string& installerPath)
{
    wchar_t exePath[MAX_PATH] = {};
    if (!GetModuleFileNameW(NULL, exePath, MAX_PATH))
        return false;

    wchar_t tempDir[MAX_PATH] = {};
    if (!GetTempPathW(MAX_PATH, tempDir))
        return false;

    wchar_t batchPath[MAX_PATH] = {};
    if (GetTempFileNameW(tempDir, L"upd", 0, batchPath) == 0)
        return false;

    std::wstring batchFile = batchPath;
    batchFile += L".bat";
    MoveFileExW(batchPath, batchFile.c_str(), MOVEFILE_REPLACE_EXISTING);

    auto WcharToUtf8 = [](const wchar_t* ws) -> std::string {
        int len = WideCharToMultiByte(CP_UTF8, 0, ws, -1, NULL, 0, NULL, NULL);
        if (len <= 0) return {};
        std::string s(len - 1, 0);
        WideCharToMultiByte(CP_UTF8, 0, ws, -1, &s[0], len, NULL, NULL);
        return s;
    };

    std::string utf8Content = "@timeout /t 5 /nobreak > nul\n";
    utf8Content += "\"" + installerPath + "\" /S\n";
    utf8Content += "start \"\" \"" + WcharToUtf8(exePath) + "\"\n";
    utf8Content += "del \"" + WcharToUtf8(batchFile.c_str()) + "\"";

    HANDLE hBatch = CreateFileW(batchFile.c_str(), GENERIC_WRITE, 0, NULL,
        CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hBatch == INVALID_HANDLE_VALUE)
        return false;

    DWORD written = 0;
    WriteFile(hBatch, utf8Content.c_str(), (DWORD)utf8Content.size(), &written, NULL);
    CloseHandle(hBatch);

    SHELLEXECUTEINFOW sei = {sizeof(sei)};
    sei.lpVerb = L"open";
    sei.lpFile = batchFile.c_str();
    sei.nShow = SW_HIDE;

    return ShellExecuteExW(&sei);
}

#else

// Non-Windows impl using curl/wget
Info CheckForUpdate()
{
    Info info;
    info.currentVersion = GetCurrentVersion();

    // Try curl first, then wget
    auto tryDownload = [](const std::string& url) -> std::string {
        std::string cmd;
        // curl: -sS = silent but show errors, -L follow redirects
        cmd = "curl -sS -L \"" + url + "\" 2>/dev/null";

        FILE* pipe = popen(cmd.c_str(), "r");
        if (!pipe) {
            // try wget
            cmd = "wget -q -O - \"" + url + "\" 2>/dev/null";
            pipe = popen(cmd.c_str(), "r");
            if (!pipe) return {};
        }

        std::string result;
        char buf[4096];
        size_t n;
        while ((n = fread(buf, 1, sizeof(buf), pipe)) > 0)
            result.append(buf, n);
        pclose(pipe);

        while (!result.empty() && (result.back() == '\n' || result.back() == '\r' || result.back() == ' '))
            result.pop_back();
        return result;
    };

    std::string versionUrl = "https://github.com/coding2233/GitBee/releases/download/prerelease/latest_version.txt";
    std::string response = tryDownload(versionUrl);

    if (response.empty())
    {
        info.error = "Failed to check for updates (curl/wget not found or network error)";
        LOG_WARN("UpdateCheck: %s", info.error.c_str());
        return info;
    }

    info.latestVersion = response;
    info.assetName = "GitBee-installer-" + response + ".exe";
    info.downloadUrl = "https://github.com/coding2233/GitBee/releases/download/prerelease/"
                       + info.assetName;
    info.available = (info.latestVersion != info.currentVersion
        && !info.downloadUrl.empty());

    LOG_INFO("UpdateCheck: current=%s latest=%s available=%d",
             info.currentVersion.c_str(), info.latestVersion.c_str(), info.available);
    return info;
}

bool DownloadInstaller(const std::string& url, const std::string& destPath)
{
    LOG_INFO("DownloadInstaller: %s -> %s", url.c_str(), destPath.c_str());

    // Try curl -Lo, then wget -O
    std::string cmd;
    cmd = "curl -sS -L -o \"" + destPath + "\" \"" + url + "\" 2>/dev/null";

    int ret = system(cmd.c_str());
    if (ret == 0) {
        LOG_INFO("DownloadInstaller: curl succeeded");
        return true;
    }
    LOG_WARN("DownloadInstaller: curl failed (ret=%d), trying wget...", ret);

    cmd = "wget -q -O \"" + destPath + "\" \"" + url + "\" 2>/dev/null";
    ret = system(cmd.c_str());
    if (ret == 0) {
        LOG_INFO("DownloadInstaller: wget succeeded");
        return true;
    }

    LOG_ERROR("DownloadInstaller: both curl (ret=%d) and wget (ret=%d) failed",
              ret, ret);
    return false;
}

bool RunInstallerSilent(const std::string& installerPath)
{
    (void)installerPath;
    // Not supported on non-Windows; user can run the installer manually
    LOG_WARN("RunInstallerSilent: not supported on this platform");
    return false;
}

#endif

}
