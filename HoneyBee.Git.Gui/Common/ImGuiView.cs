using strange.extensions.mediation.impl;

namespace Wanderer.Common
{
    public abstract class ImGuiView : EventView
    {
        //public static List<Vector4> Colors { get; } = new List<Vector4>() { new Vector4(0.9803921568627451f, 0.8274509803921569f, 0.5647058823529412f,1.0f),
        //                                        new Vector4(0.9725490196078431f, 0.7607843137254902f, 0.5686274509803922f,1.0f),
        //                                        new Vector4(0.4156862745098039f, 0.5372549019607843f, 0.8f,1.0f),
        //                                        new Vector4(0.5098039215686275f, 0.8f, 0.8274509803921569f,1.0f),
        //                                        new Vector4(0.7215686274509804f, 0.9137254901960784f, 0.5647058823529412f,1.0f),
        //                                        new Vector4(0.9647058823529412f, 0.7254901960784314f, 0.2313725490196078f,1.0f),
        //                                        new Vector4(0.8980392156862745f, 0.3137254901960784f, 0.2235294117647059f,1.0f),
        //                                        new Vector4(0.2901960784313725f, 0.4117647058823529f, 0.7411764705882353f,1.0f),
        //                                        new Vector4(0.3764705882352941f, 0.6392156862745098f, 0.7372549019607843f,1.0f),
        //                                        new Vector4(0.4705882352941176f, 0.8784313725490196f, 0.5607843137254902f,1.0f),
        //                                        new Vector4(0.9803921568627451f, 0.596078431372549f, 0.2274509803921569f,1.0f),
        //                                        new Vector4(0.9215686274509804f, 0.1843137254901961f, 0.0235294117647059f,1.0f),
        //                                        new Vector4(0.1176470588235294f, 0.2156862745098039f, 0.6f,1.0f),
        //                                        new Vector4(0.2352941176470588f, 0.3882352941176471f, 0.5098039215686275f,1.0f),
        //                                        new Vector4(0.2196078431372549f, 0.6784313725490196f, 0.6627450980392157f,1.0f),
        //                                        new Vector4(0.8980392156862745f, 0.5568627450980392f, 0.1490196078431373f,1.0f),
        //                                        new Vector4(0.7176470588235294f, 0.0823529411764706f, 0.2509803921568627f,1.0f),
        //                                        new Vector4(0.0470588235294118f, 0.1411764705882353f, 0.3803921568627451f,1.0f),
        //                                        new Vector4(0.0392156862745098f, 0.2392156862745098f, 0.3843137254901961f,1.0f),
        //                                        new Vector4(0.0274509803921569f, 0.6f, 0.5725490196078431f,1.0f)};

        //protected int m_priority;
        //public int priority
        //{
        //    get
        //    {
        //        return m_priority;
        //    }
        //    set
        //    {
        //        m_priority = value;
        //        //排序
        //        s_imGuiViews.Sort((x, y) =>
        //        {
        //            return x.priority - y.priority;
        //        });
        //    }
        //}

        public virtual string Name { get; } = "None";

        public virtual string IconName { get; } = Icon.Get(Icon.Material_tab);

        public virtual void OnDraw()
        { }
    }
}
