using System;
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
using System.Net;
using MulticaretEditor;
using MulticaretEditor.Highlighting;
using MulticaretEditor.KeyMapping;

public class MainForm : Form
{
	private const string UntitledTxt = "Untitled.txt";
	private readonly string[] args;

	private readonly Settings settings;
	public Settings Settings { get { return settings; } }

	private readonly ConfigParser configParser;

	private readonly MainFormMenu menu;
	private readonly Timer validationTimer;

	public readonly FrameList frames;
	public readonly Commander commander;

	private ConcreteHighlighterSet highlightingSet;
	private SyntaxFilesScanner syntaxFilesScanner;

	public MainForm(string[] args)
	{
		this.args = args;

		MulticaretTextBox.initMacrosExecutor = new MacrosExecutor(GetFocusedTextBox);

		frames = new FrameList(this);

		ResourceManager manager = new ResourceManager("TypewriterNET", typeof(Program).Assembly);
		Icon = (Icon)manager.GetObject("icon");
		Name = Application.ProductName;
		Text = Name;

		menu = new MainFormMenu(this);
		Menu = menu;

		settings = new Settings(ApplySettings);
		configParser = new ConfigParser(settings);
		commander = new Commander();

		Load += OnLoad;

		validationTimer = new Timer();
		validationTimer.Interval = 20;
		validationTimer.Tick += OnValidationTimerTick;
		validationTimer.Start();
	}

	public void UpdateTitle()
	{
		Buffer buffer = LastBuffer;
		string name = buffer != null ? buffer.FullPath : null;
		Text = Application.ProductName + (string.IsNullOrEmpty(name) ? "" : " - " + name);
	}

	public Buffer LastBuffer
	{
		get { return lastFrame != null && lastFrame.Nest != null ? lastFrame.SelectedBuffer : null; }
	}

	private void OnValidationTimerTick(object sender, EventArgs e)
	{
		if (frames.NeedResize)
		{
			frames.NeedResize = false;
			DoResize();
		}
	}

	private Nest mainNest;
	public Nest MainNest { get { return mainNest; } }

	private Nest consoleNest;
	public Nest ConsoleNest { get { return consoleNest; } }

	private Nest leftNest;
	public Nest LeftNest { get { return leftNest; } }

	private FileDragger fileDragger;
	private TempSettings tempSettings;
	private SchemeManager schemeManager;

	private XmlLoader xmlLoader;
	public XmlLoader XmlLoader { get { return xmlLoader; } }

	private DialogManager dialogs;
	public DialogManager Dialogs { get { return dialogs; } }

	private Log log;
	public Log Log { get { return log; } }

	private FileTree fileTree;

	private void OnLoad(object sender, EventArgs e)
	{
		string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TypewriterNET");
		if (!Directory.Exists(appDataPath))
			Directory.CreateDirectory(appDataPath);
		AppPath.Init(Application.StartupPath, appDataPath);

		BuildMenu();

		tempSettings = new TempSettings(this, settings);
		commander.Init(this, settings, tempSettings);
		dialogs = new DialogManager(this, tempSettings);

		mainNest = AddNest(false, true, true, tempSettings.GetInt("mainNest.size", 70));
		mainNest.buffers = new BufferList();
		new Frame().Create(mainNest);

		consoleNest = AddNest(false, false, true, tempSettings.GetInt("consoleNest.size", 20));
		consoleNest.buffers = new BufferList();
		leftNest = AddNest(true, true, false, tempSettings.GetInt("leftNest.size", 120));

		log = new Log(this, consoleNest);
		xmlLoader = new XmlLoader(this);

		schemeManager = new SchemeManager(xmlLoader);
		syntaxFilesScanner = new SyntaxFilesScanner(new string[] {
			Path.Combine(AppPath.AppDataDir, AppPath.Syntax),
			Path.Combine(AppPath.StartupDir, AppPath.Syntax) });
		highlightingSet = new ConcreteHighlighterSet(xmlLoader, log);

		syntaxFilesScanner.Rescan();
		highlightingSet.UpdateParameters(syntaxFilesScanner);
		frames.UpdateSettings(settings, UpdatePhase.HighlighterChange);

		fileTree = new FileTree(this);

		leftNest.buffers = new BufferList();

		SetFocus(null, new KeyMapNode(keyMap, 0), null);

		ApplySettings();
		ReloadConfig();
		fileDragger = new FileDragger(this);

		tempSettings.Load();
		frames.UpdateSettings(settings, UpdatePhase.TempSettingsLoaded);

		if (args.Length == 1)
			LoadFile(args[0]);
		if (args.Length == 3 && args[0] == "-connect")
			LoadFile(args[1], args[2]);
		FormClosing += OnFormClosing;
		mainNest.buffers.AllRemoved += OpenEmptyIfNeed;
		OpenEmptyIfNeed();

		UpdateTitle();

		Activated += OnActivated;
	}

