using System;
using System.Collections.Generic;

namespace MulticaretEditor
{
	public class RERegex
	{
		private readonly RENode _root;
		private RENode[] _resetNodes;
		
		public RERegex(RENode node)
		{
			_root = node;
			List<RENode> resetNodes = new List<RENode>();
			_root.FillResetNodes(resetNodes);
			_resetNodes = resetNodes.ToArray();
		}
		
		public RERegex(string pattern) : this(Parse(pattern))
		{
		}
		
		public int MatchLength(string text)
		{
			for (int i = 0; i < _resetNodes.Length; i++)
			{
				_resetNodes[i].Reset();
			}
			int matchLength = -1;
			RENode nodeI = _root;
			for (int i = 0; i < text.Length; i++)
			{
				nodeI = nodeI.MatchChar(text[i]);
				if (nodeI == RENode.fail)
				{
					matchLength = -1;
					break;
				}
				if (nodeI == null)
				{
					matchLength = i + 1;
					break;
				}
			}
			return matchLength;
		}
		
		public static RENode Parse(string pattern)
		{
			List<REToken> tokens = new List<REToken>();
			for (int i = 0; i < pattern.Length; i++)
			{
				char c = pattern[i];
				if (c == '\\')
				{
					i++;
					if (i >= pattern.Length)
					{
						break;
					}
					c = pattern[i];
					tokens.Add(new REToken('\\', c));
					continue;
				}
				tokens.Add(new REToken('\0', c));
			}
			return ParseRange(tokens, 0, tokens.Count, null);
		}
		
		private static RENode ParseRange(List<REToken> tokens, int index0, int index1, RENode next)
		{
			RENode result = null;
			for (int i = index1; i-- > index0;)
			{
				REToken token = tokens[i];
				if (token.type == '\0')
				{
					result = new REChar(token.c, result);
				}
				else if (token.type == '\\')
				{
					if (token.c == '|')
					{
						return new REAlternate(ParseRange(tokens, index0, i, next), result, next);
					}
					if (token.c == ')')
					{
						int bracketIndex = -1;
						int depth = 1;
						for (int j = i; j-- > index0;)
						{
							token = tokens[j];
							if (token.type == '\\')
							{
								if (token.c == ')')
								{
									depth++;
								}
								else if (token.c == '(')
								{
									depth--;
								}
								if (depth == 0)
								{
									bracketIndex = j;
									break;
								}
							}
						}
						if (bracketIndex == -1)
						{
							return null;
						}
						result = ParseRange(tokens, bracketIndex + 1, i, result);
						i = bracketIndex;
					}
					else if (token.c == '(')
					{
						return null;
					}
				}
			}
			return result;
		}
	}
}