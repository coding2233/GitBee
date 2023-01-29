local styleConfig = {}

-- 字体
-- C:\\Windows\\Fonts\\msyh.ttc
styleConfig.Font = "lua/style/wqy-microhei.ttc"
-- 字体大小
styleConfig.FontSize = 14.0

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
styleConfig.GlyphRanges = "lua/style/chinese.txt"


return styleConfig




