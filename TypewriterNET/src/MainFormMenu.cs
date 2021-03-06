using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using MulticaretEditor;

public class MainFormMenu : MainMenu
{
	public const string RecentItemName = "Recent";
	
	private MainForm mainForm;
	private List<string> names;

	public KeyMapNode node;

	public MainFormMenu(MainForm mainForm)
	{
		this.mainForm = mainForm;

		names = new List<string>();
		AddRootItem("&File", false, true);
		AddRootItem("&Edit", false, false);
		AddRootItem("F&ind", false, false);
		AddRootItem("&View", false, false);
		AddRootItem("Prefere&nces", true, false);
		AddRootItem("&?", false, false);
	}

	private void AddRootItem(string name, bool isOther, bool hasRecent)
	{
		if (!isOther)
			names.Add(name);
		MenuItems.Add(new DynamicMenuItem(this, name, isOther, hasRecent));
	}

	public class DynamicMenuItem : MenuItem
	{
		private MainFormMenu menu;
		private string name;
		private bool isOther;
		private bool hasRecent;

		public DynamicMenuItem(MainFormMenu menu, string name, bool isOther, bool hasRecent) : base(name)
		{
			this.name = name;
			this.menu = menu;
			this.isOther = isOther;
			this.hasRecent = hasRecent;

			MenuItems.Add(new MenuItem(" "));
			Popup += OnPopup;
		}

		private void OnPopup(object sender, EventArgs e)
		{
			MenuItems.Clear();
			menu.BuildItems(this, name, isOther);
			if (MenuItems.Count == 0)
			{
				MenuItem item = new MenuItem("[Empty]");
				item.Enabled = false;
				MenuItems.Add(item);
			}
			if (hasRecent)
			{
				MenuItem item = null;
				foreach (MenuItem itemI in MenuItems)
				{
					if (itemI.Text == RecentItemName)
					{
						item = itemI;
						break;
					}
				}
				if (item == null)
				{
					MenuItems.Add(new MenuItem("-"));
					item = new MenuItem(RecentItemName);
					MenuItems.Add(item);
				}
				BuildRecentItems(item);
			}
		}
		
		private void BuildRecentItems(MenuItem root)
		{
			TempSettings tempSettings = menu.mainForm.TempSettings;
			if (tempSettings == null)
			{
				MenuItem item = new MenuItem("[Not loaded yet]");
				item.Enabled = false;
				MenuItems.Add(item);
				return;
			}
			string currendDir = Directory.GetCurrentDirectory().ToLowerInvariant() + "\\";
			List<string> files = tempSettings.GetRecentlyFiles();
			int count = 0;
			for (int i = files.Count; i--> 0;)
			{
				string file = files[i];
				++count;
				if (count > 20)
				{
					break;
				}
				MenuItem item = new MenuItem(
					file.ToLowerInvariant().StartsWith(currendDir) ? file.Substring(currendDir.Length) : file,
					new MenuItemRecentFileDelegate(menu.mainForm, file).OnClick);
				root.MenuItems.Add(item);
			}
		}
	}

	private void BuildItems(MenuItem root, string rootName, bool isOther)
	{
		if (node == null)
			return;
		List<KeyAction> actions = new List<KeyAction>();
		Dictionary<KeyAction, bool> actionSet = new Dictionary<KeyAction, bool>();
		Dictionary<KeyAction, List<KeyItem>> keysByAction = new Dictionary<KeyAction, List<KeyItem>>();
		List<KeyItem> keyItems = new List<KeyItem>();
		foreach (KeyMap keyMapI in node.ToList())
		{
			keyItems.AddRange(keyMapI.items);
			foreach (KeyItem keyItem in keyMapI.items)
			{
				if (keyItem.action != KeyAction.Nothing && !actionSet.ContainsKey(keyItem.action))
				{
					actionSet.Add(keyItem.action, true);
					actions.Add(keyItem.action);
				}
			}
		}
		foreach (KeyItem keyItem in keyItems)
		{
			List<KeyItem> list;
			keysByAction.TryGetValue(keyItem.action, out list);
			if (list == null)
			{
				list = new List<KeyItem>();
				keysByAction[keyItem.action] = list;
			}
			list.Add(keyItem);
		}
		Dictionary<string, Menu> itemByPath = new Dictionary<string, Menu>();
		KeysConverter keysConverter = new KeysConverter();
		foreach (KeyAction action in actions)
		{
			string itemName = GetMenuItemName(action.name);
			List<KeyItem> keys;
			keysByAction.TryGetValue(action, out keys);
			if (keys != null && keys.Count > 0)
			{
				string shortcutText = GetShortcutText(action, keys, keysConverter);
				itemName += (action.getText != null ? action.getText() : "");
				itemName += (!string.IsNullOrEmpty(shortcutText) ? "\t" + shortcutText : "");
			}
			bool filtered;
			if (isOther)
			{
				filtered = true;
				foreach (string name in names)
				{
					if (action.name.StartsWith(name + "\\"))
					{
						filtered = false;
						break;
					}
				}
			}
			else
			{
				filtered = action.name.StartsWith(rootName + "\\");
			}
			if (filtered)
			{
				MenuItem item = new MenuItem(itemName, new MenuItemActionDelegate(mainForm, action).OnClick);
				GetMenuItemParent(root, rootName, action.name, itemByPath).MenuItems.Add(item);
			}
		}
	}

