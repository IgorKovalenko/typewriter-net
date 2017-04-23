using System;
using System.Windows.Forms;
using System.Collections.Generic;

namespace MulticaretEditor
{
	public class Receiver
	{
		public event Setter ViModeChanged;
		
		private readonly Controller controller;
		private readonly LineArray lines;
		private readonly bool alwaysInputMode;
		
		private Context context;
		private AReceiver state;
		
		public Dictionary<char, char> viMap;
		
		private bool viMode;
		public bool ViMode { get { return viMode; } }
		
		public Receiver(Controller controller, bool viMode, bool alwaysInputMode)
		{
			this.controller = controller;
			this.lines = controller.Lines;
			this.alwaysInputMode = alwaysInputMode;
			
			context = new Context(this);
			ProcessSetViMode(viMode);
		}
		
		public void SetViMode(bool value)
		{
			if (viMode != value)
			{
				ProcessSetViMode(value);
			}
		}
		
		private void ProcessSetViMode(bool value)
		{
			if (value)
			{
				context.SetState(new ViReceiver(null));
			}
			else
			{
				context.SetState(new InputReceiver(null, alwaysInputMode));
			}
		}
		
		public class Context
		{
			private Receiver receiver;
			
			public Context(Receiver receiver)
			{
				this.receiver = receiver;
			}
			
			public void SetState(AReceiver state)
			{
				if (receiver.state != state)
				{
					receiver.state = state;
					receiver.state.Init(receiver.controller, this);
					receiver.state.DoOn();
					if (receiver.viMode != receiver.state.AltMode)
					{
						receiver.viMode = receiver.state.AltMode;
						if (receiver.ViModeChanged != null)
						{
							receiver.ViModeChanged();
						}
					}
				}
			}
			
			public char GetMapped(char c)
			{
				if (receiver.viMap != null && !ClipboardExecuter.IsEnLayout())
				{
					char result;
					if (receiver.viMap.TryGetValue(c, out result))
					{
						return result;
					}
				}
				return c;
			}
		}
		
		public void DoKeyPress(char code, out string viShortcut, out bool scrollToCursor)
		{
			state.DoKeyPress(code, out viShortcut, out scrollToCursor);
		}
		
		public bool DoKeyDown(Keys keysData, out bool scrollToCursor)
		{
			return state.DoKeyDown(keysData, out scrollToCursor);
		}
		
		public bool DoFind(string text)
		{
			return state.DoFind(text);
		}
		
		public void ResetViInput()
		{
			state.ResetViInput();
		}
	}
}
