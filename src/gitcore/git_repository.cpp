#include "git_repository.h"
#include "git_process.h"

#include <memory>
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

std::pair<bool, std::string> GitRepository::Git(const std::string& path,
    const std::vector<std::string>& args)
{
    return GitProcess::Execute(path, args);
}

GitRepository::GitRepository(const std::string& path) : m_path(path) {}

bool GitRepository::IsValid() const
{
    auto [s, o] = Git(m_path, {"rev-parse", "--git-dir"});
    return s && !o.empty();
}

std::string GitRepository::GetRootPath() const
{
    auto [s, o] = Git(m_path, {"rev-parse", "--show-toplevel"});
    return s ? o : "";
}

std::string GitRepository::GetCurrentBranch() const
{
    auto [s, o] = Git(m_path, {"rev-parse", "--abbrev-ref", "HEAD"});
    return s ? o : "";
}

GitSignature GitRepository::GetSignature() const
{
    GitSignature sig;
    auto [s1, name] = Git(m_path, {"config", "user.name"});
    auto [s2, email] = Git(m_path, {"config", "user.email"});
    if (s1) sig.name = name;
    if (s2) sig.email = email;
    return sig;
}

bool GitRepository::Commit(const std::string& message)
{
    if (message.empty()) return false;
    auto [s, o] = Git(m_path, {"commit", "-m", message});
    return s;
}

bool GitRepository::Stage(const std::vector<std::string>& files)
{
    std::vector<std::string> args = {"add"};
    if (files.empty())
        args.push_back(".");
    else
        for (auto& f : files) args.push_back(f);
    auto [s, o] = Git(m_path, args);
    return s;
}

bool GitRepository::Unstage(const std::vector<std::string>& files)
{
    std::vector<std::string> args = {"reset", "HEAD", "--"};
    if (files.empty())
        args.push_back(".");
    else
        for (auto& f : files) args.push_back(f);
    auto [s, o] = Git(m_path, args);
    return s;
}

bool GitRepository::Restore(const std::vector<std::string>& files)
{
    std::vector<std::string> args = {"checkout", "--"};
    for (auto& f : files) args.push_back(f);
    auto [s, o] = Git(m_path, args);
    return s;
}

bool GitRepository::Discard(const std::vector<std::string>& files)
{
    std::vector<std::string> args = {"restore"};
    for (auto& f : files) args.push_back(f);
    auto [s, o] = Git(m_path, args);
    if (!s)
    {
        args = {"checkout", "--"};
        for (auto& f : files) args.push_back(f);
        auto [s2, o2] = Git(m_path, args);
        return s2;
    }
    return s;
}

bool GitRepository::Pull()
{
    auto [s, o] = Git(m_path, {"pull"});
    return s;
}

bool GitRepository::Push()
{
    auto [s, o] = Git(m_path, {"push"});
    return s;
}

bool GitRepository::Fetch()
{
    auto [s, o] = Git(m_path, {"fetch", "--all"});
    return s;
}

GitBranchInfo GitRepository::ParseBranchLine(const std::string& line) const
{
    GitBranchInfo info;
    if (line.empty()) return info;

    std::string trimmed = line;
    auto start = trimmed.find_first_not_of(" \t");
    if (start != std::string::npos) trimmed = trimmed.substr(start);

    info.isHead = (!trimmed.empty() && trimmed[0] == '*');
    if (info.isHead && trimmed.size() > 2) trimmed = trimmed.substr(2);

    info.name = trimmed;
    // Extract tracking info
    auto bracket = trimmed.find(" [");
    if (bracket != std::string::npos)
    {
        info.name = trimmed.substr(0, bracket);
        std::string tracking = trimmed.substr(bracket + 2);
        if (tracking.back() == ']') tracking.pop_back();

        if (tracking.find("ahead") != std::string::npos)
        {
            auto n = tracking.find(", behind");
            if (n != std::string::npos)
            {
                info.aheadBy = std::stoi(tracking.substr(6, n - 6));
                info.behindBy = std::stoi(tracking.substr(n + 9));
            }
            else
            {
                info.aheadBy = std::stoi(tracking.substr(6));
            }
        }
        else if (tracking.find("behind") != std::string::npos)
        {
            info.behindBy = std::stoi(tracking.substr(8));
        }
    }

    return info;
}

