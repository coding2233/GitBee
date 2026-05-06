#pragma once

#include <volt-ui/VoltApp.h>

class GitBeeApp : public volt::App {
public:
    using volt::App::App;

protected:
    void OnCreate() override;
    void OnRender() override;
    void OnEvent(const SDL_Event& event) override;

private:
    bool show_demo_ = false;
};