	public static string GetShortcutText(KeyAction action, List<KeyItem> keys, KeysConverter keysConverter)
	{
		string text = "";
		bool first = true;
		foreach (KeyItem keyItem in keys)
		{
			if (keyItem.keys == Keys.None && !keyItem.doubleClick)
				continue;
			text += first ? "\t" : " / ";
			first = false;
			if (action.doOnModeChange != null)
				text += "[";
			bool hasPrev = false;
			if (keyItem.keys != Keys.None)
			{
				if (keyItem.keys == Keys.Alt)
				{
					text += "Alt";
				}
				if (keyItem.keys == (Keys.Control | Keys.OemSemicolon))
				{
					text += "Ctrl+;";
				}
				else if (keyItem.keys == (Keys.Control | Keys.Shift | Keys.OemSemicolon))
				{
					text += "Ctrl+Shift+;";
				}
				else if (keyItem.keys == (Keys.Control | Keys.OemOpenBrackets))
				{
					text += "Ctrl+[";
				}
				else if (keyItem.keys == (Keys.Control | Keys.OemCloseBrackets))
				{
					text += "Ctrl+]";
				}
				else if (keyItem.keys == (Keys.Control | Keys.OemPipe))
				{
					text += "Ctrl+\\";
				}
				else
				{
					text += keysConverter.ConvertToString(keyItem.keys);
				}
				hasPrev = true;
			}
			if (keyItem.doubleClick)
				text += (hasPrev ? "+" : "") + "DoubleClick";
			if (action.doOnModeChange != null)
				text += "]";
		}
		return text;
	}

	private Menu GetMenuItemParent(MenuItem root, string rootName, string path, Dictionary<string, Menu> itemByPath)
	{
		string parentPath = GetMenuItemParentPath(path);
		if (parentPath == rootName || string.IsNullOrEmpty(parentPath))
			return root;
		Menu parent;
		itemByPath.TryGetValue(parentPath, out parent);
		if (parent != null)
			return parent;
		MenuItem item = new MenuItem(GetMenuItemName(parentPath));
		itemByPath[parentPath] = item;
		GetMenuItemParent(root, rootName, parentPath, itemByPath).MenuItems.Add(item);
		return item;
	}

	private static string GetMenuItemParentPath(string path)
	{
		int index = path.LastIndexOf("\\");
		if (index == -1)
			return "";
		return path.Substring(0, index);
	}

	private static string GetMenuItemName(string path)
	{
		int index = path.LastIndexOf("\\");
		if (index == -1)
			return path;
		return path.Substring(index + 1);
	}

	public class MenuItemActionDelegate
	{
		private MainForm mainForm;
		private KeyAction action;
		
		public MenuItemActionDelegate(MainForm mainForm, KeyAction action)
		{
			this.mainForm = mainForm;
			this.action = action;
		}
		
		public void OnClick(object sender, EventArgs e)
		{
			Controller controller = mainForm.FocusedController;
			if (action.doOnModeChange != null)
				action.doOnModeChange(controller, true);
			action.doOnDown(controller);
			if (action.doOnModeChange != null)
				action.doOnModeChange(controller, false);
		}
	}
	
	public class MenuItemRecentFileDelegate
	{
		private MainForm mainForm;
		private string file;
		
		public MenuItemRecentFileDelegate(MainForm mainForm, string file)
		{
			this.mainForm = mainForm;
			this.file = file;
		}
		
		public void OnClick(object sender, EventArgs e)
		{
			mainForm.LoadFile(file);
		}
	}
}
