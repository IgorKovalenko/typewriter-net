using System;
using System.Collections.Generic;

namespace MulticaretEditor
{
	public class RE_L : RENode
	{
		private readonly RENode _next;
		private readonly bool _upper;
		
		public RE_L(bool upper, RENode next)
		{
			_upper = upper;
			_next = next;
		}
		
		public override RENode MatchChar(char c)
		{
			bool result = char.IsLower(c);
			if (_upper)
			{
				result = !result && c != '\n' && c != '\r';
			}
			return result ? _next : RENode.fail;
		}
		
		public override void FillResetNodes(List<RENode> nodes)
		{
			if (_next != null)
			{
				_next.FillResetNodes(nodes);
			}
		}
		
		public override string ToString()
		{
			return "(" + (_upper ? "L" : "l") + _next + ")";
		}
	}
}