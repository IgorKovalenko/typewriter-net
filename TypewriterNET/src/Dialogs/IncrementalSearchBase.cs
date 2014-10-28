using System;
using System.IO;
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

public class IncrementalSearchBase : ADialog
{
	private SwitchList<string> list;
	private TabBar<string> tabBar;
	private SplitLine splitLine;
	private MulticaretTextBox variantsTextBox;
	private MulticaretTextBox textBox;

	private string submenu;

	public IncrementalSearchBase(string submenu)
	{
		this.submenu = submenu;
	}

	override protected void DoCreate()
	{
		list = new SwitchList<string>();
		tabBar = new TabBar<string>(list, TabBar<string>.DefaultStringOf);
		tabBar.Text = "Search";
		tabBar.CloseClick += OnCloseClick;
		Controls.Add(tabBar);

		splitLine = new SplitLine();
		Controls.Add(splitLine);

		KeyMap textKeyMap = new KeyMap();
		KeyMap variantsKeyMap = new KeyMap();
		{
			KeyAction action = new KeyAction("F&ind\\" + submenu + "\\Close search", DoClose, null, false);
			textKeyMap.AddItem(new KeyItem(Keys.Escape, null, action));
			variantsKeyMap.AddItem(new KeyItem(Keys.Escape, null, action));
		}
		{
			textKeyMap.AddItem(new KeyItem(Keys.Up, null, new KeyAction("F&ind\\" + submenu + "\\Select searching file up", DoUp, null, false)));
			textKeyMap.AddItem(new KeyItem(Keys.Down, null, new KeyAction("F&ind\\" + submenu + "\\Select searching file down", DoDown, null, false)));
		}
		{
			KeyAction action = new KeyAction("F&ind\\" + submenu + "\\Next field", DoNextField, null, false);
			textKeyMap.AddItem(new KeyItem(Keys.Tab, null, action));
			variantsKeyMap.AddItem(new KeyItem(Keys.Tab, null, action));
		}
		{
			KeyAction action = new KeyAction("F&ind\\" + submenu + "\\Open searching file", DoExecute, null, false);
			textKeyMap.AddItem(new KeyItem(Keys.Enter, null, action));
			textKeyMap.AddItem(new KeyItem(Keys.None, null, action).SetDoubleClick(true));
			variantsKeyMap.AddItem(new KeyItem(Keys.Enter, null, action));
			variantsKeyMap.AddItem(new KeyItem(Keys.None, null, action).SetDoubleClick(true));
		}

		variantsTextBox = new MulticaretTextBox();
		variantsTextBox.KeyMap.AddAfter(KeyMap);
		variantsTextBox.KeyMap.AddAfter(variantsKeyMap, 1);
		variantsTextBox.KeyMap.AddAfter(DoNothingKeyMap, -1);
		variantsTextBox.FocusedChange += OnTextBoxFocusedChange;
		variantsTextBox.Controller.isReadonly = true;
		Controls.Add(variantsTextBox);

		textBox = new MulticaretTextBox();
		textBox.KeyMap.AddAfter(KeyMap);
		textBox.KeyMap.AddAfter(textKeyMap, 1);
		textBox.KeyMap.AddAfter(DoNothingKeyMap, -1);
		textBox.FocusedChange += OnTextBoxFocusedChange;
		textBox.TextChange += OnTextBoxTextChange;
		Controls.Add(textBox);

		SetTextBoxParameters();

		tabBar.MouseDown += OnTabBarMouseDown;
		InitResizing(tabBar, splitLine);
		Height = MinSize.Height;

		Name = Directory.GetCurrentDirectory();
		Prebuild();
		InitVariantsText(GetVariantsText(textBox.Text));
	}

	private void OnCloseClick()
	{
		DispatchNeedClose();
	}

	override protected void DoDestroy()
	{
	}

	new public string Name
	{
		get { return list.Count > 0 ? list[0] : ""; }
		set
		{
			list.Clear();
			list.Add(value);
		}
	}

