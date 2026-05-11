#pragma once

#include <imgui.h>
#include <string>

namespace Theme {

void ApplyDark();
void LoadFonts();
ImFont* GetDefaultFont();
ImFont* GetIconFont();

}
