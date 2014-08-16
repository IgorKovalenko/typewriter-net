using System;
using System.IO;
using System.Windows.Forms;

public struct AppPath
{
	public const string Syntax = "syntax";
	public const string Schemes = "schemes";
	public const string Templates = "templates";

	private static string startupDir;
	public static string StartupDir { get { return startupDir; } }

	private static string appDataDir;
	public static string AppDataDir { get { return appDataDir; } }

	private static string templatesDir;
	public static string TemplatesDir { get { return templatesDir; } }

	private static AppPath syntaxDir;
	public static AppPath SyntaxDir { get { return syntaxDir; } }

	private static AppPath syntaxDtd;
	public static AppPath SyntaxDtd { get { return syntaxDtd; } }

	private static AppPath schemesDir;
	public static AppPath SchemesDir { get { return schemesDir; } }

	private static string configPath;
	public static string ConfigPath { get { return configPath; } }

	private static string configTemplatePath;
	public static string ConfigTemplatePath { get { return configTemplatePath; } }

	public static void Init(string startupDir, string appDataDir)
	{
		AppPath.startupDir = startupDir;
		AppPath.appDataDir = appDataDir;
		AppPath.templatesDir = Path.Combine(startupDir, Templates);
		AppPath.syntaxDir = new AppPath(Syntax);
		AppPath.syntaxDtd = new AppPath(Path.Combine(Syntax, "language.dtd"));
		AppPath.schemesDir = new AppPath(Schemes);
		AppPath.configPath = Path.Combine(appDataDir, "config.xml");
		AppPath.configTemplatePath = Path.Combine(templatesDir, "config.xml");
	}

	public readonly string local;
	public readonly string appDataPath;
	public readonly string startupPath;

	public AppPath(string local)
	{
		this.local = local;
		appDataPath = Path.Combine(appDataDir, local);
		startupPath = Path.Combine(startupDir, local);
	}

	public string GetExisted()
	{
		if (File.Exists(appDataPath))
			return appDataPath;
		if (File.Exists(startupPath))
			return startupPath;
		return null;
	}
}
