using System;
using System.IO;
using MulticaretEditor;

public class Buffer
{
	public Buffer(string fullPath, string name)
	{
		SetFile(fullPath, name);
		controller = new Controller(new LineArray());
	}
	
	private Controller controller;
	public Controller Controller { get { return controller; } }
	
	private string fullPath;
	public string FullPath { get { return fullPath; } }
	
	private string name;
	public string Name { get { return name; } }
	
	public void SetFile(string fullPath, string name)
	{
		this.fullPath = fullPath;
		this.name = name;
	}
	
	public bool first;
	public bool needSaveAs;
	public FileInfo fileInfo;
	public DateTime lastWriteTimeUtc;
	public string Tag;
	
	public static string StringOf(Buffer info)
	{
		return info.Name + (info.Controller.history.Changed ? "*" : "");
	}
}
