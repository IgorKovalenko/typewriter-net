using System;
using System.Globalization;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Resources;
using System.Xml;
using MulticaretEditor;
using MulticaretEditor.Highlighting;
using MulticaretEditor.KeyMapping;

public class DialogManager
{
	public class DialogOwner<T> where T : ADialog
	{
		private readonly DialogManager manager;

		public DialogOwner(DialogManager manager)
		{
			this.manager = manager;
		}

		private T dialog;
		public T Dialog { get { return dialog; } }

		public void Open(T dialog, bool closeOther)
		{
			if (closeOther)
				manager.CloseDialogs();
			this.dialog = dialog;
			manager.closeMethods.Add(Close);
			AddBottomNest(dialog);
			dialog.NeedClose += OnNeedClose;
		}

		public bool SwitchOpen()
		{
			if (dialog == null || !dialog.Focused)
				return true;
			Close(true);
			return false;
		}

		public bool Close(bool changeFocus)
		{
			if (changeFocus && dialog != null)
			{
				if (manager.mainForm.LastFrame != null)
					manager.mainForm.LastFrame.Focus();
			}
			if (dialog != null)
			{
				manager.closeMethods.Remove(Close);
				dialog.Nest.Destroy();
				dialog = null;
				return true;
			}
			return false;
		}

		private void OnNeedClose()
		{
			Close(true);
		}

		private void AddBottomNest(ADialog dialog)
		{
			Nest nest = manager.frames.AddParentNode();
			nest.hDivided = false;
			nest.left = false;
			nest.isPercents = false;
			nest.size = 1;
			dialog.Create(nest);
			dialog.Focus();
		}
	}

	private MainForm mainForm;
	private TempSettings tempSettings;
	private FrameList frames;
	private List<Getter<bool, bool>> closeMethods;

	private DialogOwner<InfoDialog> info;
	private DialogOwner<CommandDialog> command;
	private CommandDialog.Data commandData = new CommandDialog.Data();
	private DialogOwner<FindDialog> find;
	private FindDialog.Data findData;
	private DialogOwner<FindDialog> findInFiles;
	private FindDialog.Data findInFilesData;
	private DialogOwner<ReplaceDialog> replace;
	private ReplaceDialog.Data replaceData;
	private DialogOwner<FindDialog> goToLine;
	private FindDialog.Data goToLineData;

