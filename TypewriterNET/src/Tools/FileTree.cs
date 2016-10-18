using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using MulticaretEditor;
using MulticaretEditor.KeyMapping;
using MulticaretEditor.Highlighting;
using System.Threading;

public class FileTree
{
	public enum NodeType
	{
		None, Directory, File, Error
	}

	public class Node
	{
		public Node(NodeType type, string name, string fullPath)
		{
			this.type = type;
			this.name = name;
			this.fullPath = fullPath;
			hash = fullPath.GetHashCode();
		}

		public readonly NodeType type;
		public readonly string name;
		public readonly string fullPath;
		public readonly int hash;
		public readonly List<Node> childs = new List<Node>();
		public bool expanded = false;
		public int line = -1;
	}

	public struct SelectionData
	{
		public int hash0;
		public int iChar0;
		public int hash1;
		public int iChar1;

		public SelectionData(int hash0, int iChar0, int hash1, int iChar1)
		{
			this.hash0 = hash0;
			this.iChar0 = iChar0;
			this.hash1 = hash1;
			this.iChar1 = iChar1;
		}
	}

	private MainForm mainForm;
	private Dictionary<int, bool> expanded;
	private Dictionary<Selection, SelectionData> selectionDatas;

	private Buffer buffer;
	public Buffer Buffer { get { return buffer; } }

	public FileTree(MainForm mainForm)
	{
		this.mainForm = mainForm;
        
        ResetReload();
		expanded = new Dictionary<int, bool>();
		selectionDatas = new Dictionary<Selection, SelectionData>();
		buffer = new Buffer(null, "File tree", SettingsMode.FileTree);
		buffer.showEncoding = false;
		buffer.Controller.isReadonly = true;
		buffer.additionKeyMap = new KeyMap();
		{
			KeyAction action = new KeyAction("&View\\File tree\\Open item, no switch", DoOnEnterNoSwitch, null, false);
			buffer.additionKeyMap.AddItem(new KeyItem(Keys.Shift | Keys.Enter, null, action));
			buffer.additionKeyMap.AddItem(new KeyItem(Keys.None, Keys.Shift, action).SetDoubleClick(true));
		}
		{
			KeyAction action = new KeyAction("&View\\File tree\\Open item", DoOnEnter, null, false);
			buffer.additionKeyMap.AddItem(new KeyItem(Keys.Enter, null, action));
			buffer.additionKeyMap.AddItem(new KeyItem(Keys.None, null, action).SetDoubleClick(true));
		}
		{
			KeyAction action = new KeyAction("&View\\File tree\\Set current directory", DoOnSetCurrentDirectory, null, false);
			buffer.additionKeyMap.AddItem(new KeyItem(Keys.Alt | Keys.Enter, null, action));
			buffer.additionKeyMap.AddItem(new KeyItem(Keys.None, Keys.Alt, action).SetDoubleClick(true));
		}
		{
			KeyAction action = new KeyAction("&View\\File tree\\Close file tree", DoCloseBuffer, null, false);
			buffer.additionKeyMap.AddItem(new KeyItem(Keys.Escape, null, action));
		}
		buffer.additionBeforeKeyMap = new KeyMap();
		{
			KeyAction action = new KeyAction("&View\\File tree\\Remove item", DoRemoveItem, null, false);
			buffer.additionBeforeKeyMap.AddItem(new KeyItem(Keys.Delete, null, action));
		}
		{
			KeyAction action = new KeyAction("&View\\File tree\\Add item", DoAddItem, null, false);
			buffer.additionBeforeKeyMap.AddItem(new KeyItem(Keys.Control | Keys.N, null, action));
		}
		{
			KeyAction action = new KeyAction("&View\\File tree\\Add directory", DoAddDir, null, false);
			buffer.additionBeforeKeyMap.AddItem(new KeyItem(Keys.Control | Keys.Shift | Keys.N, null, action));
		}
		{
			KeyAction action = new KeyAction("&View\\File tree\\Move item", DoMoveItem, null, false);
			buffer.additionBeforeKeyMap.AddItem(new KeyItem(Keys.Control | Keys.M, null, action));
		}
		{
			KeyAction action = new KeyAction("&View\\File tree\\Rename item", DoRenameItem, null, false);
			buffer.additionBeforeKeyMap.AddItem(new KeyItem(Keys.Control | Keys.R, null, action));
		}
		buffer.onSelected = OnBufferSelected;
		buffer.onUpdateSettings = OnBufferUpdateSettings;
	}

