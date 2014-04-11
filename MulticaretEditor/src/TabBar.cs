﻿using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Text;
using System.Diagnostics;
using Microsoft.Win32;
using MulticaretEditor.KeyMapping;
using MulticaretEditor.Highlighting;

namespace MulticaretEditor
{
	public class TabBar<T> : Control
	{
		public static string DefaultStringOf(T value)
		{
			return value + "";
		}

		public event Setter CloseClick;
		public event Setter<T> TabDoubleClick;
		
		private Timer arrowTimer;
		private StringFormat stringFormat = new StringFormat(StringFormatFlags.MeasureTrailingSpaces);
		private readonly SwitchList<T> list;
		private readonly StringOfDelegate<T> stringOf;

		public TabBar(SwitchList<T> list, StringOfDelegate<T> stringOf)
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			SetStyle(ControlStyles.UserPaint, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			
			this.list = list;
			this.stringOf = stringOf;
			TabStop = false;
			list.SelectedChange += OnSelectedChange;
			
			SetFont(FontFamily.GenericMonospace, 10.25f);
			
			arrowTimer = new Timer();
			arrowTimer.Interval = 150;
			arrowTimer.Tick += OnArrowTick;
		}
		
		private void OnSelectedChange()
		{
			needScrollToSelected = true;
			Invalidate();
		}

		public SwitchList<T> List { get { return list; } }

		private string text;
		public override string Text
		{
			get { return text; }
			set
			{
				if (text != value)
				{
					text = value;
					Invalidate();
				}
			}
		}

		private Font font;
		private Font boldFont;
		private int charWidth;
		private int charHeight;

		public void SetFont(FontFamily family, float emSize)
		{
			font = new Font(family, emSize);
			boldFont = new Font(family, emSize, FontStyle.Bold);
			
			SizeF size = GetCharSize(font, 'M');
			charWidth = (int)Math.Round(size.Width * 1f) - 1;
			charHeight = (int)Math.Round(size.Height * 1f) + 1;
			Height = charHeight;
			
			Invalidate();
		}
		
		private Scheme scheme = new Scheme();
		public Scheme Scheme
		{
			get { return scheme; }
			set
			{
				if (scheme != value)
				{
					scheme = value;
					Invalidate();
				}
			}
		}

		private static SizeF GetCharSize(Font font, char c)
		{
			Size sz2 = TextRenderer.MeasureText("<" + c.ToString() + ">", font);
			Size sz3 = TextRenderer.MeasureText("<>", font);
			return new SizeF(sz2.Width - sz3.Width + 1, font.Height);
		}
		
		public new void Invalidate()
		{
			if (InvokeRequired)
				BeginInvoke(new MethodInvoker(Invalidate));
			else
				base.Invalidate();
		}

		private PredictableList<Rectangle> rects = new PredictableList<Rectangle>();
		private Rectangle closeRect;
		private Rectangle? leftRect;
		private Rectangle? rightRect;
		private int leftIndent;
		private int rightIndent;
		private int offsetIndex;
		private bool needScrollToSelected;
		
		private void ScrollToSelectedIfNeed()
		{
			if (!needScrollToSelected)
				return;
			needScrollToSelected = false;
			int selectedIndex = list.IndexOf(list.Selected);
			if (selectedIndex != -1)
			{
				if (offsetIndex > selectedIndex)
				{
					offsetIndex = selectedIndex;
				}
				else
				{
					for (int i = offsetIndex; i < list.Count; i++)
					{
						offsetIndex = i;
						if (rects.buffer[selectedIndex].Right + GetOffsetX(i) < Width - rightIndent)
							break;
					}
				}
			}
		}
		
		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			int width = Width;
			int x = charWidth;
			int indent = charWidth / 2;
			
			g.FillRectangle(scheme.tabsBgBrush, 0, 0, width - rightIndent, charHeight - 1);
			g.DrawLine(scheme.lineNumberFgPen, 0, charHeight - 1, width, charHeight - 1);

			leftIndent = charWidth;
			if (text != null)
			{
				for (int j = 0; j < text.Length; j++)
				{
					g.DrawString(
						text[j] + "", font, scheme.fgBrush,
						10 - charWidth / 3 + j * charWidth, 0, stringFormat);
				}
				leftIndent += (charWidth + 1) * text.Length;
			}

