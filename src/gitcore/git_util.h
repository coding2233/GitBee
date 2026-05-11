#pragma once

#include <string>
#include <vector>
#include "git_types.h"

namespace git {

std::string ExtractField(const std::string& output, size_t& pos);
std::vector<GitCommit> ParseLogOutput(const std::string& output, int batchSize);

}
