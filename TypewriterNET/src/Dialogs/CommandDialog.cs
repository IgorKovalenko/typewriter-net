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

public class CommandDialog : ADialog
{
	private TabBar<string> tabBar;
	private SplitLine splitLine;
	private MulticaretTextBox textBox;

	public CommandDialog(string name)
	{
		Name = name;
	}

	override protected void DoCreate()
	{
		tabBar = new TabBar<string>(null, TabBar<string>.DefaultStringOf);
		tabBar.CloseClick += OnCloseClick;
		tabBar.Text = Name;
		Controls.Add(tabBar);

		splitLine = new SplitLine();
		Controls.Add(splitLine);

		KeyMap frameKeyMap = new KeyMap();
		frameKeyMap.AddItem(new KeyItem(Keys.Escape, null, new KeyAction("&View\\Cancel command", DoCancel, null, false)));
		frameKeyMap.AddItem(new KeyItem(Keys.Enter, null, new KeyAction("&View\\Run command", DoRunCommand, null, false)));

		textBox = new MulticaretTextBox();
		textBox.KeyMap.AddAfter(KeyMap);
		textBox.KeyMap.AddAfter(frameKeyMap, 1);
		textBox.KeyMap.AddAfter(DoNothingKeyMap, -1);
		textBox.FocusedChange += OnTextBoxFocusedChange;
		Controls.Add(textBox);

		tabBar.MouseDown += OnTabBarMouseDown;
		InitResizing(tabBar, splitLine);
		Height = MinSize.Height;
	}

	override public bool Focused { get { return textBox.Focused; } }

	private void OnCloseClick()
	{
		DispatchNeedClose();
	}

	override protected void DoDestroy()
	{
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

	override protected void DoUpdateSettings(Settings settings, UpdatePhase phase)
	{
		if (phase == UpdatePhase.Raw)
		{
			settings.ApplySimpleParameters(textBox);
			tabBar.SetFont(settings.font.Value, settings.fontSize.Value);
		}
		else if (phase == UpdatePhase.Parsed)
		{
			textBox.Scheme = settings.ParsedScheme;
			tabBar.Scheme = settings.ParsedScheme;
		}
	}

	private bool DoCancel(Controller controller)
	{
		DispatchNeedClose();
		return true;
	}

	private bool DoRunCommand(Controller controller)
	{
		Commander commander = MainForm.commander;
		DispatchNeedClose();
		commander.Execute(textBox.Text);
		return true;
	}
}