	private void OnBufferSelected(Buffer buffer)
	{
		Reload();
	}

	private void OnBufferUpdateSettings(Buffer buffer, UpdatePhase phase)
	{
		if (phase == UpdatePhase.ChangeCurrentDirectory)
			Reload();
	}

	private bool DoOnEnterNoSwitch(Controller controller)
	{
		return ProcessEnter(controller, true);
	}

	private bool DoOnEnter(Controller controller)
	{
		return ProcessEnter(controller, false);
	}

	private bool DoOnSetCurrentDirectory(Controller controller)
	{
		if (nodes.Count == 0)
			return false;
		Place place = controller.Lines.PlaceOf(controller.LastSelection.anchor);
		Node node = nodes[place.iLine];
		if (node.type != NodeType.Directory)
			return false;
		SetCurrentDirectory(node);
		return true;
	}

	private void SetCurrentDirectory(Node node)
	{
		SetCurrentDirectory(node.fullPath);
	}

	private void SetCurrentDirectory(string path)
	{
		string error;
		if (!mainForm.SetCurrentDirectory(path, out error))
			mainForm.Dialogs.ShowInfo("Error", error);
	}

	private List<Node> nodes = new List<Node>();

	private bool ProcessEnter(Controller controller, bool noSwitch)
	{
		if (nodes.Count == 0)
			return false;
		Dictionary<int, bool> selections = new Dictionary<int, bool>();
		foreach (Selection selection in controller.Selections)
		{
			Place place0 = controller.Lines.PlaceOf(selection.anchor);
			Place place1 = controller.Lines.PlaceOf(selection.caret);
			selectionDatas[selection] = new SelectionData(
				nodes[place0.iLine].hash, place0.iChar,
				nodes[place1.iLine].hash, place1.iChar);
			int i0 = Math.Min(place0.iLine, place1.iLine);
			int i1 = Math.Max(place0.iLine, place1.iLine);
			for (int i = i0; i <= i1; i++)
			{
				selections[i] = selection == controller.LastSelection && i == place1.iLine;
			}
		}
		bool needRebuild = false;
		Node mainFileNode = null;
		bool fileOpened = false;
		foreach (Node nodeI in nodes)
		{
			if (selections.ContainsKey(nodeI.line))
			{
				if (nodeI.type == NodeType.Directory)
				{
					if (nodeI.fullPath == "..")
					{
						SetCurrentDirectory(nodeI);
						return true;
					}
					if (nodeI.expanded)
						Collapse(nodeI);
					else
						Expand(nodeI);
					needRebuild = true;
				}
				else if (nodeI.type == NodeType.File)
				{
					fileOpened = true;
					if (selections[nodeI.line])
						mainFileNode = nodeI;
					else
						mainForm.LoadFile(nodeI.fullPath);
				}
			}
		}
		if (mainFileNode != null)
			mainForm.LoadFile(mainFileNode.fullPath);
		if (needRebuild)
		{
			Rebuild();
			List<Selection> selectionsToRemove = new List<Selection>();
			Dictionary<int, Node> nodeOf = new Dictionary<int, Node>();
			foreach (Node nodeI in nodes)
			{
				nodeOf[nodeI.hash] = nodeI;
			}
			foreach (Selection selection in controller.Selections)
			{
				SelectionData selectionData;
				if (!selectionDatas.TryGetValue(selection, out selectionData))
				{
					selectionsToRemove.Add(selection);
					continue;
				}
				Node node0;
				nodeOf.TryGetValue(selectionData.hash0, out node0);
				Node node1;
				nodeOf.TryGetValue(selectionData.hash1, out node1);
				if (node0 == null && node1 == null)
				{
					selectionsToRemove.Add(selection);
					continue;
				}
				if (node0 != null && node1 == null)
				{
					selection.anchor = selection.caret = controller.Lines.IndexOf(new Place(node0.line, selectionData.iChar0));
				}
				else if (node0 == null && node1 != null)
				{
					selection.anchor = selection.caret = controller.Lines.IndexOf(new Place(node1.line, selectionData.iChar1));
				}
				else
				{
					selection.anchor = controller.Lines.IndexOf(new Place(selectionData.iChar0, node0.line));
					selection.caret = controller.Lines.IndexOf(new Place(selectionData.iChar1, node1.line));
				}
			}
		}
		if (!noSwitch && fileOpened && mainForm.MainNest.Frame != null)
			mainForm.MainNest.Frame.Focus();
		return true;
	}

