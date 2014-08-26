using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using MulticaretEditor;
using MulticaretEditor.KeyMapping;
using MulticaretEditor.Highlighting;

public class RunShellCommand
{
	public class Position
	{
		public string fileName;
		public Place place;
		public int shellStart;
		public int shellLength;

		public Position(string fileName, Place place, int shellStart, int shellLength)
		{
			this.fileName = fileName;
			this.place = place;
			this.shellStart = shellStart;
			this.shellLength = shellLength;
		}
	}

	private MainForm mainForm;

	public RunShellCommand(MainForm mainForm)
	{
		this.mainForm = mainForm;
	}

	private Buffer buffer;
	private Dictionary<int, List<Position>> positions;

	public string Execute(string commandText, IRList<RegexData> regexList)
	{
		positions = new Dictionary<int, List<Position>>();

		Process p = new Process();
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.RedirectStandardOutput = true;
		p.StartInfo.FileName = "cmd.exe";
		p.StartInfo.Arguments = "/C " + commandText;
		p.Start();
		string output = p.StandardOutput.ReadToEnd();
		p.WaitForExit();

		buffer = new Buffer(null, "Shell command results");
		buffer.Controller.isReadonly = true;
		buffer.Controller.InitText(output);
		if (regexList != null)
		{
			List<StyleRange> ranges = new List<StyleRange>();
			foreach (RegexData regexData in regexList)
			{
				string currentDirectory = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
				foreach (Match match in regexData.regex.Matches(output))
				{
					if (match.Groups.Count >= 2)
					{
						string path = match.Groups[1].Value;
						int shellStart = match.Groups[1].Index;
						int shellLength = match.Groups[1].Length;
						ranges.Add(new StyleRange(shellStart, shellLength, Ds.String.index));
						int iLine = 1;
						if (match.Groups.Count >= 3)
						{
							try
							{
								iLine = int.Parse(match.Groups[2].Value);
							}
							catch
							{
							}
							int start = match.Groups[2].Index;
							int length = match.Groups[2].Length;
							ranges.Add(new StyleRange(start, length, Ds.DecVal.index));
							shellLength = Math.Max(shellStart + shellLength, start + length) - shellStart;
						}
						int iChar = 1;
						if (match.Groups.Count >= 4)
						{
							try
							{
								iChar = int.Parse(match.Groups[3].Value);
							}
							catch
							{
							}
							int start = match.Groups[3].Index;
							int length = match.Groups[3].Length;
							ranges.Add(new StyleRange(start, length, Ds.DecVal.index));
							shellLength = Math.Max(shellStart + shellLength, start + length) - shellStart;
						}
						Place place = buffer.Controller.Lines.PlaceOf(match.Index);
						List<Position> list;
						positions.TryGetValue(place.iLine, out list);
						if (list == null)
						{
							list = new List<Position>();
							positions[place.iLine] = list;
						}
						list.Add(new Position(path, new Place(iChar - 1, iLine - 1), shellStart, shellLength));
					}
				}
			}
			buffer.Controller.SetStyleRanges(ranges);
		}
		buffer.additionKeyMap = new KeyMap();
		{
			KeyAction action = new KeyAction("F&ind\\Navigate to position", ExecuteEnter, null, false);
			buffer.additionKeyMap.AddItem(new KeyItem(Keys.Enter, null, action));
			buffer.additionKeyMap.AddItem(new KeyItem(Keys.None, null, action).SetDoubleClick(true));
		}
		{
			KeyAction action = new KeyAction("F&ind\\Close execution", CloseBuffer, null, false);
			buffer.additionKeyMap.AddItem(new KeyItem(Keys.Escape, null, action));
		}
		mainForm.ShowBuffer(mainForm.ConsoleNest, buffer);
		if (mainForm.ConsoleNest.Frame != null)
			mainForm.ConsoleNest.Frame.Focus();
		return null;
	}

	public bool ExecuteEnter(Controller controller)
	{
		int caret = buffer.Controller.LastSelection.caret;
		Place place = buffer.Controller.Lines.PlaceOf(caret);
		List<Position> list;
		if (positions.TryGetValue(place.iLine, out list))
		{
			list.Sort(ComparePositions);
			Position position = list[0];
			for (int i = 0; i < list.Count; i++)
			{
				Position positionI = list[i];
				int index = caret - positionI.shellStart;
				if (index >= 0 && index < positionI.shellLength)
				{
					position = positionI;
					break;
				}
			}
			string fullPath = null;
			try
			{
				fullPath = Path.GetFullPath(position.fileName);
			}
			catch
			{
				mainForm.Dialogs.ShowInfo("Error", "Incorrect path: " + position.fileName);
				return true;
			}
			mainForm.NavigateTo(fullPath, position.place, position.place);
			return true;
		}
		return false;
	}

	private static int ComparePositions(Position position0, Position position1)
	{
		return position0.shellStart - position1.shellStart;
	}

	private bool CloseBuffer(Controller controller)
	{
		if (buffer != null && buffer.Frame != null)
			buffer.Frame.RemoveBuffer(buffer);
		return true;
	}
}