	override public Size MinSize { get { return new Size(tabBar.Height * 3, tabBar.Height * 2); } }

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
		if (Destroyed)
			return;
		tabBar.Selected = textBox.Focused;
		if (textBox.Focused)
			Nest.MainForm.SetFocus(textBox, textBox.KeyMap, null);
	}

	private void OnTextBoxTextChange()
	{
		InitVariantsText(GetVariantsText(textBox.Text));
	}

	override public bool Focused { get { return textBox.Focused; } }

	override protected void OnResize(EventArgs e)
	{
		base.OnResize(e);
		int tabBarHeight = tabBar.Height;
		tabBar.Size = new Size(Width, tabBarHeight);
		splitLine.Location = new Point(Width - 10, tabBarHeight);
		splitLine.Size = new Size(10, Height - tabBarHeight);
		variantsTextBox.Location = new Point(0, tabBarHeight);
		variantsTextBox.Size = new Size(Width - 10, Height - tabBarHeight - variantsTextBox.CharHeight - 2);
		variantsTextBox.Controller.NeedScrollToCaret();
		textBox.Location = new Point(0, Height - variantsTextBox.CharHeight);
		textBox.Size = new Size(Width - 10, variantsTextBox.CharHeight);
	}

	override protected void DoUpdateSettings(Settings settings, UpdatePhase phase)
	{
		if (phase == UpdatePhase.Raw)
		{
			settings.ApplySimpleParameters(variantsTextBox);
			settings.ApplySimpleParameters(textBox);
			SetTextBoxParameters();
			tabBar.SetFont(settings.font.Value, settings.fontSize.Value);
		}
		else if (phase == UpdatePhase.Parsed)
		{
			BackColor = settings.ParsedScheme.tabsBgColor;
			variantsTextBox.Scheme = settings.ParsedScheme;
			textBox.Scheme = settings.ParsedScheme;
			tabBar.Scheme = settings.ParsedScheme;
		}
	}

	private void SetTextBoxParameters()
	{
		variantsTextBox.ShowLineNumbers = false;
		variantsTextBox.HighlightCurrentLine = true;
		variantsTextBox.WordWrap = true;

		textBox.ShowLineNumbers = false;
		textBox.HighlightCurrentLine = false;
	}

	private bool DoClose(Controller controller)
	{
		DispatchNeedClose();
		return true;
	}

	private bool DoNextField(Controller controller)
	{
		if (controller == textBox.Controller && variantsTextBox.Controller.Lines.charsCount != 0)
			variantsTextBox.Focus();
		else
			textBox.Focus();
		return true;
	}

	private void InitVariantsText(string text)
	{
		variantsTextBox.Controller.InitText(text);
		variantsTextBox.Controller.ClearMinorSelections();
		Selection selection = variantsTextBox.Controller.LastSelection;
		Place place = new Place(0, variantsTextBox.Controller.Lines.LinesCount - 1);
		selection.anchor = selection.caret = variantsTextBox.Controller.Lines.IndexOf(place);
		variantsTextBox.Invalidate();
		Nest.size = tabBar.Height + variantsTextBox.CharHeight *
			(!string.IsNullOrEmpty(text) && variantsTextBox.Controller != null ? variantsTextBox.GetScrollSizeY() + 1 : 1) + 4;
		variantsTextBox.Controller.NeedScrollToCaret();
		SetNeedResize();
	}

	private bool DoUp(Controller controller)
	{
		variantsTextBox.Controller.MoveUp(false);
		variantsTextBox.Controller.NeedScrollToCaret();
		variantsTextBox.Invalidate();
		return true;
	}

	private bool DoDown(Controller controller)
	{
		variantsTextBox.Controller.MoveDown(false);
		variantsTextBox.Controller.NeedScrollToCaret();
		variantsTextBox.Invalidate();
		return true;
	}

	private bool DoExecute(Controller controller)
	{
		Place place = variantsTextBox.Controller.Lines.PlaceOf(variantsTextBox.Controller.LastSelection.caret);
		string lineText = variantsTextBox.Controller.Lines[place.iLine].Text.Trim();
		Execute(place.iLine, lineText);
		return true;
	}

	virtual protected void Prebuild()
	{
	}

	virtual protected string GetVariantsText(string text)
	{
		return "";
	}

	virtual protected void Execute(int line, string lineText)
	{
	}
}
