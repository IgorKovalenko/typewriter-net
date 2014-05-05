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
using MulticaretEditor;
using MulticaretEditor.Highlighting;
using MulticaretEditor.KeyMapping;
using TypewriterNET;
using TypewriterNET.Frames;

public class MainForm : Form
{
	private readonly string[] args;
	private readonly Settings settings;
	private readonly MainFormMenu menu;

	public readonly FrameList frames;

	public MainForm(string[] args)
	{
		this.args = args;

		frames = new FrameList(this);

		ResourceManager manager = new ResourceManager("TypewriterNET", typeof(Program).Assembly);
		Icon = (Icon)manager.GetObject("icon");
		Name = Application.ProductName;
		Text = Name;

		menu = new MainFormMenu(this);
		Menu = menu;

		settings = new Settings(ApplySettings);

		Load += OnLoad;
	}

	private Nest mainNest;
	private Nest consoleNest;
	private Nest leftNest;
	private XmlLoader xmlLoader;
	private FileDragger fileDragger;
	private TempSettings tempSettings;

	private Frame mainFrame;
	public Frame MainFrame { get { return mainFrame; } }

	private Log log;
	public Log Log { get { return log; } }

	private void OnLoad(object sender, EventArgs e)
	{
		string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TypewriterNET");
		if (!Directory.Exists(appDataPath))
			Directory.CreateDirectory(appDataPath);
		AppPath.Init(Application.StartupPath, appDataPath);

		BuildMenu();

		mainNest = AddNest(false, true, true, 70);
		mainFrame = new Frame("", keyMap, doNothingKeyMap);
		mainNest.AFrame = mainFrame;

		consoleNest = AddNest(false, false, true, 20);
		leftNest = AddNest(true, true, true, 20);

		leftNest.AFrame = new Frame("", keyMap, doNothingKeyMap);

		mainNest.Frame.AddBuffer(NewFileBuffer());

		log = new Log(this, consoleNest);
		xmlLoader = new XmlLoader(this);

		SetFocus(null, new KeyMapNode(keyMap, 0));

		ApplySettings();
		ReloadConfig();
		fileDragger = new FileDragger(this);

		tempSettings = new TempSettings(this, settings);
		tempSettings.Load();
		if (args.Length == 1)
		{
			LoadFile(args[0]);
		}
		FormClosing += OnFormClosing;
	}

	private void OnFormClosing(object sender, FormClosingEventArgs e)
	{
		tempSettings.Save();
		foreach (Buffer buffer in frames.GetBuffers(BufferTag.File))
		{
			if (buffer.onRemove != null && !buffer.onRemove(buffer))
			{
				e.Cancel = true;
				break;
			}
		}
	}

	public KeyMapNode MenuNode { get { return menu.node; } }

	private MulticaretTextBox focusedTextBox;
	public Controller FocusedController { get { return focusedTextBox != null ? focusedTextBox.Controller : null; } }

	public void SetFocus(MulticaretTextBox textBox, KeyMapNode node)
	{
		focusedTextBox = textBox;
		menu.node = node;
	}

	private void ApplySettings()
	{
		frames.UpdateSettings(settings);
		DoResize();
	}

	public void DoResize()
	{
		frames.Resize(0, 0, ClientSize);
	}

	private Nest AddNest(bool hDivided, bool left, bool isPercents, int percents)
	{
		Nest nest = frames.AddParentNode();
		nest.hDivided = hDivided;
		nest.left = left;
		nest.isPercents = isPercents;
		nest.size = percents;
		return nest;
	}

	override protected void OnResize(EventArgs e)
	{
		base.OnResize(e);
		DoResize();
	}