	private Node node;
	private string currentDirectory;

	public void Reload()
	{
	    wasReloaded = true;
	    ResetReload();
		currentDirectory = Directory.GetCurrentDirectory();
		string root = currentDirectory;
		node = new Node(NodeType.Directory, Path.GetFileName(root), Path.GetFullPath(root));
		Expand(node);
		Rebuild();
		if (!expandedTemp.Equals(SValue.None))
		{
			SetExpandedTemp(expandedTemp);
			expandedTemp = SValue.None;
		}
	}

	public void Find(string fullPath)
	{
		fullPath = fullPath.ToLowerInvariant();
		if (!fullPath.StartsWith(currentDirectory.ToLowerInvariant()))
		{
			string dir = Path.GetDirectoryName(fullPath);
			SetCurrentDirectory(dir);
		}
		ExpandTo(node, fullPath);
		Rebuild();
		for (int i = 0, count = nodes.Count; i < count; i++)
		{
			Node nodeI = nodes[i];
			if (nodeI.fullPath.ToLowerInvariant() == fullPath)
			{
				buffer.Controller.ClearMinorSelections();
				Place place = new Place(0, i);
				buffer.Controller.LastSelection.anchor = buffer.Controller.LastSelection.caret = buffer.Controller.Lines.IndexOf(place);
				buffer.Controller.NeedScrollToCaret();
				break;
			}
		}
	}

	private void ExpandTo(Node node, string fullPath)
	{
		if (!fullPath.StartsWith(node.fullPath.ToLowerInvariant()))
			return;
		if (node.fullPath.ToLowerInvariant() == fullPath)
			return;
		if (!node.expanded)
		{
			Expand(node);
		}
		foreach (Node nodeI in node.childs)
		{
			string nodeFullPath = !nodeI.fullPath.EndsWith("\\") ? nodeI.fullPath + "\\" : nodeI.fullPath;
			if (fullPath.StartsWith(nodeFullPath.ToLowerInvariant()))
			{
				ExpandTo(nodeI, fullPath);
				break;
			}
		}
	}

