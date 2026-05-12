#include <cassert>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>

#include "git_process.h"
#include "git_repository.h"
#include "git_types.h"

namespace fs = std::filesystem;

static fs::path CreateTempGitRepo() {
    fs::path tmpDir = fs::temp_directory_path() / "gitbee_test_XXXXXX";
    for (int i = 0; i < 100; ++i) {
        auto path = fs::temp_directory_path() / ("gitbee_test_" + std::to_string(i));
        if (!fs::exists(path)) {
            tmpDir = path;
            break;
        }
    }
    fs::create_directories(tmpDir);
    auto result = GitProcess::Execute(tmpDir.string(), {"init"});
    if (!result.ok) {
        fs::remove_all(tmpDir);
        return {};
    }
    return tmpDir;
}

static void TestGitProcess() {
    std::cout << "[TEST] GitProcess::Execute ... ";

    auto result = GitProcess::Execute(".", {"--version"});
    assert(result.ok);
    assert(!result.out.empty());
    assert(result.out.find("git") != std::string::npos);

    std::cout << "PASSED" << std::endl;
}

static void TestGitRepository() {
    std::cout << "[TEST] GitRepository::IsValid ... ";

    GitRepository invalidRepo("/nonexistent/path");
    assert(!invalidRepo.IsValid());

    std::cout << "PASSED" << std::endl;
}

static void TestGitRepositoryValid() {
    std::cout << "[TEST] GitRepository valid repo ... ";

    auto repoPath = CreateTempGitRepo();
    assert(!repoPath.empty());

    GitRepository repo(repoPath.string());
    assert(repo.IsValid());
    assert(repo.GetRootPath() == fs::absolute(repoPath).string());

    auto branch = repo.GetCurrentBranch();
    assert(!branch.empty());

    fs::remove_all(repoPath);
    std::cout << "PASSED" << std::endl;
}

static void TestGitLog() {
    std::cout << "[TEST] GitRepository::GetLog (no commits) ... ";

    auto repoPath = CreateTempGitRepo();
    assert(!repoPath.empty());

    GitRepository repo(repoPath.string());
    auto commits = repo.GetLog();
    assert(commits.empty());

    fs::remove_all(repoPath);
    std::cout << "PASSED" << std::endl;
}

int main() {
    TestGitProcess();
    TestGitRepository();
    TestGitRepositoryValid();
    TestGitLog();

    std::cout << "\nAll tests passed!" << std::endl;
    return 0;
}
