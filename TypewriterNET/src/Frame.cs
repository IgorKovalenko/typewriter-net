using System;
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
using MulticaretEditor;

public class Frame : AFrame
{
	private static Controller _emptyController;

	private static Controller GetEmptyController()
	{
		if (_emptyController == null)
		{
			_emptyController = new Controller(new LineArray());
			_emptyController.InitText("[Empty]");
			_emptyController.isReadonly = true;
		}
		return _emptyController;
	}

	private TabBar<Buffer> tabBar;
	private SplitLine splitLine;
	private MulticaretTextBox textBox;
	private SwitchList<Buffer> list;

	public Frame(string name, KeyMap keyMap, KeyMap doNothingKeyMap)
	{
		Name = name;

		list = new SwitchList<Buffer>();
		list.SelectedChange += OnTabSelected;

		tabBar = new TabBar<Buffer>(list, Buffer.StringOf);
		tabBar.Text = name;
		tabBar.CloseClick += OnCloseClick;
		tabBar.TabDoubleClick += OnTabDoubleClick;
		Controls.Add(tabBar);

		splitLine = new SplitLine();
		Controls.Add(splitLine);

		KeyMap frameKeyMap = new KeyMap();
		frameKeyMap.AddItem(new KeyItem(Keys.Tab, Keys.Control, new KeyAction("&View\\Switch tab", DoTabDown, DoTabModeChange, false)));
		frameKeyMap.AddItem(new KeyItem(Keys.Control | Keys.W, null, new KeyAction("&View\\Close tab", DoCloseTab, null, false)));

		textBox = new MulticaretTextBox();
		textBox.KeyMap.AddAfter(keyMap);
		textBox.KeyMap.AddAfter(frameKeyMap);
		textBox.KeyMap.AddAfter(doNothingKeyMap, -1);
		textBox.FocusedChange += OnTextBoxFocusedChange;
		textBox.Controller = GetEmptyController();
		Controls.Add(textBox);

		InitResizing(tabBar, splitLine);
		tabBar.MouseDown += OnTabBarMouseDown;
	}

	private bool DoTabDown(Controller controller)
	{
		list.Down();
		return true;
	}
	
	private void DoTabModeChange(Controller controller, bool mode)
	{
		if (mode)
			list.ModeOn();
		else
			list.ModeOff();
	}

	private bool DoCloseTab(Controller controller)
	{
		RemoveBuffer(list.Selected);
		return true;
	}

	override public Size MinSize { get { return new Size(tabBar.Height * 3, tabBar.Height); } }
	override public Frame AsFrame { get { return this; } }

	public Buffer SelectedBuffer { get { return list.Selected; } }

	override public bool Focused { get { return textBox.Focused; } }

	override public void Focus()
	{
		textBox.Focus();
	}

	private void OnTabBarMouseDown(object sender, EventArgs e)
	{
		textBox.Focus();
	}

	private void OnTextBoxFocusedChange()
	{
		if (Nest != null)
		{
			tabBar.Selected = textBox.Focused;
			Nest.MainForm.SetFocus(textBox, textBox.KeyMap);
		}
	}

	public string Title
	{
		get { return tabBar.Text; }
		set { tabBar.Text = value; }
	}

	override protected void OnResize(EventArgs e)
	{
		base.OnResize(e);
		int tabBarHeight = tabBar.Height;
		tabBar.Size = new Size(Width, tabBarHeight);
		splitLine.Location = new Point(Width - 10, tabBarHeight);
		splitLine.Size = new Size(10, Height - tabBarHeight);
		textBox.Location = new Point(0, tabBarHeight);
		textBox.Size = new Size(Width - 10, Height - tabBarHeight);
	}

	override public void UpdateSettings(Settings settings)
	{
		textBox.WordWrap = settings.WordWrap;
		textBox.ShowLineNumbers = settings.ShowLineNumbers;
		textBox.ShowLineBreaks = settings.ShowLineBreaks;
		textBox.HighlightCurrentLine = settings.HighlightCurrentLine;
		textBox.TabSize = settings.TabSize;
		textBox.LineBreak = settings.LineBreak;
		textBox.FontFamily = settings.FontFamily;
		textBox.FontSize = settings.FontSize;
		textBox.ScrollingIndent = settings.ScrollingIndent;
		textBox.ShowColorAtCursor = settings.ShowColorAtCursor;
		textBox.KeyMap.main.SetAltChars(settings.AltCharsSource, settings.AltCharsResult);
		
		tabBar.SetFont(settings.FontFamily, settings.FontSize);
	}

	public bool ContainsBuffer(Buffer buffer)
	{
		return list.Contains(buffer);
	}

	public void AddBuffer(Buffer buffer)
	{
		if (!list.Contains(buffer))
		{
			buffer.Controller.history.ChangedChange += OnChangedChange;
			list.Add(buffer);
			if (buffer.onAdd != null)
				buffer.onAdd(buffer, this);
		}
		else
		{
			list.Add(buffer);
		}
	}

	public void RemoveBuffer(Buffer buffer)
	{
		if (buffer == null)
			return;
		if (buffer.onRemove != null && !buffer.onRemove(buffer))
			return;
		buffer.Controller.history.ChangedChange -= OnChangedChange;
		list.Remove(buffer);
		if (list.Count == 0)
			Close();
	}

	private void OnChangedChange()
	{
		tabBar.Invalidate();
	}

	private void OnTabSelected()
	{
		Buffer buffer = list.Selected;
		textBox.Controller = buffer != null ? buffer.Controller : GetEmptyController();
	}

	private void OnCloseClick()
	{
		RemoveBuffer(list.Selected);
		if (list.Count == 0)
			Close();
	}

	private void OnTabDoubleClick(Buffer buffer)
	{
		RemoveBuffer(buffer);
	}

	private void Close()
	{
		if (Nest == null)
			return;
		MainForm mainForm = Nest.MainForm;
		Nest.AFrame = null;
		mainForm.DoResize();
	}
}
