﻿using System;
using System.Windows.Forms;

namespace MulticaretEditor
{
	public class ScrollBarData
	{
		public int value;
		public int areaSize;
		public int contentSize;
		public bool visible;
		
		public void ApplyParamsTo(ScrollBar scrollBar)
		{
			scrollBar.Minimum = 0;
			scrollBar.Maximum = contentSize;
			scrollBar.LargeChange = areaSize;
			scrollBar.Visible = visible;
		}
		
		public int ClampValue(int value)
		{
			return MathHelper.Clamp(value, 0, contentSize - areaSize);
		}
	}
}
