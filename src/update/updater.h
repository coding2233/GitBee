#pragma once

#include <string>

namespace updater {

struct Info {
    std::string currentVersion;
    std::string latestVersion;
    std::string downloadUrl;
    std::string assetName;
    bool available = false;
    std::string error;
};

std::string GetCurrentVersion();
Info CheckForUpdate();
bool DownloadInstaller(const std::string& url, const std::string& destPath);
bool RunInstallerSilent(const std::string& installerPath);

}
