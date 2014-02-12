using System;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace MulticaretEditor
{
	public class ClipboardExecuter
	{
		public string text;
		
		public void PutTo()
		{
			try
			{
				if (text != null && text != "")
					Clipboard.SetText(text);
			}
			catch (ExternalException)
			{
			}
		}
		
		public void GetFrom()
		{
			if (Clipboard.ContainsText())
				text = Clipboard.GetText();
		}
		
		public static void PutToClipboard(string text)
		{
			ClipboardExecuter executer = new ClipboardExecuter();
			executer.text = text;
			Thread thread = new Thread(executer.PutTo);
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
		}
		
		public static string GetFromClipboard()
		{
			ClipboardExecuter executer = new ClipboardExecuter();
			Thread thread = new Thread(executer.GetFrom);
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			return executer.text;
		}
	}
}