	private void Expand(Node node)
	{
		if (node.type == NodeType.Directory)
		{
			node.expanded = true;
			node.childs.Clear();
			List<string> directories = null;
			List<string> files = null;
			try
			{
				string[] rawDirectories = Directory.GetDirectories(node.fullPath);
				string[] rawFiles = Directory.GetFiles(node.fullPath);
				directories = new List<string>();
				files = new List<string>();
				FileNameFilter filter = !string.IsNullOrEmpty(hideInFileTree) ?
				    new FileNameFilter(hideInFileTree) : null;
				foreach (string directory in rawDirectories)
				{
				    if (filter == null || !filter.Match(Path.GetFileName(directory)))
				        directories.Add(directory);
				}
				foreach (string file in rawFiles)
				{
				    if (filter == null || !filter.Match(Path.GetFileName(file)))
				        files.Add(file);
				}
			}
			catch (Exception e)
			{
				node.childs.Add(new Node(NodeType.Error, CommonHelper.GetOneLine(e.Message), node.fullPath + "#error"));
				return;
			}
			foreach (string file in directories)
			{
				Node nodeI = new Node(NodeType.Directory, Path.GetFileName(file), Path.GetFullPath(file));
				if (expanded.ContainsKey(nodeI.hash))
					Expand(nodeI);
				node.childs.Add(nodeI);
			}
			foreach (string file in files)
			{
				Node nodeI = new Node(NodeType.File, Path.GetFileName(file), Path.GetFullPath(file));
				if (expanded.ContainsKey(nodeI.hash))
					Expand(nodeI);
				node.childs.Add(nodeI);
			}
		}
	}

	private void Collapse(Node node)
	{
		if (node.type == NodeType.Directory)
		{
			node.expanded = false;
			node.childs.Clear();
		}
	}

	private void Rebuild()
	{
		nodes.Clear();
		StringBuilder builder = new StringBuilder();
		bool first = true;
		List<StyleRange> ranges = new List<StyleRange>();
		Rebuild(node, builder, "", ref first, ranges);
		buffer.Controller.InitText(builder.ToString());
		buffer.Controller.SetStyleRanges(ranges);

		int charsCount = buffer.Controller.Lines.charsCount;
		foreach (Selection selection in buffer.Controller.Selections)
		{
			if (selection.anchor > charsCount)
				selection.anchor = charsCount;
			if (selection.caret > charsCount)
				selection.caret = charsCount;
		}
		buffer.Controller.JoinSelections();
	}

