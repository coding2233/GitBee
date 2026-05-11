#include "git_util.h"
#include <sstream>

namespace git {

std::string ExtractField(const std::string& output, size_t& pos)
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

std::vector<GitCommit> ParseLogOutput(const std::string& output, int batchSize)
{
    std::vector<GitCommit> commits;
    size_t pos = 0;
    int count = 0;

    while (pos < output.size() && count < batchSize) {
        GitCommit commit;
        commit.hash = ExtractField(output, pos);
        if (commit.hash.empty()) break;

        commit.shortHash = ExtractField(output, pos);
        commit.author = ExtractField(output, pos);
        commit.authorEmail = ExtractField(output, pos);
        commit.date = ExtractField(output, pos);
        commit.message = ExtractField(output, pos);

        std::string refs = ExtractField(output, pos);
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

        std::string parents = ExtractField(output, pos);
        if (!parents.empty()) {
            std::istringstream parentStream(parents);
            std::string parentHash;
            while (parentStream >> parentHash) {
                commit.parentHashes.push_back(parentHash);
            }
        }

        commits.push_back(std::move(commit));
        count++;

        while (pos < output.size() && output[pos] == '\n') {
            pos++;
        }
    }

    return commits;
}

}
