Style = {}

-- 语言
Style.Language = require("language/zh_cn")

-- 字体
-- C:\\Windows\\Fonts\\msyh.ttc
Style.Font = "lua/style/fonts/wqy-microhei.ttc"
-- 字体大小
Style.FontSize = 14.0

-- 返回字形范围, 可以使用自带字形范围(中、日、韩等)，也可以使用自定自定文本内容
-- 字形范围越小，初始内存占用越小
-- GetGlyphRangesDefault
-- GetGlyphRangesChineseFull
-- GetGlyphRangesChineseSimplifiedCommon
-- GetGlyphRangesCyrillic
-- GetGlyphRangesJapanese
-- GetGlyphRangesKorean
-- GetGlyphRangesThai
-- GetGlyphRangesVietnamese
-- */*.txt 使用自定义字形范围
Style.GlyphRanges = "lua/style/chinese.txt"

-- 自定义颜色
-- 以dark模板为准
Style.Color = require("color")