	public bool SetCurrentDirectory(string path, out string error)
	{
		error = null;
		if (string.IsNullOrEmpty(path))
			return false;
		if (path.ToLowerInvariant() == Directory.GetCurrentDirectory().ToLowerInvariant())
			return false;
		try
		{
			Directory.SetCurrentDirectory(path);
		}
		catch (Exception e)
		{
			error = e.Message;
			return false;
		}
		frames.UpdateSettings(settings, UpdatePhase.ChangeCurrentDirectory);
		if (hasCurrentConfig || File.Exists(AppPath.ConfigPath.GetCurrentPath()))
			ReloadConfig();
		return true;
	}

	private bool activationInProcess = false;

	private void OnActivated(object sender, EventArgs e)
	{
		if (activationInProcess)
			return;
		activationInProcess = true;

		foreach (Buffer buffer in frames.GetBuffers(BufferTag.File))
		{
			CheckFileChange(buffer);
		}

		activationInProcess = false;
	}

	private void CheckFileChange(Buffer buffer)
	{
		if (buffer.fileInfo != null)
		{
			buffer.fileInfo.Refresh();
			if (buffer.lastWriteTimeUtc != buffer.fileInfo.LastWriteTimeUtc)
			{
				DialogResult result = MessageBox.Show("File was changed. Reload it?", Name, MessageBoxButtons.YesNo);
				if (result == DialogResult.Yes)
					ReloadFile(buffer);
			}
		}
	}

	private void OpenEmptyIfNeed()
	{
		if (mainNest.Frame == null)
			new Frame().Create(mainNest);
		if (mainNest.buffers.list.Count == 0)
		{
			Buffer buffer = NewFileBuffer();
			mainNest.Frame.AddBuffer(buffer);
		}
	}

	private void RemoveEmptyIfNeed()
	{
		Buffer buffer = null;
		for (int i = mainNest.buffers.list.Count; i-- > 0;)
		{
			Buffer bufferI = mainNest.buffers.list[i];
			if ((bufferI.tags & BufferTag.File) != 0 && bufferI.IsEmpty && !bufferI.HasHistory &&
				bufferI.Name == UntitledTxt)
			{
				buffer = bufferI;
				break;
			}
		}
		if (buffer != null)
			mainNest.buffers.list.Remove(buffer);
	}

	private bool forbidTempSaving = false;

	private void OnFormClosing(object sender, FormClosingEventArgs e)
	{
		foreach (Buffer buffer in frames.GetBuffers(BufferTag.File))
		{
			if (buffer.onRemove != null && !buffer.onRemove(buffer))
			{
				e.Cancel = true;
				break;
			}
		}
		if (!forbidTempSaving)
			tempSettings.Save();
	}

	public KeyMapNode MenuNode { get { return menu.node; } }

	private MulticaretTextBox focusedTextBox;
	public Controller FocusedController { get { return focusedTextBox != null ? focusedTextBox.Controller : null; } }

	private MulticaretTextBox GetFocusedTextBox()
	{
		return focusedTextBox;
	}

	private Frame lastFrame;
	public Frame LastFrame { get { return lastFrame; } }

	public void SetFocus(MulticaretTextBox textBox, KeyMapNode node, Frame frame)
	{
		focusedTextBox = textBox;
		menu.node = node;
		if (frame != null)
			lastFrame = frame;
		UpdateTitle();
	}

	private void ApplySettings()
	{
		settings.ParsedScheme = schemeManager.LoadScheme(settings.scheme.Value);
		settings.Parsed = true;
		BackColor = settings.ParsedScheme.bgColor;
		TopMost = settings.alwaysOnTop.Value;
		frames.UpdateSettings(settings, UpdatePhase.Raw);
		frames.UpdateSettings(settings, UpdatePhase.Parsed);
	}

	public void DoResize()
	{
		frames.Resize(0, 0, ClientSize);
	}

	private Nest AddNest(bool hDivided, bool left, bool isPercents, TempSettingsInt settingsInt)
	{
		Nest nest = frames.AddParentNode();
		nest.hDivided = hDivided;
		nest.left = left;
		nest.isPercents = isPercents;
		nest.size = settingsInt.value;
		nest.settingsSize = settingsInt;
		return nest;
	}

	override protected void OnResize(EventArgs e)
	{
		base.OnResize(e);
		DoResize();
	}

	private KeyMap keyMap;
	public KeyMap KeyMap { get { return keyMap; } }

	private KeyMap doNothingKeyMap;
	public KeyMap DoNothingKeyMap { get { return doNothingKeyMap; } }