std::vector<GitBranchInfo> GitRepository::GetBranches() const
{
    std::vector<GitBranchInfo> result;
    auto [s, output] = Git(m_path, {"branch", "-vv", "--format=%(refname:short)%00%(objectname:short)%00%(upstream:track)"});
    if (!s || output.empty())
    {
        // Fallback
        auto [s2, o2] = Git(m_path, {"branch"});
        if (!s2) return result;

        std::istringstream ss(o2);
        std::string line;
        while (std::getline(ss, line))
        {
            GitBranchInfo info = ParseBranchLine(line);
            if (!info.name.empty())
                result.push_back(info);
        }
        return result;
    }

    std::istringstream ss(output);
    std::string line;
    while (std::getline(ss, line))
    {
        if (line.empty()) continue;
        GitBranchInfo info = ParseBranchLine(line);
        if (!info.name.empty())
        {
            info.isRemote = false;
            result.push_back(info);
        }
    }
    return result;
}

std::vector<GitBranchInfo> GitRepository::GetRemoteBranches() const
{
    std::vector<GitBranchInfo> result;
    auto [s, output] = Git(m_path, {"branch", "-r"});
    if (!s) return result;

    std::istringstream ss(output);
    std::string line;
    while (std::getline(ss, line))
    {
        GitBranchInfo info = ParseBranchLine(line);
        if (!info.name.empty())
        {
            info.isRemote = true;
            result.push_back(info);
        }
    }
    return result;
}

bool GitRepository::CheckoutBranch(const std::string& name)
{
    auto [s, o] = Git(m_path, {"checkout", name});
    return s;
}

bool GitRepository::CreateBranch(const std::string& name, const std::string& from)
{
    std::vector<std::string> args = {"branch", name};
    if (!from.empty()) args.push_back(from);
    auto [s, o] = Git(m_path, args);
    return s;
}

std::vector<GitTagInfo> GitRepository::GetTags() const
{
    std::vector<GitTagInfo> result;
    auto [s, output] = Git(m_path, {"tag", "--format=%(refname:short)%00%(objectname:short)"});
    if (!s) return result;

    size_t pos = 0;
    while (pos < output.size())
    {
        auto name = extractField(output, pos);
        if (name.empty()) break;
        auto sha = extractField(output, pos);
        result.push_back({name, sha});
    }
    return result;
}

std::vector<GitSubmoduleInfo> GitRepository::GetSubmodules() const
{
    std::vector<GitSubmoduleInfo> result;
    auto [s, output] = Git(m_path, {"submodule", "status", "--recursive"});
    if (!s) return result;

    std::istringstream ss(output);
    std::string line;
    while (std::getline(ss, line))
    {
        if (line.empty()) continue;
        GitSubmoduleInfo info;
        auto space1 = line.find(' ');
        if (space1 != std::string::npos)
        {
            info.sha = line.substr(1, space1 - 1);
            auto space2 = line.find('(', space1 + 1);
            if (space2 != std::string::npos)
            {
                info.path = line.substr(space1 + 1, space2 - space1 - 2);
                auto space3 = line.find(')', space2);
                if (space3 != std::string::npos)
                    info.name = info.path;
            }
            else
            {
                info.path = line.substr(space1 + 1);
                info.name = info.path;
            }
            result.push_back(info);
        }
    }
    return result;
}

// --- Log / Detail methods unchanged ---

