using System;
using System.IO;
using System.Xml;

public class XmlLoader
{
	private MainForm mainForm;

	public XmlLoader(MainForm mainForm)
	{
		this.mainForm = mainForm;
	}

	public XmlDocument Load(string file, bool ignoreMissingFile)
	{
		if (!File.Exists(file))
		{
			if (!ignoreMissingFile)
			{
				mainForm.Log.WriteLine("Missing file: " + file);
				mainForm.Log.Open();
			}
			return null;
		}
		try
		{
			XmlDocument xml = new XmlDocument();
			xml.Load(file);
			return xml;
		}
		catch (Exception e)
		{
			mainForm.Log.WriteLine("Error: " + e.Message);
			mainForm.Log.Open();
		}
		return null;
	}
}