			rects.Clear();
			for (int i = 0; i < list.Count; i++)
			{
				T value = list[i];
				string tabText = stringOf(value);
				Rectangle rect = new Rectangle(x - indent, 0, tabText.Length * charWidth + indent * 2, charHeight);
				x += (tabText.Length + 1) * charWidth;
				rects.Add(rect);
			}
			rightIndent = charHeight;
			if (x > width - leftIndent - rightIndent)
			{
				rightIndent += charWidth * 4;
				leftRect = new Rectangle(width - rightIndent, 0, charWidth * 2, charHeight);
				rightRect = new Rectangle(width - rightIndent + charWidth * 2, 0, charWidth * 2, charHeight);
				ScrollToSelectedIfNeed();
				if (offsetIndex < 0)
					offsetIndex = 0;
				else if (offsetIndex > rects.count - 1)
					offsetIndex = rects.count - 1;
				if (offsetIndex > 0)
				{
					for (int i = offsetIndex; i-- > 0;)
					{
						if (rects.buffer[rects.count - 1].Right + GetOffsetX(i) > width - rightIndent - 1)
							break;
						offsetIndex = i;
					}
				}
			}
			else
			{
				leftRect = null;
				rightRect = null;
				offsetIndex = 0;
			}
			
			int offsetX = GetOffsetX(offsetIndex);
			for (int i = Math.Max(0, offsetIndex); i < list.Count; i++)
			{
				T value = list[i];
				string tabText = stringOf(value);
				bool selected = object.Equals(list.Selected, value);
				Rectangle rect = rects.buffer[i];
				rect.X += offsetX;
				if (rect.X > width)
					break;
				
				if (selected)
				{
					g.FillRectangle(scheme.bgBrush, rect);
					g.DrawRectangle(scheme.lineNumberFgPen, rect);
				}
				else
				{
					g.FillRectangle(scheme.lineNumberBackground, rect);
					g.DrawRectangle(scheme.lineNumberFgPen, rect.X, rect.Y, rect.Width, rect.Height - 1);
				}
				for (int j = 0; j < tabText.Length; j++)
				{
					g.DrawString(
						tabText[j] + "", font, selected ? scheme.fgBrush : scheme.lineNumberForeground,
						rect.X - charWidth / 3 + j * charWidth + charWidth / 2, 0, stringFormat);
				}
				rects.Add(rect);
			}
			
			g.FillRectangle(scheme.tabsBgBrush, width - rightIndent, 0, rightIndent, charHeight - 1);
			g.DrawLine(scheme.lineNumberFgPen, width - rightIndent, charHeight - 1, width, charHeight - 1);
			
			closeRect = new Rectangle(width - charHeight, 0, charHeight, charHeight);
			g.DrawString("Х", font, scheme.tabsFgBrush, closeRect.X - charWidth / 3 + charWidth / 2, -1, stringFormat);
			
			if (leftRect != null)
				g.DrawString("<", font, scheme.tabsFgBrush, leftRect.Value.X - charWidth / 3 + charWidth / 2, -1, stringFormat);
			if (rightRect != null)
				g.DrawString(">", font, scheme.tabsFgBrush, rightRect.Value.X - charWidth / 3 + charWidth / 2, -1, stringFormat);

			base.OnPaint(e);
		}
		
		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			Point location = e.Location;
			if (closeRect.Contains(location))
			{
				if (CloseClick != null)
					CloseClick();
			}
			if (leftRect != null && leftRect.Value.Contains(location))
			{
				offsetIndex--;
				arrowTickDelta = -1;
				arrowTimer.Start();
				Invalidate();
			}
			if (rightRect != null && rightRect.Value.Contains(location))
			{
				offsetIndex++;
				arrowTickDelta = 1;
				arrowTimer.Start();
				Invalidate();
			}
			if (location.X < Width - rightIndent)
			{
				location.X -= GetOffsetX(offsetIndex);
				for (int i = 0; i < rects.count; i++)
				{
					if (rects.buffer[i].Contains(location))
					{
						if (i < list.Count)
							list.Selected = list[i];
						return;
					}
				}
			}
		}
		
		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			arrowTimer.Stop();
		}
		
		protected override void OnLostFocus(EventArgs e)
		{
			arrowTimer.Stop();
		}
		
		protected override void OnMouseDoubleClick(MouseEventArgs e)
		{
			base.OnMouseDoubleClick(e);
			Point location = e.Location;
			if (location.X < Width - rightIndent)
			{
				location.X -= GetOffsetX(offsetIndex);
				for (int i = 0; i < rects.count; i++)
				{
					if (rects.buffer[i].Contains(location))
					{
						if (i < list.Count)
						{
							if (TabDoubleClick != null)
								TabDoubleClick(list[i]);
						}
						return;
					}
				}
			}
		}
		
		private int GetOffsetX(int index)
		{
			return (index >= 0 && index < rects.count ? -rects.buffer[index].X : 0) + leftIndent;
		}
		
		private int arrowTickDelta;
		
		private void OnArrowTick(object senter, EventArgs e)
		{
			offsetIndex += arrowTickDelta;
			Invalidate();
		}
	}
}
