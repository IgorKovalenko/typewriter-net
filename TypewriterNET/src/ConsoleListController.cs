﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using MulticaretEditor;
using MulticaretEditor.KeyMapping;
using MulticaretEditor.Highlighting;
using TypewriterNET.Frames;

namespace TypewriterNET
{
	public class ConsoleListController : ISearchableFrame
	{
		private SwitchList<ConsoleInfo> list;
	    private TabBar<ConsoleInfo> tabBar;
	    private MulticaretTextBox textBox;
	    
	    private List<KeyAction> actions;
		private TableLayoutPanel table;
	    
		public ConsoleListController(TableLayoutPanel table, MainContext mainContext)
		{
			this.table = table;

			list = new SwitchList<ConsoleInfo>();
			tabBar = new TabBar<ConsoleInfo>(list, ConsoleInfo.StringOf);
			tabBar.Margin = new Padding();
			tabBar.Dock = DockStyle.Bottom;
			tabBar.Visible = false;
			table.Controls.Add(tabBar, 0, 2);
			
			textBox = new MulticaretTextBox();
			textBox.Dock = DockStyle.Bottom;
			textBox.Margin = new Padding();
			textBox.Height = 100;
			textBox.Visible = false;
			textBox.WordWrap = true;
			table.Controls.Add(textBox, 0, 3);
			
			list.SelectedChange += OnSelectedChange;

			tabBar.TabDoubleClick += OnTabBarDoubleClick;
			tabBar.CloseClick += OnTabBarCloseClick;
			tabBar.MouseDown += OnTabBarMouseDown;
			tabBar.MouseUp += OnTabBarMouseUp;
			
			actions = new List<KeyAction>();
			
			textBox.KeyMap.AddAfter(mainContext.keyMap);
			textBox.KeyMap.AddAfter(mainContext.doNothingKeyMap);
		}

		public MulticaretTextBox TextBox { get { return textBox; } }

		public void AddSearchPanel(Control control)
		{
	        table.Controls.Add(control, 0, 6);
		}

		public void RemoveSearchPanel(Control control)
		{
	        table.Controls.Remove(control);
		}

		public int AreaHeight
		{
			get { return textBox.Height; }
			set { textBox.Height = Math.Max(0, value); }
		}
		
		private KeyAction AddAction(string name, Getter<Controller, bool> doOnDown, Setter<Controller, bool> doOnModeChange, bool needScroll)
		{
			KeyAction action = new KeyAction(name, doOnDown, doOnModeChange, needScroll);
			actions.Add(action);
			return action;
		}
		
		public void UpdateParameters(Config config)
		{
			textBox.WordWrap = config.WordWrap;
			textBox.ShowLineBreaks = config.ShowLineBreaks;
			textBox.TabSize = config.TabSize;
			textBox.LineBreak = config.LineBreak;
			textBox.FontFamily = config.FontFamily;
			textBox.FontSize = config.FontSize;
			
			tabBar.SetFont(config.FontFamily, config.FontSize);
		}
		
		public void UpdateScheme(Scheme scheme)
		{
			textBox.Scheme = scheme;
			tabBar.Scheme = scheme;
		}

		private bool visible;
		public bool Visible { get { return visible; } }
		
		private void SetVisible(bool value)
		{
			if (visible != value)
			{
				visible = value;
				tabBar.Visible = value;
				textBox.Visible = value;
			}
		}
		
		private Setter hideCallback;
		
		public void Show(Setter hideCallback)
		{
			this.hideCallback = hideCallback;
			SetVisible(true);
			textBox.Focus();
		}
		
		public void Hide()
		{
			if (!visible)
				return;
			SetVisible(false);
			if (hideCallback != null)
			{
				Setter callback = hideCallback;
				hideCallback = null;
				callback();
			}
		}
		
		public ConsoleInfo SelectedConsole
		{
			get { return list.Selected; }
			set { list.Selected = value; }
		}
		
		public void AddConsole(ConsoleInfo info)
		{
			list.Add(info);
		}
		
		public void RemoveConsole(ConsoleInfo info)
		{
			list.Remove(info);
		}
		
		public bool ContainsConsole(ConsoleInfo info)
		{
			return list.Contains(info);
		}
		
		private void OnSelectedChange()
	    {
			textBox.Controller = list.Selected != null ? list.Selected.Controller : null;
			if (textBox.Controller == null)
				Hide();
	    }
		
		private void OnTabBarDoubleClick(ConsoleInfo info)
		{
			list.Remove(info);
		}
		
		private void OnTabBarCloseClick()
		{
			list.Remove(list.Selected);
		}

		private int resizeStartY;

		private void OnTabBarMouseDown(object sender, MouseEventArgs e)
		{
			resizeStartY = e.Y;
			tabBar.MouseMove += OnTabBarMouseMove;
		}

		private void OnTabBarMouseUp(object sender, MouseEventArgs e)
		{
			tabBar.MouseMove -= OnTabBarMouseMove;
		}

		private void OnTabBarMouseMove(object sender, MouseEventArgs e)
		{
			AreaHeight += resizeStartY - e.Y;
		}
	}
}