	public DialogManager(MainForm mainForm, TempSettings tempSettings)
	{
		this.mainForm = mainForm;
		this.tempSettings = tempSettings;
		frames = mainForm.frames;
		closeMethods = new List<Getter<bool, bool>>();

		KeyMap keyMap = mainForm.KeyMap;
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.OemSemicolon, null, new KeyAction("&View\\Open/close command dialog", DoInputCommand, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.F, null, new KeyAction("F&ind\\Find...", DoFind, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.Shift | Keys.F, null, new KeyAction("F&ind\\Find in Files...", DoFindInFiles, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.H, null, new KeyAction("F&ind\\Replace...", DoReplace, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.G, null, new KeyAction("F&ind\\Go to line...", DoGoToLine, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Escape, null, new KeyAction("F&ind\\Close dialogs", DoCloseDialogs, null, false)));

		info = new DialogOwner<InfoDialog>(this);
		command = new DialogOwner<CommandDialog>(this);
		find = new DialogOwner<FindDialog>(this);
		findData = new FindDialog.Data(tempSettings.FindHistory);
		findInFiles = new DialogOwner<FindDialog>(this);
		findInFilesData = new FindDialog.Data(tempSettings.FindInFilesHistory);
		replace = new DialogOwner<ReplaceDialog>(this);
		replaceData = new ReplaceDialog.Data(tempSettings.ReplaceHistory);
		goToLine = new DialogOwner<FindDialog>(this);
		goToLineData = new FindDialog.Data(tempSettings.GoToLineHistory);
	}

	public void ShowInfo(string name, string text)
	{
		if (info.Dialog == null)
			info.Open(new InfoDialog(), false);
		info.Dialog.Name = "Command";
		info.Dialog.InitText(text);
	}

	private bool DoInputCommand(Controller controller)
	{
		if (command.SwitchOpen())
			command.Open(new CommandDialog(commandData, "Command"), true);
		return true;
	}

	private bool DoFind(Controller controller)
	{
		if (find.SwitchOpen())
			find.Open(new FindDialog(findData, tempSettings.FindParams, DoFindText, "Find"), true);
		return true;
	}

	private bool DoFindInFiles(Controller controller)
	{
		if (findInFiles.SwitchOpen())
			findInFiles.Open(new FindDialog(findInFilesData, tempSettings.FindParams, DoFindInFilesDialog, "Find in Files"), true);
		return true;
	}

	private bool DoReplace(Controller controller)
	{
		if (replace.SwitchOpen())
			replace.Open(new ReplaceDialog(replaceData, tempSettings.FindParams, DoFindText, "Replace"), true);
		return true;
	}

	private bool DoFindText(string text)
	{
		if (mainForm.LastFrame != null)
		{
			Controller lastController = mainForm.LastFrame.Controller;
			int index;
			int length;
			if (tempSettings.FindParams.regex)
			{
				string error;
				Regex regex = ParseRegex(text, out error);
				if (regex == null || error != null)
				{
					ShowInfo("FindInFiles", "Error: " + error);
					return true;
				}
				Match match = regex.Match(lastController.Lines.GetText(), lastController.Lines.LastSelection.Right);
				index = -1;
				length = text.Length;
				if (match.Success)
				{
					index = match.Index;
					length = match.Length;
				}
				else
				{
					match = regex.Match(lastController.Lines.GetText(), 0);
					if (match.Success)
					{
						index = match.Index;
						length = match.Length;
					}
				}
			}
			else
			{
				length = text.Length;
				CompareInfo ci = tempSettings.FindParams.ignoreCase ? CultureInfo.InvariantCulture.CompareInfo : null;
				index = ci != null ?
					ci.IndexOf(lastController.Lines.GetText(), text, lastController.Lines.LastSelection.Right, CompareOptions.IgnoreCase) :
					lastController.Lines.IndexOf(text, lastController.Lines.LastSelection.Right);
				if (index == -1)
					index = ci != null ?
						ci.IndexOf(lastController.Lines.GetText(), text, 0, CompareOptions.IgnoreCase) :
						lastController.Lines.IndexOf(text, 0);
			}
			if (index != -1)
			{
				lastController.PutCursor(lastController.Lines.PlaceOf(index), false);
				lastController.PutCursor(lastController.Lines.PlaceOf(index + length), true);
				mainForm.LastFrame.TextBox.MoveToCaret();
			}
		}
		return true;
	}

	private bool DoFindInFilesDialog(string text)
	{
		findInFiles.Close(true);
		string errors = new FindInFiles(mainForm)
			.Execute(text, tempSettings.FindParams, mainForm.Settings.findInFilesDir.Value, mainForm.Settings.findInFilesFilter.Value);
		if (errors != null)
			ShowInfo("FindInFiles", errors);
		return true;
	}

	private Place? GetLastPlace()
	{
		if (mainForm.LastFrame != null)
		{
			Controller lastController = mainForm.LastFrame.Controller;
			return lastController.Lines.PlaceOf(lastController.LastSelection.caret);
		}
		return null;
	}

	private bool DoGoToLine(Controller controller)
	{
		if (goToLine.SwitchOpen())
		{
			Place? place = GetLastPlace();
			if (string.IsNullOrEmpty(goToLineData.oldText) && place != null)
				goToLineData.oldText = place.Value.iLine + "";
			goToLine.Open(new FindDialog(
				goToLineData, tempSettings.FindParams, DoGoToLine,
				"Go to line" +
				(place != null ? " (current line: " + (place.Value.iLine + 1) + ", char: " + (place.Value.iChar + 1) + ")" : "")
			), true);
		}
		return true;
	}

	private bool DoGoToLine(string text)
	{
		int iLine;
		try
		{
			iLine = int.Parse(text);
		}
		catch (Exception e)
		{
			ShowInfo("Go to line", e.Message);
			return true;
		}
		iLine--;
		if (mainForm.LastFrame != null)
		{
			Controller lastController = mainForm.LastFrame.Controller;
			int iChar = lastController.Lines[iLine].GetFirstSpaces();
			lastController.PutCursor(new Place(iChar, iLine), false);
			mainForm.LastFrame.TextBox.MoveToCaret();
			mainForm.LastFrame.Focus();
		}
		return true;
	}

	private bool DoCloseDialogs(Controller controller)
	{
		return CloseDialogs();
	}

	private bool CloseDialogs()
	{
		bool result = false;
		foreach (Getter<bool, bool> closeMethod in closeMethods.ToArray())
		{
			if (closeMethod(false))
				result = true;
		}
		closeMethods.Clear();
		return result;
	}

	public static Regex ParseRegex(string regexText, out string error)
	{
		Regex regex = null;
		RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.Multiline;
		string rawRegex;
		if (regexText.Length > 2 && regexText[0] == '/' && regexText.LastIndexOf("/") > 1)
		{
			int lastIndex = regexText.LastIndexOf("/");
			string optionsText = regexText.Substring(lastIndex + 1);
			rawRegex = regexText.Substring(1, lastIndex - 1);
			for (int i = 0; i < optionsText.Length; i++)
			{
				char c = optionsText[i];
				if (c == 'i')
					options |= RegexOptions.IgnoreCase;
				else if (c == 's')
					options &= ~RegexOptions.Multiline;
				else if (c == 'e')
					options |= RegexOptions.ExplicitCapture;
				else
				{
					error = "Unsupported regex option: " + c;
					return null;
				}
			}
		}
		else
		{
			rawRegex = regexText;
		}
		try
		{
			regex = new Regex(rawRegex, options);
		}
		catch (Exception e)
		{
			error = "Incorrect regex: " + regexText + " - " + e.Message;
			return null;
		}
		error = null;
		return regex;
	}
}
