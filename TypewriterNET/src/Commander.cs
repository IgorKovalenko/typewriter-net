using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using MulticaretEditor;
using MulticaretEditor.Highlighting;

public class Commander
{
	public class Command
	{
		public readonly string name;
		public readonly string argNames;
		public readonly string desc;
		public readonly Setter<string> execute;

		public Command(string name, string argNames, string desc, Setter<string> execute)
		{
			this.name = name;
			this.argNames = argNames;
			this.desc = desc;
			this.execute = execute;
		}
	}

	private MainForm mainForm;
	private TempSettings tempSettings;
	private Settings settings;

	private readonly List<Command> commands = new List<Command>();

	private StringList history;
	public StringList History { get { return history; } }

	private string FirstWord(string text, out string tail)
	{
		string first;
		int index = text.IndexOf(' ');
		if (index != -1)
		{
			first = text.Substring(0, index);
			tail = text.Substring(index + 1);
		}
		else
		{
			first = text;
			tail = "";
		}
		return first;
	}

	public void Execute(string text)
	{
		if (string.IsNullOrEmpty(text))
			return;
		string args;
		string name = FirstWord(text, out args);
		if (name == "")
			return;
		history.Add(text);
		Command command = null;
		foreach (Command commandI in commands)
		{
			if (commandI.name == name)
			{
				command = commandI;
				break;
			}
		}
		if (command != null)
		{
			command.execute(args);
		}
		else if (settings[name] != null)
		{
			if (args != "")
			{
				string errors = settings[name].SetText(args);
				settings.DispatchChange();
				if (!string.IsNullOrEmpty(errors))
					mainForm.Dialogs.ShowInfo("Error assign of \"" + name + "\"", errors);
			}
			else
			{
				mainForm.Dialogs.ShowInfo("Value of \"" + name + "\"", settings[name].Text);
			}
		}
		else if (name.StartsWith("!!"))
		{
			string commandText = text.Substring(2);
			if (ReplaceVars(ref commandText))
			{
				Process p = new Process();
				p.StartInfo.UseShellExecute = true;
				p.StartInfo.FileName = "cmd.exe";
				p.StartInfo.Arguments = "/C " + commandText;
				p.Start();
			}
		}
		else if (name.StartsWith("!"))
		{
			string commandText = text.Substring(1).Trim();
			if (ReplaceVars(ref commandText))
				ExecuteShellCommand(commandText);
		}
		else
		{
			mainForm.Dialogs.ShowInfo("Error", "Unknown command/property \"" + name + "\"");
		}
	}

	private bool ReplaceVars(ref string commandText)
	{
		if (commandText.Contains(RunShellCommand.FileVar))
		{
			Buffer lastBuffer = mainForm.LastBuffer;
			if (lastBuffer == null || string.IsNullOrEmpty(lastBuffer.FullPath))
			{
				mainForm.Dialogs.ShowInfo("Error", "No opened file in current frame for replace " + RunShellCommand.FileVar);
				return false;
			}
			commandText = commandText.Replace(RunShellCommand.FileVar, lastBuffer.FullPath);
		}
		return true;
	}

	public string GetHelpText()
	{
		StringBuilder builder = new StringBuilder();
		builder.AppendLine("# Commands");
		builder.AppendLine();

		TextTable table = new TextTable().SetMaxColWidth(40);
		table.Add("Command").Add("Arguments").Add("Description");
		table.AddLine();
		table.Add("!command").Add("*").Add("Run shell command");
		table.NewRow();
		table.Add("!!command").Add("*").Add("Execute without output capture");
		table.NewRow();
		table.Add("").Add("").Add("Variables: ");
		table.NewRow();
		table.Add("").Add("").Add("  " + RunShellCommand.FileVar + " - current file full path");
		foreach (Command command in commands)
		{
			table.NewRow();
			table.Add(command.name)
				.Add(!string.IsNullOrEmpty(command.desc) ? command.argNames : "")
				.Add(!string.IsNullOrEmpty(command.desc) ? command.desc : "");
		}
		builder.Append(table);

		return builder.ToString();
	}