	private void BuildMenu()
	{
		keyMap = new KeyMap();
		doNothingKeyMap = new KeyMap();

		doNothingKeyMap.AddItem(new KeyItem(Keys.Escape, null, KeyAction.Nothing));
		doNothingKeyMap.AddItem(new KeyItem(Keys.Escape | Keys.Shift, null, KeyAction.Nothing));

		keyMap.AddItem(new KeyItem(Keys.Control | Keys.N, null, new KeyAction("&File\\New", DoNew, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.O, null, new KeyAction("&File\\Open", DoOpen, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.S, null, new KeyAction("&File\\Save", DoSave, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.Shift | Keys.S, null, new KeyAction("&File\\Save As", DoSaveAs, null, false)));
		keyMap.AddItem(new KeyItem(Keys.None, null, new KeyAction("&File\\-", null, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Alt | Keys.F4, null, new KeyAction("&File\\Exit", DoExit, null, false)));

		keyMap.AddItem(new KeyItem(Keys.Control | Keys.D1, null, new KeyAction("&View\\Open/close log", DoOpenCloseLog, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.D2, null, new KeyAction("&View\\Open/close find results", DoOpenCloseFindResults, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.D3, null, new KeyAction("&View\\Open/close shell command results", DoOpenCloseShellResults, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.Oemtilde, null, new KeyAction("&View\\Open/close console panel", DoOpenCloseConsolePanel, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Escape, null, new KeyAction("&View\\Close console panel", DoCloseConsolePanel, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.E, null, new KeyAction("&View\\Change focus", DoChangeFocus, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.I, null, new KeyAction("&View\\File tree\\Open/close file tree", DoOpenCloseFileTree, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.D0, null, new KeyAction("&View\\File tree\\Find file in tree", DoFindFileInTree, null, false)));

		keyMap.AddItem(new KeyItem(Keys.Control | Keys.F2, null, new KeyAction("Prefere&nces\\Edit/create current config", DoEditCreateCurrentConfig, null, false)));
		keyMap.AddItem(new KeyItem(Keys.F2, null, new KeyAction("Prefere&nces\\Edit config", DoOpenUserConfig, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Shift | Keys.F2, null, new KeyAction("Prefere&nces\\Open base config", DoOpenBaseConfig, null, false)));
		keyMap.AddItem(new KeyItem(Keys.None, null, new KeyAction("Prefere&nces\\Reset config...", DoResetConfig, null, false)));
		keyMap.AddItem(new KeyItem(Keys.None, null, new KeyAction("Prefere&nces\\Reset temp and close", DoResetTempAndClose, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.Shift | Keys.F3, null, new KeyAction("Prefere&nces\\Edit current scheme", DoEditCurrentScheme, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.F3, null, new KeyAction("Prefere&nces\\Open AppDdata folder", DoOpenAppDataFolder, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Shift | Keys.F3, null, new KeyAction("Prefere&nces\\Open startup folder", DoOpenStartupFolder, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.F4, null, new KeyAction("Prefere&nces\\Open current folder", DoOpenCurrentFolder, null, false)));
		keyMap.AddItem(new KeyItem(Keys.F4, null, new KeyAction("Prefere&nces\\Change current folder", DoChangeCurrentFolder, null, false)));
		keyMap.AddItem(new KeyItem(Keys.None, null, new KeyAction("Prefere&nces\\New syntax file", DoNewSyntax, null, false)));
		keyMap.AddItem(new KeyItem(Keys.F5, null, new KeyAction("Prefere&nces\\Execute command", DoExecuteF5Command, null, false)));
		keyMap.AddItem(new KeyItem(Keys.F6, null, new KeyAction("Prefere&nces\\Execute command", DoExecuteF6Command, null, false)));
		keyMap.AddItem(new KeyItem(Keys.F7, null, new KeyAction("Prefere&nces\\Execute command", DoExecuteF7Command, null, false)));
		keyMap.AddItem(new KeyItem(Keys.F8, null, new KeyAction("Prefere&nces\\Execute command", DoExecuteF8Command, null, false)));

		keyMap.AddItem(new KeyItem(Keys.F1, null, new KeyAction("&?\\Help", DoHelp, null, false)));
	}

	private bool DoNew(Controller controller)
	{
		RemoveEmptyIfNeed();
		mainNest.Frame.AddBuffer(NewFileBuffer());
		return true;
	}

	private bool DoOpen(Controller controller)
	{
		OpenFileDialog dialog = new OpenFileDialog();
		if (dialog.ShowDialog() == DialogResult.OK)
			LoadFile(dialog.FileName);
		return true;
	}

	public Buffer LoadFile(string file)
	{
		return LoadFile(file, null);
	}

	public Buffer LoadFile(string file, string httpServer)
	{
		string fullPath = null;
		string name = null;
		try
		{
			fullPath = Path.GetFullPath(file);
			name = Path.GetFileName(file);
		}
		catch (Exception e)
		{
			Log.WriteWarning("Path", e.Message);
			Log.Open();
			return null;
		}
		Buffer buffer = mainNest.buffers.GetBuffer(fullPath, name);
		bool needLoad = false;
		bool isNew = false;
		if (buffer == null)
		{
			buffer = NewFileBuffer();
			buffer.httpServer = httpServer;
			needLoad = true;
			isNew = true;
		}
		buffer.SetFile(fullPath, name);
		ShowBuffer(mainNest, buffer);
		if (buffer.Frame != null)
			buffer.Frame.UpdateHighlighter();

		if (needLoad && !ReloadFile(buffer))
		{
			if (isNew && buffer.Frame != null)
				buffer.Frame.RemoveBuffer(buffer);
			return null;
		}
		RemoveEmptyIfNeed();
		return buffer;
	}

	public Buffer ForcedLoadFile(string file)
	{
		string fullPath = null;
		string name = null;
		try
		{
			fullPath = Path.GetFullPath(file);
			name = Path.GetFileName(file);
		}
		catch (Exception e)
		{
			Log.WriteWarning("Path", e.Message);
			Log.Open();
			return null;
		}
		Buffer buffer = mainNest.buffers.GetBuffer(fullPath, name);
		bool needLoad = false;
		if (buffer == null)
		{
			buffer = NewFileBuffer();
			needLoad = File.Exists(fullPath);
		}
		buffer.SetFile(fullPath, name);
		ShowBuffer(mainNest, buffer);
		if (buffer.Frame != null)
			buffer.Frame.UpdateHighlighter();

		if (needLoad && !ReloadFile(buffer))
			return null;
		RemoveEmptyIfNeed();
		return buffer;
	}

	public bool ReloadFile(Buffer buffer)
	{
		if (buffer.httpServer != null)
		{
			string text = "";
			try
			{
				HttpWebRequest request = (HttpWebRequest)WebRequest.Create(buffer.httpServer + "/" + buffer.Name + "/get");
				request.Timeout = settings.connectionTimeout.Value;
				request.Method = "POST";
				request.ContentType = "application/x-www-form-urlencoded";
				request.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

				byte[] byteVersion = Encoding.ASCII.GetBytes("NULL");
				request.ContentLength = byteVersion.Length;

				Stream stream = request.GetRequestStream();
				stream.Write(byteVersion, 0, byteVersion.Length);
				stream.Close();

				HttpWebResponse response = (HttpWebResponse)request.GetResponse();

				buffer.encodingPair = settings.httpEncoding.Value;
				using (StreamReader reader = new StreamReader(response.GetResponseStream(), settings.httpEncoding.Value.encoding))
				{
					text = reader.ReadToEnd();
				}
			}
			catch (Exception e)
			{
				Log.WriteError("http", e.ToString());
				Log.Open();
				return false;
			}
			buffer.InitText(text);
			return true;
		}
		if (!File.Exists(buffer.FullPath))
		{
			Log.WriteWarning("Missing file", buffer.FullPath);
			Log.Open();
			return false;
		}
		{
			tempSettings.ApplyQualitiesBeforeLoading(buffer);
			byte[] bytes = null;
			try
			{
				bytes = File.ReadAllBytes(buffer.FullPath);
			}
			catch (IOException e)
			{
				Log.WriteError("File loading error", e.Message);
				Log.Open();
			}
			string error;
			buffer.InitBytes(bytes, settings.defaultEncoding.Value, out error);
			if (error != null)
			{
				Log.WriteError("File decoding error", error);
				Log.Open();
			}
			buffer.fileInfo = new FileInfo(buffer.FullPath);
			buffer.lastWriteTimeUtc = buffer.fileInfo.LastWriteTimeUtc;
			buffer.needSaveAs = false;
			tempSettings.ApplyQualities(buffer);
			return true;
		}
	}

	private bool DoSave(Controller controller)
	{
		TrySaveFile(frames.GetSelectedBuffer(BufferTag.File));
		return true;
	}

	private bool DoSaveAs(Controller controller)
	{
		Buffer buffer = frames.GetSelectedBuffer(BufferTag.File);
		if (buffer != null)
		{
			SaveFileDialog dialog = new SaveFileDialog();
			dialog.FileName = buffer.Name;
			dialog.InitialDirectory = Path.GetDirectoryName(buffer.FullPath);
			if (dialog.ShowDialog() == DialogResult.OK)
			{
				buffer.SetFile(Path.GetFullPath(dialog.FileName), Path.GetFileName(dialog.FileName));
				SaveFile(buffer);
				UpdateTitle();
			}
		}
		return true;
	}

	private void TrySaveFile(Buffer buffer)
	{
		if (buffer == null)
			return;
		buffer.settedEncodingPair = buffer.encodingPair;
		if (buffer.httpServer != null)
		{
			string text = "";
			try
			{
				HttpWebRequest request = (HttpWebRequest)WebRequest.Create(buffer.httpServer + "/" + buffer.Name + "/push");
				request.Timeout = settings.connectionTimeout.Value;
				request.Method = "POST";
				request.ContentType = "application/x-www-form-urlencoded";
				request.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

				byte[] byteVersion = buffer.encodingPair.encoding.GetBytes(buffer.Controller.Lines.GetText());
				request.ContentLength = byteVersion.Length;

				Stream stream = request.GetRequestStream();
				stream.Write(byteVersion, 0, byteVersion.Length);
				stream.Close();

				HttpWebResponse response = (HttpWebResponse)request.GetResponse();

				using (StreamReader reader = new StreamReader(response.GetResponseStream(), buffer.encodingPair.encoding))
				{
					text = reader.ReadToEnd();
				}
			}
			catch (Exception e)
			{
				Log.WriteError("http", e.ToString());
				Log.Open();
				return;
			}
			buffer.Controller.history.MarkAsSaved();
			Log.WriteInfo("Responce", text);
			return;
		}
		if (!string.IsNullOrEmpty(buffer.FullPath) && !buffer.needSaveAs)
		{
			SaveFile(buffer);
			return;
		}
		SaveFileDialog dialog = new SaveFileDialog();
		dialog.FileName = buffer.Name;
		if (!string.IsNullOrEmpty(buffer.FullPath))
			dialog.InitialDirectory = Path.GetDirectoryName(buffer.FullPath);
		if (dialog.ShowDialog() == DialogResult.OK)
		{
			buffer.SetFile(Path.GetFullPath(dialog.FileName), Path.GetFileName(dialog.FileName));
			SaveFile(buffer);
		}
	}

	public void SaveFile(Buffer buffer)
	{
		string text = buffer.Controller.Lines.GetText();
		try
		{
			if (!buffer.encodingPair.bom && buffer.encodingPair.encoding == Encoding.UTF8)
				File.WriteAllText(buffer.FullPath, text);
			else
				File.WriteAllText(buffer.FullPath, text, buffer.encodingPair.encoding);
		}
		catch (Exception e)
		{
			MessageBox.Show(e.Message, Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
			return;
		}
		buffer.Controller.history.MarkAsSaved();
		buffer.fileInfo = new FileInfo(buffer.FullPath);
		buffer.lastWriteTimeUtc = buffer.fileInfo.LastWriteTimeUtc;
		buffer.needSaveAs = false;
		string fullPath = buffer.FullPath.ToLowerInvariant();
		string syntaxDir = Path.GetDirectoryName(buffer.FullPath).ToLowerInvariant();
		if (fullPath == AppPath.ConfigPath.GetCurrentPath().ToLowerInvariant() ||
			fullPath == AppPath.ConfigPath.startupPath.ToLowerInvariant() ||
			fullPath == AppPath.ConfigPath.appDataPath.ToLowerInvariant())
		{
			ReloadConfig();
		}
		else if (schemeManager.IsActiveSchemePath(settings.scheme.Value, buffer.FullPath))
		{
			ApplySettings();
		}
		else if (Path.GetExtension(buffer.FullPath).ToLowerInvariant() == ".xml" &&
			(syntaxDir == AppPath.SyntaxDir.appDataPath.ToLowerInvariant() ||
			syntaxDir == AppPath.SyntaxDir.startupPath.ToLowerInvariant()))
		{
			ReloadSyntaxes();
		}
	}

	private bool DoExit(Controller controller)
	{
		Close();
		return true;
	}

	private bool DoOpenCloseLog(Controller controller)
	{
		return OpenCloseConsoleBuffer(LogId);
	}

	private bool DoOpenCloseFindResults(Controller controller)
	{
		return OpenCloseConsoleBuffer(FindResultsId);
	}

	private bool DoOpenCloseShellResults(Controller controller)
	{
		return OpenCloseConsoleBuffer(ShellResultsId);
	}

	private bool OpenCloseConsoleBuffer(string id)
	{
		Buffer buffer;
		consoleBuffers.TryGetValue(id, out buffer);
		if (buffer == null)
			return false;
		if (consoleNest.buffers.list.Selected == buffer)
		if (buffer.Frame != null)
		{
			buffer.Frame.Destroy();
			return true;
		}
		consoleBuffers[id] = buffer;
		ShowBuffer(consoleNest, buffer);
		if (consoleNest.Frame != null)
			consoleNest.Frame.Focus();
		return true;
	}

	private bool DoOpenCloseConsolePanel(Controller controller)
	{
		if (consoleNest.AFrame != null)
		{
			if (consoleNest.AFrame.Focused)
				consoleNest.AFrame.Destroy();
			else
				consoleNest.AFrame.Focus();
		}
		else
		{
			new Frame().Create(consoleNest);
			if (consoleNest.buffers.list.Count == 0)
				Log.Open();
			consoleNest.Frame.Focus();
		}
		return true;
	}

	private bool DoCloseConsolePanel(Controller controller)
	{
		if (consoleNest.AFrame != null)
		{
			consoleNest.AFrame.Destroy();
			return true;
		}
		return false;
	}

	private bool DoOpenCloseFileTree(Controller controller)
	{
		if (leftNest.AFrame != null && leftNest.AFrame.Focused && leftNest.buffers.list.Selected == fileTree.Buffer)
		{
			leftNest.AFrame.Destroy();
		}
		else
		{
			fileTree.Reload();
			if (leftNest.AFrame == null)
				new Frame().Create(leftNest);
			leftNest.Frame.AddBuffer(fileTree.Buffer);
			leftNest.Frame.Focus();
		}
		return true;
	}

	private bool DoFindFileInTree(Controller controller)
	{
		Buffer buffer = LastBuffer;
		if (buffer == null || buffer.FullPath == null)
			return false;
		OpenFileTree();
		fileTree.Find(buffer.FullPath);
		leftNest.Frame.Focus();
		return true;
	}

	public bool FileTreeOpened
	{
		get { return fileTree.Buffer.Frame != null; }
	}

	public FileTree FileTree { get { return fileTree; } }

	public void OpenFileTree()
	{
		if (fileTree.Buffer.Frame == null)
		{
			if (leftNest.AFrame == null)
				new Frame().Create(leftNest);
			leftNest.Frame.AddBuffer(fileTree.Buffer);
			leftNest.Frame.Focus();
		}
	}

	private bool DoChangeFocus(Controller controller)
	{
		Frame frame = frames.GetChildFrame(frames.GetFocusedFrame());
		if (frame == null)
			frame = frames.GetFirstFrame();
		if (frame != null)
			frame.Focus();
		return true;
	}

	private bool DoEditCreateCurrentConfig(Controller controller)
	{
		string path = AppPath.ConfigPath.GetCurrentPath();
		string templatePath = Path.Combine(AppPath.TemplatesDir, "current-tw-config.xml");
		if (!File.Exists(path))
		{
			if (!File.Exists(templatePath))
			{
				Log.WriteWarning("Config", "Missing template config at: " + templatePath);
				Log.Open();
				return false;
			}
			File.Copy(templatePath, path);
		}
		LoadFile(path);
		return true;
	}

	private bool DoOpenUserConfig(Controller controller)
	{
		CopyConfigIfNeed();
		LoadFile(AppPath.ConfigPath.appDataPath);
		return true;
	}

	private bool DoOpenBaseConfig(Controller controller)
	{
		if (!File.Exists(AppPath.ConfigPath.startupPath))
		{
			MessageBox.Show("Missing base config", Name, MessageBoxButtons.OK);
			return true;
		}
		LoadFile(AppPath.ConfigPath.startupPath);
		return true;
	}

	private bool DoResetConfig(Controller controller)
	{
		if (!File.Exists(AppPath.ConfigPath.appDataPath))
		{
			MessageBox.Show("Nothing to reset", Name, MessageBoxButtons.OK);
			return true;
		}
		DialogResult result = MessageBox.Show("Current config will be removed", Name, MessageBoxButtons.OKCancel);
		if (result == DialogResult.OK)
		{
			if (File.Exists(AppPath.ConfigPath.appDataPath))
				File.Delete(AppPath.ConfigPath.appDataPath);
			CopyConfigIfNeed();
			ReloadConfig();

			activationInProcess = true;
			string fullPath = Path.GetFullPath(AppPath.ConfigPath.appDataPath);
			string name = Path.GetFileName(AppPath.ConfigPath.appDataPath);
			Buffer buffer = mainNest.buffers.GetBuffer(fullPath, name);
			if (buffer != null && buffer.Frame != null)
				CheckFileChange(buffer);
			activationInProcess = false;
		}
		return true;
	}

	private bool DoResetTempAndClose(Controller controller)
	{
		string path = TempSettings.GetTempSettingsPath();
		if (File.Exists(path))
			File.Delete(path);
		forbidTempSaving = true;
		Close();
		return true;
	}

	private bool DoEditCurrentScheme(Controller controller)
	{
		CreateAppDataFolders();
		List<AppPath> paths = schemeManager.GetSchemePaths(settings.scheme.Value);
		if (paths.Count > 0)
		{
			foreach (AppPath path in paths)
			{
				if (!File.Exists(path.appDataPath))
					File.Copy(path.startupPath, path.appDataPath);
				LoadFile(path.appDataPath);
			}
		}
		return true;
	}

	private bool DoOpenAppDataFolder(Controller controller)
	{
		CreateAppDataFolders();
		System.Diagnostics.Process process = new System.Diagnostics.Process();
		process.StartInfo.FileName = AppPath.AppDataDir;
		process.Start();
		return true;
	}

	private bool DoOpenStartupFolder(Controller controller)
	{
		System.Diagnostics.Process process = new System.Diagnostics.Process();
		process.StartInfo.FileName = AppPath.StartupDir;
		process.Start();
		return true;
	}

	private bool DoOpenCurrentFolder(Controller controller)
	{
		System.Diagnostics.Process process = new System.Diagnostics.Process();
		process.StartInfo.FileName = Directory.GetCurrentDirectory();
		process.Start();
		return true;
	}

	private bool DoChangeCurrentFolder(Controller controller)
	{
		FolderBrowserDialog dialog = new FolderBrowserDialog();
		dialog.Description = "Current folder selection";
		dialog.SelectedPath = Directory.GetCurrentDirectory();
		if (dialog.ShowDialog() == DialogResult.OK)
		{
			string error;
			if (!SetCurrentDirectory(dialog.SelectedPath, out error))
				MessageBox.Show(error, Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		return true;
	}

	private bool DoNewSyntax(Controller controller)
	{
		CreateAppDataFolders();
		string templatePath = Path.Combine(AppPath.TemplatesDir, "syntax.xml");
		string filePath = Path.Combine(AppPath.SyntaxDir.appDataPath, "new-syntax.xml");
		Buffer buffer = ForcedLoadFile(filePath);
		if (!File.Exists(templatePath))
		{
			Log.WriteWarning("Missing template", templatePath);
			Log.Open();
			return true;
		}
		buffer.InitText(File.ReadAllText(templatePath));
		return true;
	}

	private void CreateAppDataFolders()
	{
		CopyConfigIfNeed();
		if (!Directory.Exists(AppPath.SyntaxDir.appDataPath))
			Directory.CreateDirectory(AppPath.SyntaxDir.appDataPath);
		if (!File.Exists(AppPath.SyntaxDtd.appDataPath) && File.Exists(AppPath.SyntaxDtd.startupPath))
			File.Copy(AppPath.SyntaxDtd.startupPath, AppPath.SyntaxDtd.appDataPath);
		if (!Directory.Exists(AppPath.SchemesDir.appDataPath))
			Directory.CreateDirectory(AppPath.SchemesDir.appDataPath);
	}

	private bool DoHelp(Controller controller)
	{
		ProcessHelp();
		return true;
	}

	private Buffer _helpBuffer;

	public void ProcessHelp()
	{
		if (_helpBuffer == null || _helpBuffer.Frame == null)
		{
			string text = "# About\n" +
				"\n" +
				Application.ProductName + "\n" +
				"Build " + Application.ProductVersion + "\n" +
				"\n" +
				"# Actions\n" +
				"\n" +
				"All actions are represented in menu.\n" +
				"Menu subitems are depended on frame with cursor\n" +
				"[] in menu item denotes complex shortcut,\n" +
				"i.e. for [Ctrl+Tab]:\n" +
				"\tCtrl↓, Tab↓↑, Ctrl↑ - switch back / forward between 2 tabs\n" +
				"\tCtrl↓, Tab↓↑, Tab↓↑, Ctrl↑ - switch back / forward between 3 tabs\n" +
				"\n" +
				commander.GetHelpText() + "\n" +
				settings.GetHelpText();
			_helpBuffer = new Buffer(null, "Help.twh", SettingsMode.Normal);
			_helpBuffer.tags = BufferTag.Other;
			_helpBuffer.onRemove = OnHelpBufferRemove;
			_helpBuffer.Controller.isReadonly = true;
			_helpBuffer.Controller.InitText(text);
			ShowBuffer(mainNest, _helpBuffer);
		}
		else
		{
			_helpBuffer.Frame.RemoveBuffer(_helpBuffer);
			_helpBuffer = null;
		}
	}

	private bool OnHelpBufferRemove(Buffer buffer)
	{
		_helpBuffer = null;
		return true;
	}

	private Buffer NewFileBuffer()
	{
		Buffer buffer = new Buffer(null, UntitledTxt, SettingsMode.Normal);
		buffer.tags = BufferTag.File;
		buffer.needSaveAs = true;
		buffer.onRemove = OnFileBufferRemove;
		buffer.encodingPair = settings.defaultEncoding.Value;
		return buffer;
	}

	public void ShowBuffer(Nest nest, Buffer buffer)
	{
		if (nest.Frame == null)
			new Frame().Create(nest);
		nest.Frame.AddBuffer(buffer);
	}

	private Dictionary<string, Buffer> consoleBuffers = new Dictionary<string, Buffer>();

	public const string LogId = "LogId";
	public const string FindResultsId = "FindResultsId";
	public const string ShellResultsId = "ShellResultsId";

	public void ShowConsoleBuffer(string id, Buffer buffer)
	{
		Buffer oldBuffer;
		consoleBuffers.TryGetValue(id, out oldBuffer);
		if (oldBuffer != null)
		{
			if (oldBuffer.Frame != null)
				oldBuffer.Frame.RemoveBuffer(oldBuffer);
			else
				consoleNest.buffers.list.Remove(oldBuffer);
			consoleBuffers.Remove(id);
		}
		RegisterConsoleBuffer(id, buffer);
		if (buffer != null)
		{
			ShowBuffer(consoleNest, buffer);
			if (consoleNest.Frame != null)
				consoleNest.Frame.Focus();
		}
	}

	public void RegisterConsoleBuffer(string id, Buffer buffer)
	{
		if (id != null && buffer != null)
			consoleBuffers[id] = buffer;
		else if (id != null)
			consoleBuffers.Remove(id);
	}

	private bool OnFileBufferRemove(Buffer buffer)
	{
		if (buffer != null)
		{
			tempSettings.StorageQualities(buffer);
			if (buffer.Changed)
			{
				DialogResult result = MessageBox.Show("Do you want to save the current changes in\n" + buffer.Name + "?", Name, MessageBoxButtons.YesNoCancel);
				switch (result)
				{
					case DialogResult.Yes:
						TrySaveFile(buffer);
						return true;
					case DialogResult.No:
						return true;
					case DialogResult.Cancel:
						return false;
				}
			}
			else
			{
				return true;
			}
		}
		return false;
	}

	private void CopyConfigIfNeed()
	{
		if (!File.Exists(AppPath.ConfigPath.appDataPath))
		{
			if (!File.Exists(AppPath.ConfigPath.startupPath))
			{
				Log.WriteWarning("Config", "Missing base config at: " + AppPath.ConfigPath.startupPath);
				Log.Open();
				return;
			}
			File.Copy(AppPath.ConfigPath.startupPath, AppPath.ConfigPath.appDataPath);
			Log.WriteInfo("Config", "Config was created: " + AppPath.ConfigPath.appDataPath);
		}
	}

	private bool hasCurrentConfig = false;

	private void ReloadConfig()
	{
		hasCurrentConfig = false;
		configParser.Reset();
		StringBuilder builder = new StringBuilder();
		foreach (string path in AppPath.ConfigPath.GetBoth())
		{
			if (File.Exists(path))
			{
				XmlDocument xml = xmlLoader.Load(path, false);
				if (xml != null)
					configParser.Parse(xml, builder);
			}
		}
		{
			string path = AppPath.ConfigPath.GetCurrentPath();
			if (path != AppPath.ConfigPath.startupPath && File.Exists(path))
			{
				XmlDocument xml = xmlLoader.Load(path, false);
				if (xml != null)
				{
					configParser.Parse(xml, builder);
					hasCurrentConfig = true;
				}
			}
		}
		if (builder.Length > 0)
		{
			Log.WriteError("Config", builder.ToString());
			Log.Open();
		}
		settings.DispatchChange();
	}

	public void UpdateHighlighter(MulticaretTextBox textBox, string fileName)
	{
		if (fileName == null)
		{
			textBox.Highlighter = null;
			return;
		}
		string syntax = syntaxFilesScanner.GetSyntaxByFile(fileName);
		string extension = fileName.ToLowerInvariant();
		textBox.Highlighter = syntax != null ? highlightingSet.GetHighlighter(syntax) : null;
	}

	public void NavigateTo(string fileName, int position0, int position1)
	{
		Buffer buffer = LoadFile(fileName);
		if (buffer != null)
		{
			buffer.Controller.PutCursor(buffer.Controller.Lines.PlaceOf(position0), false);
			buffer.Controller.PutCursor(buffer.Controller.Lines.PlaceOf(position1), true);
			if (buffer.Frame != null)
			{
				buffer.Frame.Focus();
				buffer.Frame.TextBox.MoveToCaret();
			}
		}
	}

	public void NavigateTo(string fileName, Place place0, Place place1)
	{
		Buffer buffer = LoadFile(fileName);
		if (buffer != null)
		{
			buffer.Controller.PutCursor(place0, false);
			buffer.Controller.PutCursor(place1, true);
			if (buffer.Frame != null)
			{
				buffer.Frame.Focus();
				buffer.Frame.TextBox.MoveToCaret();
			}
		}
	}

	private bool DoExecuteF5Command(Controller controller)
	{
		return ExecuteCommand(settings.f5Command.Value);
	}

	private bool DoExecuteF6Command(Controller controller)
	{
		return ExecuteCommand(settings.f6Command.Value);
	}

	private bool DoExecuteF7Command(Controller controller)
	{
		return ExecuteCommand(settings.f7Command.Value);
	}

	private bool DoExecuteF8Command(Controller controller)
	{
		return ExecuteCommand(settings.f8Command.Value);
	}
	
	private void ReloadSyntaxes()
	{
		syntaxFilesScanner.Rescan();
		highlightingSet.UpdateParameters(syntaxFilesScanner);
		mainNest.Frame.UpdateHighlighter();
		foreach (Buffer buffer in mainNest.buffers.list)
		{
			buffer.Controller.Lines.ResetHighlighting();
		}
	}

	private bool ExecuteCommand(string command)
	{
		commander.Execute(command);
		return true;
	}
}
