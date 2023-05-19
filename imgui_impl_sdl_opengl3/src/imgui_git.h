//
// Created by EDY on 2023/5/19.
//

#ifndef IISO3_IMGUI_GIT_H
#define IISO3_IMGUI_GIT_H

#include <stdio.h>

#include "git2.h"

class ImGuiGit {

public:
    ImGuiGit(const char* git_path);
    ~ImGuiGit();

private:
    git_repository* repo_;

};


#endif //IISO3_IMGUI_GIT_H
