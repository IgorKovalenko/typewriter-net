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
using MulticaretEditor;

public class ViFindDialog : ADialog
{
	private FindDialog.Data data;
	private FindParams findParams;
	private Getter<string, Pattern, bool> doFind;
	private TabBar<NamedAction> tabBar;
	private MulticaretTextBox textBox;
	private MonospaceLabel label;

	public ViFindDialog(FindDialog.Data data, FindParams findParams, Getter<string, Pattern, bool> doFind)
	{
		this.data = data;
		this.findParams = findParams;
		this.doFind = doFind;
		Name = "Find";
	}

	override protected void DoCreate()
	{
		SwitchList<NamedAction> list = new SwitchList<NamedAction>();
		
		KeyMap frameKeyMap = new KeyMap();
		frameKeyMap.AddItem(new KeyItem(Keys.Escape, null,
			new KeyAction("F&ind\\Cancel find", DoCancel, null, false)));
		frameKeyMap.AddItem(new KeyItem(Keys.Enter, null,
			new KeyAction("F&ind\\Find next", DoFindNext, null, false)));
		if (data.history != null)
		{
			KeyAction prevAction = new KeyAction("F&ind\\Previous pattern", DoPrevPattern, null, false);
			KeyAction nextAction = new KeyAction("F&ind\\Next pattern", DoNextPattern, null, false);
			frameKeyMap.AddItem(new KeyItem(Keys.Up, null, prevAction));
			frameKeyMap.AddItem(new KeyItem(Keys.Down, null, nextAction));
			frameKeyMap.AddItem(new KeyItem(Keys.Control | Keys.P, null, prevAction));
			frameKeyMap.AddItem(new KeyItem(Keys.Control | Keys.N, null, nextAction));
		}
		
		label = new MonospaceLabel();
		label.Text = "/";
		Controls.Add(label);
		
		KeyMap beforeKeyMap = new KeyMap();
		textBox = new MulticaretTextBox(true);
		textBox.KeyMap.AddBefore(beforeKeyMap);
		textBox.KeyMap.AddAfter(KeyMap);
		textBox.KeyMap.AddAfter(frameKeyMap, 1);
		textBox.KeyMap.AddAfter(DoNothingKeyMap, -1);
		textBox.FocusedChange += OnTextBoxFocusedChange;
		textBox.TextChange += OnTextChange;
		Controls.Add(textBox);
		
		tabBar = new TabBar<NamedAction>(list, TabBar<NamedAction>.DefaultStringOf, NamedAction.HintOf);
		tabBar.Text = "Find";
		tabBar.ButtonMode = true;
		tabBar.RightHint = findParams != null ? findParams.GetIndicationHint() : null;
		tabBar.TabClick += OnTabClick;
		tabBar.CloseClick += OnCloseClick;
		tabBar.MouseDown += OnTabBarMouseDown;
		Controls.Add(tabBar);
		
		InitResizing(tabBar, null);
		Height = MinSize.Height;
		UpdateFindParams();
	}
	
	private void UpdateFindParams()
	{
		tabBar.Text2 = findParams != null ? findParams.GetIndicationText() : "";
	}
	
	private void OnTabClick(NamedAction action)
	{
		action.Execute(textBox.Controller);
	}
	
	private void OnTabBarMouseDown(object sender, EventArgs e)
	{
		textBox.Focus();
	}
	
	private void OnTextChange()
	{
		Nest.size = textBox.CharHeight * (textBox.Controller != null ? textBox.GetScrollSizeY() : 1);
		textBox.Controller.NeedScrollToCaret();
		SetNeedResize();
	}

	override public bool Focused { get { return textBox.Focused; } }

	private void OnCloseClick()
	{
		DispatchNeedClose();
	}

	override protected void DoDestroy()
	{
		data.oldText = textBox.Text;
	}

	override public Size MinSize { get { return new Size(tabBar.Height * 3, tabBar.Height + textBox.CharHeight); } }

	override public void Focus()
	{
		textBox.Focus();

		Frame lastFrame = Nest.MainForm.LastFrame;
		Controller lastController = lastFrame != null ? lastFrame.Controller : null;
		if (lastController != null)
		{
			textBox.Text = lastController.Lines.LastSelection.Empty ?
				data.oldText :
				lastController.Lines.GetText(lastController.Lines.LastSelection.Left, lastController.Lines.LastSelection.Count);
			textBox.Controller.SelectAllToEnd();
		}
	}

	private void OnTextBoxFocusedChange()
	{
		if (Destroyed)
			return;
		tabBar.Selected = textBox.Focused;
		if (textBox.Focused)
			Nest.MainForm.SetFocus(textBox, textBox.KeyMap, null);
	}

	override protected void OnResize(EventArgs e)
	{
		base.OnResize(e);
		int tabBarHeight = tabBar.Height;
		tabBar.Size = new Size(Width, tabBarHeight);
		label.Location = new Point(0, tabBarHeight);
		textBox.Location = new Point(textBox.CharWidth, tabBarHeight);
		textBox.Size = new Size(Width - textBox.CharWidth, Height - tabBarHeight + 1);
	}

	override protected void DoUpdateSettings(Settings settings, UpdatePhase phase)
	{
		if (phase == UpdatePhase.Raw)
		{
			settings.ApplySimpleParameters(textBox, null);
		}
		else if (phase == UpdatePhase.Parsed)
		{
			textBox.Scheme = settings.ParsedScheme;
			label.TextColor = settings.ParsedScheme.fgColor;
		}
		else if (phase == UpdatePhase.FindParams)
		{
			UpdateFindParams();
		}
	}

	private bool DoCancel(Controller controller)
	{
		DispatchNeedClose();
		return true;
	}

	private bool DoFindNext(Controller controller)
	{
		string text = textBox.Text;
		if (data.history != null)
			data.history.Add(text);
		return doFind(text, new Pattern(text, findParams.regex, findParams.ignoreCase));
	}
	
	private bool DoPrevPattern(Controller controller)
	{
		return GetHistoryPattern(true);
	}

	private bool DoNextPattern(Controller controller)
	{
		return GetHistoryPattern(false);
	}

	private bool GetHistoryPattern(bool isPrev)
	{
		string text = textBox.Text;
		string newText = data.history.Get(text, isPrev);
		if (newText != text)
		{
			textBox.Text = newText;
			textBox.Controller.ClearMinorSelections();
			textBox.Controller.LastSelection.anchor = textBox.Controller.LastSelection.caret = newText.Length;
		}
		return true;
	}
}
