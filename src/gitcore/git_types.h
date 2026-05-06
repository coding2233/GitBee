#pragma once

#include <string>
#include <vector>

enum class GitFileStatus {
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    StagedModified,
    StagedAdded,
    StagedDeleted,
    Unmerged,
    Unknown
};

struct GitFileEntry {
    std::string filename;
    std::string oldFilename;
    GitFileStatus status = GitFileStatus::Unknown;
    bool isStaged = false;
};

struct GitStatus {
    std::string currentBranch;
    std::string upstreamBranch;
    int aheadCount = 0;
    int behindCount = 0;
    std::vector<GitFileEntry> stagedFiles;
    std::vector<GitFileEntry> unstagedFiles;
    std::vector<GitFileEntry> untrackedFiles;
    int totalChanges = 0;
    bool hasMergeConflict = false;
};

struct GitCommit {
    std::string hash;
    std::string shortHash;
    std::string author;
    std::string authorEmail;
    std::string date;
    std::string message;
    std::string body;              // Only populated by GetCommitDetail()
    std::vector<std::string> parentHashes;
    std::vector<std::string> refs;
};

struct GitCommitDetail : public GitCommit {
    std::vector<std::string> addedFiles;
    std::vector<std::string> modifiedFiles;
    std::vector<std::string> deletedFiles;
    int additions = 0;
    int deletions = 0;
};

struct GitLogOptions {
    int maxCount = 100;
    int skip = 0;
    std::string branch = "HEAD";
    std::string path;
    std::string author;
    bool showAllBranches = false;
};