std::vector<GitCommit> GitRepository::GetLog(const GitLogOptions& options) const
{
    std::vector<std::string> args;
    args.push_back("log");
    args.push_back("--format=%H%x00%h%x00%an%x00%ae%x00%aI%x00%s%x00%D%x00%P%x00");
    args.push_back("--date-order");

    if (options.maxCount > 0) { args.push_back("-n"); args.push_back(std::to_string(options.maxCount)); }
    if (options.skip > 0) { args.push_back("--skip"); args.push_back(std::to_string(options.skip)); }
    if (!options.author.empty()) { args.push_back("--author"); args.push_back(options.author); }
    if (options.showAllBranches) args.push_back("--all");
    else args.push_back(options.branch);
    if (!options.path.empty()) { args.push_back("--"); args.push_back(options.path); }

    auto [ok, output] = Git(m_path, args);
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
                if (start != std::string::npos)
                    commit.refs.push_back(ref.substr(start));
            }
        }

        std::string parents = extractField(output, pos);
        if (!parents.empty()) {
            std::istringstream parentStream(parents);
            std::string parentHash;
            while (parentStream >> parentHash)
                commit.parentHashes.push_back(parentHash);
        }

        commits.push_back(std::move(commit));
        while (pos < output.size() && output[pos] == '\n') pos++;
    }
    return commits;
}

