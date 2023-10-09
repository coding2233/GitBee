using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.Configs
{
	public class StyleConfig
	{
		//默认字体
		public string Font { get; set; }
		//Ascii字体
		public string AsciiFont { get; set; }
		//字体大小
		public int FontSize { get; set; }
		// 返回字形范围, 可以使用自带字形范围(中、日、韩等)，也可以使用自定自定文本内容
		// 字形范围越小，初始内存占用越小
		// GetGlyphRangesDefault
		// GetGlyphRangesChineseFull
		// GetGlyphRangesChineseSimplifiedCommon
		// GetGlyphRangesCyrillic
		// GetGlyphRangesJapanese
		// GetGlyphRangesKorean
		// GetGlyphRangesThai
		// GetGlyphRangesVietnamese
		// */*.txt 使用自定义字形范围
		public string GlyphRanges { get; set; }
	}
}
