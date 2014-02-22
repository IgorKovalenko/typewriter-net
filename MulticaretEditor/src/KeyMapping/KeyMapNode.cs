using System;
using System.Collections.Generic;

namespace MulticaretEditor.KeyMapping
{
	public class KeyMapNode
	{
		public readonly KeyMap main;

		public KeyMapNode(KeyMap main)
		{
			this.main = main;
		}

		public readonly List<KeyMapNode> before = new List<KeyMapNode>();
		public readonly List<KeyMapNode> after = new List<KeyMapNode>();

		public void AddBefore(KeyMap map)
		{
			before.Add(new KeyMapNode(map));
		}

		public void AddAfter(KeyMap map)
		{
			after.Add(new KeyMapNode(map));
		}

		public bool Enumerate<T>(Getter<KeyMap, T, bool> enumerator, T parameter)
		{
			for (int i = 0, count = before.Count; i < count; i++)
			{
				if (before[i].Enumerate<T>(enumerator, parameter))
					return true;
			}
			if (main != null && enumerator(main, parameter))
				return true;
			for (int i = 0, count = after.Count; i < count; i++)
			{
				if (after[i].Enumerate<T>(enumerator, parameter))
					return true;
			}
			return false;
		}

		public List<KeyMap> ToList()
		{
			List<KeyMap> list = new List<KeyMap>();
			AddToList(list);
			return list;
		}

		private void AddToList(List<KeyMap> list)
		{
			for (int i = 0, count = before.Count; i < count; i++)
			{
				before[i].AddToList(list);
			}
			if (main != null)
				list.Add(main);
			for (int i = 0, count = after.Count; i < count; i++)
			{
				after[i].AddToList(list);
			}
		}
	}
}