	private KeyMap keyMap;
	private KeyMap doNothingKeyMap;

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
		
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.Oemtilde, null, new KeyAction("&View\\Open/close editor console", DoOpenCloseEditorConsole, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.E, null, new KeyAction("&View\\Change focus", DoChangeFocus, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.F, null, new KeyAction("F&ind\\Find...", DoFind, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.H, null, new KeyAction("F&ind\\Replace...", DoReplace, null, false)));
		
		keyMap.AddItem(new KeyItem(Keys.F2, null, new KeyAction("Prefere&nces\\Edit config", DoOpenUserConfig, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Shift | Keys.F2, null, new KeyAction("Prefere&nces\\Open base config", DoOpenBaseConfig, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.F2, null, new KeyAction("Prefere&nces\\Edit current scheme", DoOpenCurrentScheme, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.Shift | Keys.F2, null, new KeyAction("Prefere&nces\\Open current scheme all files", DoOpenCurrentScheme, null, false)));
		keyMap.AddItem(new KeyItem(Keys.Control | Keys.F3, null, new KeyAction("Prefere&nces\\Open AppDdata folder", DoOpenAppDataFolder, null, false)));
		keyMap.AddItem(new KeyItem(Keys.None, null, new KeyAction("Prefere&nces\\New syntax file", DoNewSyntax, null, false)));
		
		keyMap.AddItem(new KeyItem(Keys.F1, null, new KeyAction("&?\\About", DoAbout, null, false)));
		
		keyMap.AddItem(new KeyItem(Keys.Escape, null, new KeyAction("&View\\Close editor console", DoCloseEditorConsole, null, false)));
	}

	private bool DoNew(Controller controller)
	{
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

	public void LoadFile(string file)
	{
		string fullPath = Path.GetFullPath(file);
		Buffer buffer = NewFileBuffer();
		ShowBuffer(mainNest, buffer);
		buffer.SetFile(fullPath, Path.GetFileName(file));

		if (!File.Exists(buffer.FullPath))
		{
			Log.Write("Missing file: ", Ds.Keyword);
			Log.WriteLine(buffer.FullPath, Ds.Normal);
			Log.Open();
			return;
		}
		string text = "";
		try
		{
			text = File.ReadAllText(buffer.FullPath);
		}
		catch (IOException e)
		{
			Log.WriteLine("-- File loading errors:", Ds.Comment);
			Log.WriteLine(e.Message + "\n" + e.StackTrace);
			Log.Open();
		}
		buffer.Controller.InitText(text);
		//int caret = fileQualitiesStorage.Get(buffer.FullPath)["cursor"].Int;
		//buffer.Controller.PutCursor(buffer.Controller.SoftNormalizedPlaceOf(caret), false);
		buffer.Controller.NeedScrollToCaret();
		buffer.fileInfo = new FileInfo(buffer.FullPath);
		buffer.lastWriteTimeUtc = buffer.fileInfo.LastWriteTimeUtc;
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
			}
		}
		return true;
	}

	private void TrySaveFile(Buffer buffer)
	{
		if (buffer == null)
			return;
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
		try
		{
			File.WriteAllText(buffer.FullPath, buffer.Controller.Lines.GetText());
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
		//tabBar.Invalidate();
		
		/*if (AppPath.ConfigPath.HasPath(buffer.FullPath))
		{
			ReloadConfig();
		}
		else if (GetActiveSchemePaths(config).IndexOf(buffer.FullPath) != -1)
		{
			ReloadScheme();
		}
		
		UpdateHighlighter(buffer);*/
	}

	private bool DoExit(Controller controller)
	{
		Close();
		return true;
	}

	private bool DoOpenCloseEditorConsole(Controller controller)
	{
		if (Log.Opened)
			Log.Close();
		else
			Log.Open();
		return true;
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

	private FindDialog findDialog;
	private ReplaceDialog replaceDialog;

	private bool DoFind(Controller controller)
	{
		if (findDialog == null)
		{
			findDialog = new FindDialog("Find", keyMap, doNothingKeyMap);
			Nest nest = frames.AddParentNode();
			nest.AFrame = findDialog;
			nest.hDivided = false;
			nest.left = false;
			nest.isPercents = false;
			nest.size = findDialog.Height;
			findDialog.Focus();
		}
		else
		{
			frames.Remove(findDialog.Nest);
			findDialog = null;
		}
		OnResize(null);
		return true;
	}

	private bool DoReplace(Controller controller)
	{
		if (replaceDialog == null)
		{
			replaceDialog = new ReplaceDialog("Replace", keyMap, doNothingKeyMap);
			Nest nest = frames.AddParentNode();
			nest.AFrame = replaceDialog;
			nest.hDivided = false;
			nest.left = false;
			nest.isPercents = false;
			nest.size = replaceDialog.Height;
		}
		else
		{
			frames.Remove(replaceDialog.Nest);
			replaceDialog = null;
		}
		OnResize(null);
		return true;
	}

	private bool DoOpenUserConfig(Controller controller)
	{
		return true;
	}

	private bool DoOpenBaseConfig(Controller controller)
	{
		return true;
	}

	private bool DoOpenCurrentScheme(Controller controller)
	{
		return true;
	}

	private bool DoOpenAppDataFolder(Controller controller)
	{
		System.Diagnostics.Process process = new System.Diagnostics.Process();
		process.StartInfo.FileName = AppPath.AppDataDir;
		process.Start();
		return true;
	}

	private bool DoNewSyntax(Controller controller)
	{
		return true;
	}

	private Buffer _helpBuffer;

	private bool DoAbout(Controller controller)
	{
		if (_helpBuffer == null)
		{
			string text = "# About\n" +
				"\n" +
				Application.ProductName + "\n" +
				"Build " + Application.ProductVersion;
			_helpBuffer = new Buffer(null, "About.twh");
			_helpBuffer.tags = BufferTag.Other;
			_helpBuffer.onRemove = OnHelpBufferRemove;
			_helpBuffer.Controller.isReadonly = true;
			_helpBuffer.Controller.InitText(text);
		}
		ShowBuffer(mainNest, _helpBuffer);
		return true;
	}

	private bool OnHelpBufferRemove(Buffer buffer)
	{
		_helpBuffer = null;
		return true;
	}

	private bool DoCloseEditorConsole(Controller controller)
	{
		if (Log.Opened)
		{
			Log.Close();
			return true;
		}
		return false;
	}

	private Buffer NewFileBuffer()
	{
		Buffer buffer = new Buffer(null, "Untitled.txt");
		buffer.tags = BufferTag.File;
		buffer.needSaveAs = true;
		buffer.onRemove = OnFileBufferRemove;
		return buffer;
	}

	public void ShowBuffer(Nest nest, Buffer buffer)
	{
		if (nest.Frame == null)
			nest.AFrame = new Frame("", keyMap, doNothingKeyMap);
		nest.Frame.AddBuffer(buffer);
		DoResize();
	}

	private bool OnFileBufferRemove(Buffer buffer)
	{
		if (buffer != null)
		{
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

	private Config config = new Config();

	private void ReloadConfig()
	{
		config.Reset();
		
		StringBuilder errors = new StringBuilder();
		if (!File.Exists(AppPath.ConfigPath))
		{
			if (!File.Exists(AppPath.ConfigTemplatePath))
			{
				Log.WriteLine("Warning: Missing config", Ds.String);
				Log.Open();
			}
			File.Copy(AppPath.ConfigTemplatePath, AppPath.ConfigPath);
			Log.WriteLine("Config was created: " + AppPath.ConfigPath, Ds.Comment);
		}
		XmlDocument xml = xmlLoader.Load(AppPath.ConfigPath, false);
		if (xml != null)
		{
			StringBuilder builder = new StringBuilder();
			config.Parse(xml, builder);
			if (builder.Length > 0)
			{
				Log.WriteLine(builder.ToString());
				Log.Open();
			}
			StringWriter sw = new StringWriter();
			XmlTextWriter writer = new XmlTextWriter(sw);
			xml.WriteTo(writer);
		}
		settings.WordWrap = config.WordWrap;
		settings.ShowLineNumbers = config.ShowLineNumbers;
		settings.ShowLineBreaks = config.ShowLineBreaks;
		settings.HighlightCurrentLine = config.HighlightCurrentLine;
		settings.TabSize = config.TabSize;
		settings.LineBreak = config.LineBreak;
		settings.FontFamily = config.FontFamily;
		settings.FontSize = config.FontSize;
		settings.ScrollingIndent = config.ScrollingIndent;
		settings.ShowColorAtCursor = config.ShowColorAtCursor;
		settings.AltCharsSource = config.AltCharsSource;
		settings.AltCharsResult = config.AltCharsResult;
		settings.MaxFileQualitiesCount = config.MaxFileQualitiesCount;
		settings.RememberOpenedFiles = config.RememberOpenedFiles;
		settings.DispatchChange();
	}
}