	public void Init(MainForm mainForm, Settings settings, TempSettings tempSettings)
	{
		this.mainForm = mainForm;
		this.settings = settings;
		this.tempSettings = tempSettings;

		history = tempSettings.CommandHistory;
		commands.Add(new Command("help", "", "Open/close tab with help text", DoHelp));
		commands.Add(new Command("cd", "path", "Change/show current directory", DoChangeCurrentDirectory));
		commands.Add(new Command("exit", "", "Close window", DoExit));
		commands.Add(new Command("lclear", "", "Clear editor log", DoClearLog));
		commands.Add(new Command("reset", "name", "Reset property", DoResetProperty));
		commands.Add(new Command("edit", "file", "Edit file/new file", DoEditFile));
		commands.Add(new Command("open", "file", "Open file", DoOpenFile));
		commands.Add(new Command("md", "directory", "Create directory", DoCreateDirectory));
		commands.Add(new Command("encode", "encoding[ bom]", "Change/show encoding to save", DoChangeEncodingToSave));
		commands.Add(new Command("reload", "encoding[ bom]/reset", "Reload file in custom encoding, reset - detect encoding anew",
			DoReloadInCustomEncoding));
	}

	private void DoHelp(string args)
	{
		mainForm.ProcessHelp();
	}

	private void DoExit(string args)
	{
		mainForm.Close();
	}

	private void DoClearLog(string args)
	{
		mainForm.Log.Clear();
	}

	private void DoResetProperty(string args)
	{
		if (args == "")
		{
			settings.Reset();
			settings.DispatchChange();
		}
		else if (settings[args] != null)
		{
			settings[args].Reset();
			settings.DispatchChange();
			mainForm.Dialogs.ShowInfo("Value of \"" + args + "\"", settings[args].Text);
		}
		else
		{
			mainForm.Dialogs.ShowInfo("Error", "Unknown property \"" + args + "\"");
		}
	}

	private void DoChangeCurrentDirectory(string path)
	{
		string error = "";
		if (string.IsNullOrEmpty(path) || mainForm.SetCurrentDirectory(path, out error))
			mainForm.Dialogs.ShowInfo("Current directory", Directory.GetCurrentDirectory());
		else
			mainForm.Dialogs.ShowInfo("Error", error);
	}

	private void ExecuteShellCommand(string commandText)
	{
		new RunShellCommand(mainForm).Execute(commandText, settings.shellRegexList.Value);
	}

	private void DoEditFile(string file)
	{
		Buffer buffer = mainForm.ForcedLoadFile(file);
		buffer.needSaveAs = false;
	}

	private void DoOpenFile(string file)
	{
		mainForm.LoadFile(file);
	}

	private void DoCreateDirectory(string dir)
	{
		try
		{
			DirectoryInfo info = Directory.CreateDirectory(dir);
			mainForm.Dialogs.ShowInfo("Created directory", info.FullName);
		}
		catch (Exception e)
		{
			mainForm.Dialogs.ShowInfo("Error", e.Message);
		}
	}

	private void DoChangeEncodingToSave(string raw)
	{
		Buffer lastBuffer = mainForm.LastBuffer;
		if (lastBuffer == null || lastBuffer.Controller.isReadonly)
		{
			mainForm.Dialogs.ShowInfo("Error", "No file in current frame");
			return;
		}
		if (string.IsNullOrEmpty(raw))
		{
			mainForm.Dialogs.ShowInfo("Encoding", lastBuffer.encodingPair.ToString());
			return;
		}
		string error;
		EncodingPair pair = EncodingPair.ParseEncoding(raw, out error);
		if (pair.IsNull)
		{
			mainForm.Dialogs.ShowInfo("Encoding parsing error", "Error: " + error + "\n" + EncodingPair.GetEncodingsText());
			return;
		}
		lastBuffer.encodingPair = pair;
	}

	private void DoReloadInCustomEncoding(string raw)
	{
		Buffer lastBuffer = mainForm.LastBuffer;
		if (lastBuffer == null || lastBuffer.Controller.isReadonly || string.IsNullOrEmpty(lastBuffer.FullPath))
		{
			mainForm.Dialogs.ShowInfo("Error", "No file with path in current frame");
			return;
		}
		if (string.IsNullOrEmpty(raw))
		{
			mainForm.ReloadFile(lastBuffer);
			return;
		}
		if (raw == "reset")
		{
			tempSettings.ResetQualitiesEncoding(lastBuffer);
			lastBuffer.settedEncodingPair = new EncodingPair();
			mainForm.ReloadFile(lastBuffer);
			return;
		}
		string error;
		EncodingPair pair = EncodingPair.ParseEncoding(raw, out error);
		if (pair.IsNull)
		{
			mainForm.Dialogs.ShowInfo("Encoding parsing error", "Error: " + error + "\n" + EncodingPair.GetEncodingsText());
			return;
		}
		lastBuffer.settedEncodingPair = pair;
		mainForm.ReloadFile(lastBuffer);
	}
}