	private void Rebuild(Node node, StringBuilder builder, string indent, ref bool first, List<StyleRange> ranges)
	{
		if (!first)
			builder.AppendLine();
		if (node.type == NodeType.Directory)
		{
			if (node.expanded)
				expanded[node.hash] = true;
			else
				expanded.Remove(node.hash);
		}
		string prefix = "";
		if (!first)
		{
			prefix = "  ";
			if (node.type == NodeType.Directory)
				prefix = node.expanded ? "- " : "+ ";
			builder.Append(indent + prefix);
			if (node.type == NodeType.Directory)
			{
				ranges.Add(new StyleRange(builder.Length, node.name.Length, Ds.Keyword.index));
			}
			else if (node.type == NodeType.File)
			{
				string extension = Path.GetExtension(node.name).ToLowerInvariant();
				if (extension == ".exe" || extension == ".bat" || extension == ".cmd")
					ranges.Add(new StyleRange(builder.Length, node.name.Length, Ds.DataType.index));
				else if (extension == ".dll")
					ranges.Add(new StyleRange(builder.Length, node.name.Length, Ds.Function.index));
				else if (extension == ".txt" || extension == ".md" || extension == ".xml" || extension == ".ini")
					ranges.Add(new StyleRange(builder.Length, node.name.Length, Ds.String.index));
				else if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".gif")
					ranges.Add(new StyleRange(builder.Length, node.name.Length, Ds.Others.index));
				else if (node.name.StartsWith("."))
					ranges.Add(new StyleRange(builder.Length, node.name.Length, Ds.Comment.index));
			}
			else if (node.type == NodeType.Error)
			{
				ranges.Add(new StyleRange(builder.Length, node.name.Length, Ds.Error.index));
			}
			builder.Append(node.name);
			node.line = nodes.Count;
			nodes.Add(node);
		}
		else
		{
			Node upNode = new Node(NodeType.Directory, "..", "..");
			upNode.line = nodes.Count;
			nodes.Add(upNode);
			builder.Append(upNode.name);

			builder.AppendLine();

			Node currentDirectoryNode = new Node(NodeType.Directory, node.name, "");
			currentDirectoryNode.line = nodes.Count;
			nodes.Add(currentDirectoryNode);
			ranges.Add(new StyleRange(builder.Length, currentDirectory.Length, Ds.Constructor.index));
			builder.Append(currentDirectory);
		}
		if (!first)
			indent += "  ";
		first = false;
		foreach (Node child in node.childs)
		{
			Rebuild(child, builder, indent, ref first, ranges);
		}
	}

	private bool DoCloseBuffer(Controller controller)
	{
		if (buffer != null && buffer.Frame != null)
			buffer.Frame.RemoveBuffer(buffer);
		return true;
	}

	public SValue GetExpandedTemp()
	{
		SValue value = SValue.NewList();
		foreach (Node nodeI in nodes)
		{
			if (nodeI.expanded)
			{
				value.Add(SValue.NewInt(nodeI.hash));
			}
		}
		return value;
	}

	private SValue expandedTemp = SValue.None;

	public void SetExpandedTemp(SValue value)
	{
		if (node == null)
		{
			expandedTemp = value;
			return;
		}
		Dictionary<int, bool> expanded = new Dictionary<int, bool>();
		foreach (SValue valueI in value.List)
		{
			expanded[valueI.Int] = true;
		}
		ExpandCollection(node, expanded);
		Rebuild();
	}

	private void ExpandCollection(Node node, Dictionary<int, bool> expanded)
	{
		if ((!node.expanded) && expanded.ContainsKey(node.hash))
			Expand(node);
		foreach (Node nodeI in node.childs)
		{
			ExpandCollection(nodeI, expanded);
		}
	}

	private List<int> GetSelectionIndices(Controller controller)
	{
		Dictionary<int, bool> indexHash = new Dictionary<int, bool>();
		foreach (Selection selection in controller.Selections)
		{
			Place place0 = controller.Lines.PlaceOf(selection.anchor);
			Place place1 = controller.Lines.PlaceOf(selection.caret);
			int i0 = Math.Min(place0.iLine, place1.iLine);
			int i1 = Math.Max(place0.iLine, place1.iLine);
			for (int i = i0; i <= i1; i++)
			{
				indexHash[i] = true;
			}
		}
		List<int> indices = new List<int>();
		foreach (KeyValuePair<int, bool> pair in indexHash)
		{
			indices.Add(pair.Key);
		}
		indices.Sort();
		return indices;
	}

	private List<Node> GetFilesAndDirs(Controller controller)
	{
		List<int> indices = GetSelectionIndices(controller);
		Dictionary<Node, bool> nodesHash = new Dictionary<Node, bool>();
		List<Node> nodesToRemove = new List<Node>();
		foreach (int index in indices)
		{
			nodesHash[nodes[index]] = true;
			nodesToRemove.Add(nodes[index]);
		}
		foreach (Node nodeI in nodesToRemove)
		{
			foreach (Node nodeJ in nodeI.childs)
			{
				nodesHash.Remove(nodeJ);
			}
		}
		List<Node> result = new List<Node>();
		foreach (KeyValuePair<Node, bool> pair in nodesHash)
		{
			result.Add(pair.Key);
		}
		return result;
	}

	private bool DoRemoveItem(Controller controller)
	{
		List<Node> filesAndDirs = GetFilesAndDirs(controller);
		int count = 0;
		StringBuilder builder = new StringBuilder();
		builder.AppendLine("Remove " + (filesAndDirs.Count > 1 ? "items" : "item") + "?");
		foreach (Node nodeI in filesAndDirs)
		{
			count++;
			if (count > 10)
			{
				builder.AppendLine("...");
				break;
			}
			builder.AppendLine(nodeI.fullPath);
		}

		DialogResult result = MessageBox.Show(builder.ToString(), mainForm.Name, MessageBoxButtons.YesNo);
		if (result == DialogResult.Yes)
		{
			foreach (Node nodeI in filesAndDirs)
			{
				try
				{
					if (nodeI.type == NodeType.Directory)
						Directory.Delete(nodeI.fullPath, true);
					else if (nodeI.type == NodeType.File)
						File.Delete(nodeI.fullPath);
				}
				catch (Exception e)
				{
					mainForm.Log.WriteError("Remove error", e.Message);
					mainForm.Log.Open();
				}
			}
			Reload();
		}
		return true;
	}

	private bool DoAddItem(Controller controller)
	{
		mainForm.Dialogs.OpenInput("Add item", "unnamed.txt", DoInputItemName);
		return true;
	}

	private bool DoInputItemName(string fileName)
	{
		if (string.IsNullOrEmpty(fileName))
			return true;
		mainForm.Dialogs.CloseInput();
		List<int> indices = GetSelectionIndices(this.buffer.Controller);
		Dictionary<string, bool> dirsSet = new Dictionary<string, bool>();
		List<string> fullPaths = new List<string>();
		Dictionary<string, bool> fullPathsSet = new Dictionary<string, bool>();
		List<Node> unexpandedNodes = new List<Node>();
		foreach (int index in indices)
		{
			Node nodeI = nodes[index];
			string dir = null;
			if (nodeI.type == NodeType.Directory)
			{
				dir = nodeI.fullPath;
				if (!nodeI.expanded)
					unexpandedNodes.Add(nodeI);
			}
			else if (nodeI.type == NodeType.File)
			{
				dir = Path.GetDirectoryName(nodeI.fullPath);
			}
			if (dir != null && !dirsSet.ContainsKey(dir))
			{
				string fullPath = Path.Combine(dir, fileName);
				dirsSet[dir] = true;
				fullPaths.Add(fullPath);
				fullPathsSet[NormalizedWithoutSlash(fullPath)] = true;
			}
		}
		foreach (Node node in unexpandedNodes)
		{
			Expand(node);
		}
		Rebuild();
		foreach (string fullPath in fullPaths)
		{
			Buffer buffer = mainForm.ForcedLoadFile(fullPath);
			buffer.needSaveAs = false;
			mainForm.SaveFile(buffer);
		}
		Reload();
		this.buffer.Controller.ClearMinorSelections();
		bool first = true;
		for (int i = 0, count = nodes.Count; i < count; i++)
		{
			Node nodeI = nodes[i];
			if (fullPathsSet.ContainsKey(NormalizedWithoutSlash(nodeI.fullPath)))
			{
				Place place = new Place(0, i);
				if (first)
				{
					first = false;
					this.buffer.Controller.PutCursor(place, false);
				}
				else
				{
					this.buffer.Controller.PutNewCursor(place);
				}
			}
		}
		this.buffer.Controller.NeedScrollToCaret();
		return true;
	}
	
	private static string NormalizedWithoutSlash(string fullPath)
	{
		if (fullPath == null)
			return null;
		if (fullPath.EndsWith("\\"))
			fullPath = fullPath.Substring(0, fullPath.Length);
		return fullPath.ToLowerInvariant();
	}

	private bool DoAddDir(Controller controller)
	{
		mainForm.Dialogs.OpenInput("Add directory", "unnamed", DoInputDirName);
		return true;
	}

	private bool DoInputDirName(string fileName)
	{
		if (string.IsNullOrEmpty(fileName))
			return true;
		mainForm.Dialogs.CloseInput();
		List<int> indices = GetSelectionIndices(this.buffer.Controller);
		Dictionary<string, bool> dirsSet = new Dictionary<string, bool>();
		List<string> fullPaths = new List<string>();
		Dictionary<string, bool> fullPathsSet = new Dictionary<string, bool>();
		List<Node> unexpandedNodes = new List<Node>();
		foreach (int index in indices)
		{
			Node nodeI = nodes[index];
			string dir = null;
			if (nodeI.type == NodeType.Directory)
			{
				dir = nodeI.fullPath;
				if (!nodeI.expanded)
					unexpandedNodes.Add(nodeI);
			}
			else if (nodeI.type == NodeType.File)
			{
				dir = Path.GetDirectoryName(nodeI.fullPath);
			}
			if (dir != null && !dirsSet.ContainsKey(dir))
			{
				string fullPath = Path.Combine(dir, fileName);
				dirsSet[dir] = true;
				fullPaths.Add(fullPath);
				fullPathsSet[NormalizedWithoutSlash(fullPath)] = true;
			}
		}
		foreach (Node node in unexpandedNodes)
		{
			Expand(node);
		}
		Rebuild();
		foreach (string fullPath in fullPaths)
		{
			try
			{
				Directory.CreateDirectory(fullPath);
			}
			catch (Exception e)
			{
				mainForm.Log.WriteError("Add directory error", e.Message);
				mainForm.Log.Open();
			}
		}
		Reload();
		this.buffer.Controller.ClearMinorSelections();
		bool first = true;
		for (int i = 0, count = nodes.Count; i < count; i++)
		{
			Node nodeI = nodes[i];
			if (fullPathsSet.ContainsKey(NormalizedWithoutSlash(nodeI.fullPath)))
			{
				Place place = new Place(0, i);
				if (first)
				{
					first = false;
					this.buffer.Controller.PutCursor(place, false);
				}
				else
				{
					this.buffer.Controller.PutNewCursor(place);
				}
			}
		}
		this.buffer.Controller.NeedScrollToCaret();
		return true;
	}

	private string GetRelativePath(string fullPath)
	{
		return fullPath.StartsWith(currentDirectory) && fullPath.Length >= currentDirectory.Length + 1 ?
			fullPath.Substring(currentDirectory.Length + 1) : fullPath;
	}

	private bool DoMoveItem(Controller controller)
	{
		Place place = controller.Lines.PlaceOf(controller.LastSelection.caret);
		int index = place.iLine;
		Node node = nodes[index];
		string path = Path.GetDirectoryName(GetRelativePath(node.fullPath));
		mainForm.Dialogs.OpenInput("Move item", path, DoInputNewDir);
		return true;
	}

	private bool DoInputNewDir(string fileName)
	{
		mainForm.Dialogs.CloseInput();
		List<Node> filesAndDirs = GetFilesAndDirs(buffer.Controller);
		foreach (Node nodeI in filesAndDirs)
		{
			try
			{
				if (nodeI.type == NodeType.Directory)
				{
					DirectoryMove(nodeI.fullPath, Path.Combine(fileName, Path.GetFileName(nodeI.fullPath)));
				}
				else if (nodeI.type == NodeType.File)
				{
					FileMove(nodeI.fullPath, Path.Combine(fileName, Path.GetFileName(nodeI.fullPath)));
				}
				if (!string.IsNullOrEmpty(renamePostfixed))
				{
				    if (File.Exists(nodeI.fullPath + renamePostfixed))
				    {
				        FileMove(nodeI.fullPath + renamePostfixed, Path.Combine(fileName, Path.GetFileName(nodeI.fullPath)) + renamePostfixed);
				    }
				    else if (Directory.Exists(nodeI.fullPath + renamePostfixed))
				    {
				        DirectoryMove(nodeI.fullPath + renamePostfixed, Path.Combine(fileName, Path.GetFileName(nodeI.fullPath)) + renamePostfixed);
				    }
				}
			}
			catch (IOException e)
			{
				mainForm.Log.WriteError("Move error", e.Message);
				mainForm.Log.Open();
			}
		}
		Reload();
		return true;
	}
	
	private bool DoRenameItem(Controller controller)
	{
		Place place = controller.Lines.PlaceOf(controller.LastSelection.caret);
		int index = place.iLine;
		Node node = nodes[index];
		string fileName = Path.GetFileName(node.fullPath);
		mainForm.Dialogs.OpenInput("Rename item", fileName, DoInputNewFileName);
		return true;
	}
	
	private void FileMove(string oldFile, string newFile)
	{
		File.Move(oldFile, newFile);
		Buffer buffer = mainForm.GetBuffer(oldFile);
		if (buffer != null)
			buffer.SetFile(newFile, Path.GetFileName(newFile));
	}
	
	private void DirectoryMove(string oldDir, string newDir)
	{
		Directory.Move(oldDir, newDir);
		if (oldDir.EndsWith("/"))
			oldDir = oldDir.Substring(0, oldDir.Length - 1) + "\\";
		if (!oldDir.EndsWith("\\"))
			oldDir += "\\";
		if (newDir.EndsWith("/"))
			newDir = newDir.Substring(0, newDir.Length - 1) + "\\";
		if (!newDir.EndsWith("\\"))
			newDir += "\\";
		foreach (Buffer buffer in mainForm.GetFileBuffers())
		{
			if (!string.IsNullOrEmpty(buffer.FullPath) && buffer.FullPath.StartsWith(oldDir))
			{
				string newFile = newDir + buffer.FullPath.Substring(oldDir.Length);
				buffer.SetFile(newFile, Path.GetFileName(newFile));
			}
		}
	}
	
	private bool DoInputNewFileName(string fileName)
	{
		if (string.IsNullOrEmpty(fileName))
			return true;
		mainForm.Dialogs.CloseInput();
		List<Node> filesAndDirs = GetFilesAndDirs(buffer.Controller);
		foreach (Node nodeI in filesAndDirs)
		{
			try
			{
				if (nodeI.type == NodeType.Directory)
				{
					DirectoryMove(nodeI.fullPath, Path.Combine(Path.GetDirectoryName(nodeI.fullPath), fileName));
				}
				else if (nodeI.type == NodeType.File)
				{
					FileMove(nodeI.fullPath, Path.Combine(Path.GetDirectoryName(nodeI.fullPath), fileName));
				}
				if (!string.IsNullOrEmpty(renamePostfixed))
				{
				    if (File.Exists(nodeI.fullPath + renamePostfixed))
				    {
				        File.Move(nodeI.fullPath + renamePostfixed, Path.Combine(Path.GetDirectoryName(nodeI.fullPath), fileName + renamePostfixed));
				    }
				    else if (Directory.Exists(nodeI.fullPath + renamePostfixed))
				    {
				        DirectoryMove(nodeI.fullPath + renamePostfixed, Path.Combine(Path.GetDirectoryName(nodeI.fullPath), fileName + renamePostfixed));
				    }
				}
			}
			catch (IOException e)
			{
				mainForm.Log.WriteError("Rename error", e.Message);
				mainForm.Log.Open();
			}
		}
		mainForm.UpdateAfterFileRenamed();
		Reload();
		return true;
	}
	
	private bool wasReloaded;
	private string renamePostfixed;
	private string hideInFileTree;
	
	private void ResetReload()
	{
	    renamePostfixed = mainForm.Settings.renamePostfixed.Value + "";
	    hideInFileTree = mainForm.Settings.hideInFileTree.Value + "";
	}
	
	public void ReloadIfNeedForSettings()
	{
	    if (wasReloaded && (
	        renamePostfixed != mainForm.Settings.renamePostfixed.Value + "" ||
	        hideInFileTree != mainForm.Settings.hideInFileTree.Value + ""
	    ))
        {
            renamePostfixed = mainForm.Settings.renamePostfixed.Value + "";
	        hideInFileTree = mainForm.Settings.hideInFileTree.Value + "";
            Reload();
        }
	}
}
