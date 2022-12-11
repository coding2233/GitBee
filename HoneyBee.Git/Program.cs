// See https://aka.ms/new-console-template for more information
using ImGuiNET;
using System.Runtime.InteropServices;



public unsafe class TestPro
{

    static IntPtr OnImGuiInit()
    {
        var context = ImGui.CreateContext();

        ImGui.GetIO().Fonts.AddFontFromFileTTF("C:\\Users\\wanderer\\AppData\\Local\\Microsoft\\Windows\\Fonts\\wqy-microhei.ttc", 18.0f,null, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull());

        return context;
        //ImGui.SetCurrentContext(context);
    }
    static string ttt="";
    static void OnImGuiDraw()
    {
        //ImGui.NewFrame();
        ImGui.Button("xxxxxxxxxxx");

        ImGui.InputText("sdasd", ref ttt, 500);
        //ImGui.Render();
    }


    static void Main(string[] args)
    {
        try
        {
            const string title = "Test imgui";
            int result = Create(title, OnImGuiInit, OnImGuiDraw);
        }
        catch (Exception e)
        { 
            Console.WriteLine(e.Message);
        }
    }

    delegate IntPtr IMGUI_INIT_CALLBACK();
    delegate void IMGUI_DRAW_CALLBACK();
    delegate void IMGUI_DRAW_RENDER_CALLBACK();

    [DllImport("imgui-impl-sdl-opengl3.dll")]
    extern static int Create(string title, IMGUI_INIT_CALLBACK imgui_init_cb, IMGUI_DRAW_CALLBACK imgui_draw_cb);

    [DllImport("imgui-impl-sdl-opengl3.dll")]
    extern static void RenderDrawData(IntPtr draw_data);
}