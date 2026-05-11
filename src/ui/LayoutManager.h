#pragma once

#include <imgui.h>

class LayoutManager {
public:
    void Init();
    void Shutdown();
    void BeginFrame();
    void EndFrame();

    void SetMenuBarHeight(float h) { menubar_height_ = h; }
    void ResetLayout() { reset_layout_ = true; }

private:
    bool reset_layout_ = false;
    bool initial_layout_done_ = false;
    float menubar_height_ = 0.0f;

    void SetupInitialLayout();
};
