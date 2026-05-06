#include "app.h"
#include <imgui.h>

void GitBeeApp::OnCreate() {
    SetClearColor({0.12f, 0.12f, 0.15f, 1.0f});
    SDL_SetWindowMinimumSize(GetWindow(), 800, 600);
}

void GitBeeApp::OnRender() {
}

void GitBeeApp::OnEvent(const SDL_Event& event) {
    if (event.type == SDL_EVENT_KEY_DOWN && event.key.key == SDLK_ESCAPE) {
        Quit();
    }
}
