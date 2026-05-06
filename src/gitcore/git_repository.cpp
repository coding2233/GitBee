#include "git_repository.h"
#include "git_process.h"

#include <sstream>
#include <algorithm>
#include <cctype>

static std::string extractField(const std::string& output, size_t& pos)
{
    if (pos >= output.size()) return {};
    auto end = output.find('\0', pos);
    if (end == std::string::npos) {
        auto result = output.substr(pos);
        pos = output.size();
        return result;
    }
    auto result = output.substr(pos, end - pos);
    pos = end + 1;
    return result;
}

GitRepository::GitRepository(const std::string& path)
    : m_path(path)
{
}

bool GitRepository::IsValid() const
{
    auto [success, output] = GitProcess::Execute(m_path, {"rev-parse", "--git-dir"});
    return success && !output.empty();
}

std::string GitRepository::GetRootPath() const
{
    auto [success, output] = GitProcess::Execute(m_path, {"rev-parse", "--show-toplevel"});
    if (success) {
        return output;
    }
    return {};
}

std::string GitRepository::GetCurrentBranch() const
{
    auto [success, output] = GitProcess::Execute(m_path, {"rev-parse", "--abbrev-ref", "HEAD"});
    if (success) {
        return output;
    }
    return {};
}

std::vector<GitCommit> GitRepository::GetLog(const GitLogOptions& options) const
{
    std::vector<std::string> args;
    args.push_back("log");
    args.push_back("--format=%H%x00%h%x00%an%x00%ae%x00%aI%x00%s%x00%D%x00%P%x00");
    args.push_back("--date-order");

    if (options.maxCount > 0) {
        args.push_back("-n");
        args.push_back(std::to_string(options.maxCount));
    }
    if (options.skip > 0) {
        args.push_back("--skip");
        args.push_back(std::to_string(options.skip));
    }
    if (!options.author.empty()) {
        args.push_back("--author");
        args.push_back(options.author);
    }
    if (options.showAllBranches) {
        args.push_back("--all");
    } else {
        args.push_back(options.branch);
    }
    if (!options.path.empty()) {
        args.push_back("--");
        args.push_back(options.path);
    }

    auto [ok, output] = GitProcess::Execute(m_path, args);
    if (!ok || output.empty()) return {};

    std::vector<GitCommit> commits;
    size_t pos = 0;

    while (pos < output.size()) {
        GitCommit commit;
        commit.hash = extractField(output, pos);
        if (commit.hash.empty()) break;

        commit.shortHash = extractField(output, pos);
        commit.author = extractField(output, pos);
        commit.authorEmail = extractField(output, pos);
        commit.date = extractField(output, pos);
        commit.message = extractField(output, pos);

        std::string refs = extractField(output, pos);
        if (!refs.empty()) {
            std::istringstream refStream(refs);
            std::string ref;
            while (std::getline(refStream, ref, ',')) {
                auto start = ref.find_first_not_of(" ");
                if (start != std::string::npos) {
                    commit.refs.push_back(ref.substr(start));
                }
            }
        }

        std::string parents = extractField(output, pos);
        if (!parents.empty()) {
            std::istringstream parentStream(parents);
            std::string parentHash;
            while (parentStream >> parentHash) {
                commit.parentHashes.push_back(parentHash);
            }
        }

        commits.push_back(std::move(commit));

        while (pos < output.size() && output[pos] == '\n') {
            pos++;
        }
    }

    return commits;
}

GitCommitDetail GitRepository::GetCommitDetail(const std::string& hash) const
{
    GitCommitDetail detail;

    auto [ok, output] = GitProcess::Execute(m_path, {
        "show",
        "--format=%H%x00%an%x00%ae%x00%aI%x00%s%x00%B%x00",
        "--stat",
        hash
    });
    if (!ok || output.empty()) return detail;

    size_t pos = 0;

    detail.hash = extractField(output, pos);
    if (detail.hash.empty()) return detail;

    detail.shortHash = detail.hash.substr(0, 7);
    detail.author = extractField(output, pos);
    detail.authorEmail = extractField(output, pos);
    detail.date = extractField(output, pos);
    detail.message = extractField(output, pos);

    std::string fullBody = extractField(output, pos);
    auto newlinePos = fullBody.find('\n');
    if (newlinePos != std::string::npos) {
        size_t bodyStart = newlinePos + 1;
        while (bodyStart < fullBody.size() && fullBody[bodyStart] == '\n') {
            bodyStart++;
        }
        detail.body = fullBody.substr(bodyStart);
    }

    while (pos < output.size() && (output[pos] == '\0' || output[pos] == '\n')) {
        pos++;
    }

    if (pos >= output.size()) return detail;

    std::string statSection = output.substr(pos);
    std::istringstream stream(statSection);
    std::string line;

    while (std::getline(stream, line)) {
        if (line.empty()) continue;
        if (line.rfind("diff --git", 0) == 0) break;

        if (line.find("file") != std::string::npos && line.find("changed") != std::string::npos) {
            auto insPos = line.find(" insertion");
            if (insPos == std::string::npos)
                insPos = line.find(" insertions");
            if (insPos != std::string::npos) {
                auto numEnd = insPos;
                auto numStart = numEnd;
                while (numStart > 0 && std::isdigit(static_cast<unsigned char>(line[numStart - 1]))) {
                    numStart--;
                }
                if (numStart < numEnd) {
                    detail.additions = std::stoi(line.substr(numStart, numEnd - numStart));
                }
            }
            auto delPos = line.find(" deletion");
            if (delPos == std::string::npos)
                delPos = line.find(" deletions");
            if (delPos != std::string::npos) {
                auto numEnd = delPos;
                auto numStart = numEnd;
                while (numStart > 0 && std::isdigit(static_cast<unsigned char>(line[numStart - 1]))) {
                    numStart--;
                }
                if (numStart < numEnd) {
                    detail.deletions = std::stoi(line.substr(numStart, numEnd - numStart));
                }
            }
            continue;
        }

        auto pipePos = line.find('|');
        if (pipePos == std::string::npos) continue;

        std::string filePath = line.substr(0, pipePos);
        auto trimStart = filePath.find_first_not_of(" \t");
        auto trimEnd = filePath.find_last_not_of(" \t");
        if (trimStart != std::string::npos && trimEnd != std::string::npos) {
            filePath = filePath.substr(trimStart, trimEnd - trimStart + 1);
        }
        if (filePath.empty()) continue;

        std::string statPart = line.substr(pipePos + 1);
        bool hasPlus = statPart.find('+') != std::string::npos;
        bool hasMinus = statPart.find('-') != std::string::npos;

        if (hasPlus && !hasMinus) {
            detail.addedFiles.push_back(filePath);
        } else if (hasMinus && !hasPlus) {
            detail.deletedFiles.push_back(filePath);
        } else {
            detail.modifiedFiles.push_back(filePath);
        }
    }

    return detail;
}

std::vector<GitCommit> GitRepository::GetFileLog(const std::string& filePath, int maxCount) const
{
    GitLogOptions options;
    options.maxCount = maxCount;
    options.path = filePath;
    return GetLog(options);
}

std::vector<std::string> GitRepository::GetChangedFiles(const std::string& fromHash,
                                                         const std::string& toHash) const
{
    auto [ok, output] = GitProcess::Execute(m_path, {
        "diff", "--name-only", fromHash, toHash
    });
    if (!ok || output.empty()) return {};

    std::vector<std::string> files;
    std::istringstream stream(output);
    std::string line;
    while (std::getline(stream, line)) {
        if (!line.empty()) {
            files.push_back(line);
        }
    }
    return files;
}
