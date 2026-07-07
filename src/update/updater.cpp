#include "updater.h"
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
        return info;
    }

    HINTERNET hConnect = WinHttpConnect(hSession, L"api.github.com",
        INTERNET_DEFAULT_HTTPS_PORT, 0);
    if (!hConnect)
    {
        WinHttpCloseHandle(hSession);
        info.error = "Failed to connect to GitHub API";
        return info;
    }

    HINTERNET hRequest = WinHttpOpenRequest(hConnect, L"GET",
        L"/repos/coding2233/GitBee/releases/tags/prerelease", NULL, NULL, NULL,
        WINHTTP_FLAG_SECURE);
    if (!hRequest)
    {
        WinHttpCloseHandle(hConnect);
        WinHttpCloseHandle(hSession);
        info.error = "Failed to create request";
        return info;
    }

    if (!WinHttpSendRequest(hRequest, NULL, 0, NULL, 0, 0, 0))
    {
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        WinHttpCloseHandle(hSession);
        info.error = "Failed to send request";
        return info;
    }

    if (!WinHttpReceiveResponse(hRequest, NULL))
    {
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        WinHttpCloseHandle(hSession);
        info.error = "Failed to receive response";
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
        info.error = "GitHub API returned status " + std::to_string(statusCode);
        return info;
    }

    if (response.empty())
    {
        info.error = "Empty response from GitHub API";
        return info;
    }

    // Find the first asset with name starting with "GitBee-installer-"
    size_t assetPos = 0;
    while (true)
    {
        size_t namePos = response.find("\"name\"", assetPos);
        if (namePos == std::string::npos)
            break;

        size_t nameStart = response.find('"', namePos + 7);
        if (nameStart == std::string::npos)
            break;
        size_t nameEnd = response.find('"', nameStart + 1);
        if (nameEnd == std::string::npos)
            break;

        std::string assetName = response.substr(nameStart + 1, nameEnd - nameStart - 1);

        if (assetName.find("GitBee-installer-") == 0 && EndsWith(assetName, ".exe"))
        {
            info.assetName = assetName;

            // Extract version from assetName: GitBee-installer-<hash>.exe
            std::string prefix = "GitBee-installer-";
            std::string suffix = ".exe";
            if (assetName.length() > prefix.length() + suffix.length())
            {
                info.latestVersion = assetName.substr(
                    prefix.length(),
                    assetName.length() - prefix.length() - suffix.length());
            }

            // Find download_url near this asset
            size_t urlStart = response.rfind("\"browser_download_url\"", namePos);
            if (urlStart != std::string::npos)
            {
                size_t urlValStart = response.find('"', urlStart + 22);
                if (urlValStart != std::string::npos)
                {
                    size_t urlValEnd = response.find('"', urlValStart + 1);
                    if (urlValEnd != std::string::npos)
                    {
                        info.downloadUrl = response.substr(urlValStart + 1, urlValEnd - urlValStart - 1);
                        // Unescape unicode
                        size_t uPos = info.downloadUrl.find("\\u");
                        while (uPos != std::string::npos)
                        {
                            info.downloadUrl.replace(uPos, 6, "");
                            uPos = info.downloadUrl.find("\\u");
                        }
                    }
                }
            }

            info.available = (info.latestVersion != info.currentVersion
                && !info.latestVersion.empty()
                && !info.downloadUrl.empty());
            break;
        }

        assetPos = nameEnd + 1;
    }

    if (info.assetName.empty())
        info.error = "No installer found in latest release";

    return info;
}

bool DownloadInstaller(const std::string& url, const std::string& destPath)
{
    std::wstring wUrl(url.begin(), url.end());
    std::wstring wDest(destPath.begin(), destPath.end());

    HRESULT hr = URLDownloadToFileW(NULL, wUrl.c_str(), wDest.c_str(), 0, NULL);
    return SUCCEEDED(hr);
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

// Stub for non-Windows platforms
Info CheckForUpdate()
{
    Info info;
    info.currentVersion = GetCurrentVersion();
    info.error = "Update check is only supported on Windows";
    return info;
}

bool DownloadInstaller(const std::string& url, const std::string& destPath)
{
    (void)url;
    (void)destPath;
    return false;
}

bool RunInstallerSilent(const std::string& installerPath)
{
    (void)installerPath;
    return false;
}

#endif

}
