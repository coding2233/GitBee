#pragma once

#include <imgui.h>
#include <string>

namespace Theme {

void ApplyDark();
void LoadFonts(float scale = 1.0f);
ImFont* GetDefaultFont();
ImFont* GetIconFont();

}