GitCommitDetail GitRepository::GetCommitDetail(const std::string& hash) const
{
    GitCommitDetail detail;
    auto [ok, output] = Git(m_path, {
        "show", "--format=%H%x00%an%x00%ae%x00%aI%x00%s%x00%B%x00", "--stat", hash});
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
        while (bodyStart < fullBody.size() && fullBody[bodyStart] == '\n') bodyStart++;
        detail.body = fullBody.substr(bodyStart);
    }
    while (pos < output.size() && (output[pos] == '\0' || output[pos] == '\n')) pos++;
    if (pos >= output.size()) return detail;

    std::string statSection = output.substr(pos);
    std::istringstream stream(statSection);
    std::string line;
    while (std::getline(stream, line)) {
        if (line.empty()) continue;
        if (line.rfind("diff --git", 0) == 0) break;

        if (line.find("file") != std::string::npos && line.find("changed") != std::string::npos) {
            auto insPos = line.find(" insertion");
            if (insPos == std::string::npos) insPos = line.find(" insertions");
            if (insPos != std::string::npos) {
                auto numStart = insPos;
                while (numStart > 0 && std::isdigit((unsigned char)line[numStart - 1])) numStart--;
                if (numStart < insPos) detail.additions = std::stoi(line.substr(numStart, insPos - numStart));
            }
            auto delPos = line.find(" deletion");
            if (delPos == std::string::npos) delPos = line.find(" deletions");
            if (delPos != std::string::npos) {
                auto numStart = delPos;
                while (numStart > 0 && std::isdigit((unsigned char)line[numStart - 1])) numStart--;
                if (numStart < delPos) detail.deletions = std::stoi(line.substr(numStart, delPos - numStart));
            }
            continue;
        }
        auto pipePos = line.find('|');
        if (pipePos == std::string::npos) continue;

        std::string filePath = line.substr(0, pipePos);
        auto trimStart = filePath.find_first_not_of(" \t");
        auto trimEnd = filePath.find_last_not_of(" \t");
        if (trimStart != std::string::npos && trimEnd != std::string::npos)
            filePath = filePath.substr(trimStart, trimEnd - trimStart + 1);
        if (filePath.empty()) continue;

        std::string statPart = line.substr(pipePos + 1);
        bool hasPlus = statPart.find('+') != std::string::npos;
        bool hasMinus = statPart.find('-') != std::string::npos;
        if (hasPlus && !hasMinus) detail.addedFiles.push_back(filePath);
        else if (hasMinus && !hasPlus) detail.deletedFiles.push_back(filePath);
        else detail.modifiedFiles.push_back(filePath);
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

std::vector<std::string> GitRepository::GetChangedFiles(const std::string& fromHash, const std::string& toHash) const
{
    auto [ok, output] = Git(m_path, {"diff", "--name-only", fromHash, toHash});
    if (!ok || output.empty()) return {};
    std::vector<std::string> files;
    std::istringstream stream(output);
    std::string line;
    while (std::getline(stream, line))
        if (!line.empty()) files.push_back(line);
    return files;
}

std::shared_ptr<GitRepository> GitRepository::Open(const std::string& path)
{
    std::string resolved = path;
    while (!resolved.empty() && (resolved.back() == '/' || resolved.back() == '\\'))
        resolved.pop_back();
    if (resolved.size() >= 5) {
        std::string suffix = resolved.substr(resolved.size() - 4);
        if (suffix == ".git" || suffix == ".GIT") {
            char sep = resolved[resolved.size() - 5];
            if (sep == '/' || sep == '\\') resolved.resize(resolved.size() - 5);
        }
    }
    auto tmp = std::make_shared<GitRepository>(resolved);
    if (!tmp->IsValid()) return nullptr;
    std::string root = tmp->GetRootPath();
    if (!root.empty()) resolved = root;
    return std::make_shared<GitRepository>(resolved);
}

GitStatus GitRepository::GetStatus() const
{
    GitStatus status;

    auto [ok, branchOutput] = Git(m_path, {"rev-parse", "--abbrev-ref", "HEAD"});
    if (ok) status.currentBranch = branchOutput;

    auto [ok2, upstreamOutput] = Git(m_path, {"rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}"});
    if (ok2) status.upstreamBranch = upstreamOutput;

    auto [ok3, countOutput] = Git(m_path, {"rev-list", "--left-right", "--count", "HEAD...@{upstream}"});
    if (ok3) {
        auto space = countOutput.find('\t');
        if (space != std::string::npos) {
            status.aheadCount = std::stoi(countOutput.substr(0, space));
            status.behindCount = std::stoi(countOutput.substr(space + 1));
        }
    }

    auto [ok4, porcelain] = Git(m_path, {"status", "--porcelain", "-u"});
    if (!ok4) return status;

    std::istringstream stream(porcelain);
    std::string line;
    while (std::getline(stream, line)) {
        if (line.size() < 3) continue;
        GitFileEntry entry;
        entry.filename = line.substr(3);
        char x = line[0], y = line[1];

        if (x == '?' && y == '?') {
            entry.status = GitFileStatus::Untracked;
            entry.isStaged = false;
            status.untrackedFiles.push_back(entry);
            status.totalChanges++;
            continue;
        }
        if (x == 'U' || y == 'U') status.hasMergeConflict = true;

        if (x != ' ' && x != '?') {
            entry.isStaged = true;
            switch (x) {
                case 'M': entry.status = GitFileStatus::StagedModified; break;
                case 'A': entry.status = GitFileStatus::StagedAdded; break;
                case 'D': entry.status = GitFileStatus::StagedDeleted; break;
                case 'R': entry.status = GitFileStatus::Renamed; break;
                default: entry.status = GitFileStatus::Unknown; break;
            }
            if (entry.status == GitFileStatus::Renamed) {
                auto arrow = entry.filename.find(" -> ");
                if (arrow != std::string::npos) {
                    entry.oldFilename = entry.filename.substr(0, arrow);
                    entry.filename = entry.filename.substr(arrow + 4);
                }
            }
            status.stagedFiles.push_back(entry);
            status.totalChanges++;
        }
        if (y != ' ' && x != '?' && x != '!') {
            entry.isStaged = false;
            switch (y) {
                case 'M': entry.status = GitFileStatus::Modified; break;
                case 'A': entry.status = GitFileStatus::Added; break;
                case 'D': entry.status = GitFileStatus::Deleted; break;
                default: entry.status = GitFileStatus::Unknown; break;
            }
            if (x == ' ' || x == 'R') {
                auto arrow = entry.filename.find(" -> ");
                if (arrow != std::string::npos) {
                    entry.oldFilename = entry.filename.substr(0, arrow);
                    entry.filename = entry.filename.substr(arrow + 4);
                }
            }
            status.unstagedFiles.push_back(entry);
            status.totalChanges++;
        }
    }
    return status;
}
