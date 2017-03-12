using System;
using System.Collections.Generic;

namespace MulticaretEditor
{
	public class REParser
	{
		public REParser()
		{
		}
		
		private List<REToken> _tokens = new List<REToken>();
		private Stack<char> _operators = new Stack<char>();
		private Stack<RE.RENode> _operands = new Stack<RE.RENode>();
		private Stack<RE.RENode> _operandEnds = new Stack<RE.RENode>();
		
		public RE.RENode Parse(string pattern)
		{
			_tokens.Clear();
			_operators.Clear();
			_operands.Clear();
			_operandEnds.Clear();
			int bracket = -1;
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
					if (bracket == -1)
					{
						if (c == '{')
						{
							int matchedIndex = QuantifierStarted(pattern, i + 1);
							if (matchedIndex != -1)
							{
								_tokens.Add(new REToken('#', '{'));
								for (int j = i + 1; j <= matchedIndex - 1; j++)
								{
									_tokens.Add(new REToken('\0', pattern[j]));
								}
								_tokens.Add(new REToken('#', '}'));
								i = matchedIndex;
							}
							continue;
						}
					}
					_tokens.Add(new REToken('\\', c));
					continue;
				}
				if (bracket != -1)
				{
					if (c == ']' && bracket != i - 1)
					{
						bracket = -1;
						_tokens.Add(new REToken('#', ']'));
						continue;
					}
				}
				else
				{
					if (c == '[')
					{
						bracket = i;
						_tokens.Add(new REToken('#', '['));
						continue;
					}
				}
				_tokens.Add(new REToken('\0', c));
			}
			int index;
			return RemoveSuperfluousNodes(ParseSequence(_tokens.Count - 1, null, out index));
		}
		
		private int QuantifierStarted(string pattern, int index)
		{
			int state = 0;
			for (; index < pattern.Length; index++)
			{
				char c = pattern[index];
				if (state == 0)
				{
					if (c == '-' || c >= '0' && c <= '9')
					{
						state = 1;
					}
					else if (c == ',')
					{
						state = 2;
					}
					else
					{
						return -1;
					}
				}
				else if (state == 1)
				{
					if (c == ',')
					{
						state = 2;
					}
					else if (c == '}')
					{
						return index;
					}
					else if (!(c >= '0' && c <= '9'))
					{
						return -1;
					}
				}
				else if (state == 2)
				{
					if (c == '}')
					{
						return index;
					}
					else if (!(c >= '0' && c <= '9'))
					{
						return -1;
					}
				}
			}
			return -1;
		}
		
		private RE.RENode ParseSequence(int index, RE.RENode next, out int nextIndex)
		{
			RE.RENode resultEnd = null;
			RE.RENode result = next;
			while (index >= 0)
			{
				REToken token = _tokens[index];
				if (token.type == '\\')
				{
					if (token.c == '|')
					{
						if (_operators.Count > 0)
						{
							char o = _operators.Peek();
							if (o == '|')
							{
								_operators.Pop();
								result = BuildAlternate(result, resultEnd, _operands.Pop(), _operandEnds.Pop(), next);
								resultEnd = resultEnd ?? result;
							}
						}
						_operators.Push('|');
						_operands.Push(result);
						_operandEnds.Push(resultEnd);
						result = null;
						resultEnd = null;
						index--;
						continue;
					}
					if (token.c == '(')
					{
						index--;
						break;
					}
					else if (token.c == ')')
					{
						index--;
						result = ParseSequence(index, result, out index);
						resultEnd = resultEnd ?? result;
						continue;
					}
				}
				else if (token.type == '#')
				{
					if (token.c == ']')
					{
						index--;
						result = ParseRange(index, result, out index);
						resultEnd = resultEnd ?? result;
						continue;
					}
				}
				result = ParsePart(index, result, out index);
				resultEnd = resultEnd ?? result;
			}
			if (_operators.Count > 0)
			{
				char o = _operators.Peek();
				if (o == '|')
				{
					_operators.Pop();
					result = BuildAlternate(result, resultEnd, _operands.Pop(), _operandEnds.Pop(), next);
					resultEnd = resultEnd ?? result;
				}
			}
			nextIndex = index;
			return result;
		}
		
		private RE.RENode ParsePart(int index, RE.RENode next, out int nextIndex)
		{
			nextIndex = index - 1;
			REToken token = _tokens[index];
			if (token.type == '\0')
			{
				if (token.c == '.')
				{
					RE.RENode node = new RE.REDot();
					node.next0 = next;
					return node;
				}
				if (token.c == '*')
				{
					index--;
					if (index >= 0)
					{
						RE.RENode targetEnd = new RE.REEmpty();
						RE.RENode target = ParsePart(index, targetEnd, out nextIndex);
						RE.RENode result = BuildRepetition(target, targetEnd, next);
						return result;
					}
				}
				{
					RE.REChar node = new RE.REChar(token.c);
					node.next0 = next;
					return node;
				}
			}
			if (token.type == '#')
			{
				if (token.c == ']')
				{
					index--;
					RE.RENode result = ParseRange(index, next, out nextIndex);
					return result;
				}
				if (token.c == '}')
				{
					index--;
					if (index > 0 && _tokens[index].type == '\0' && _tokens[index].c == '-' &&
						_tokens[index - 1].type == '#' && _tokens[index - 1].c == '{')
					{
						index -= 2;
						RE.RENode targetEnd = new RE.REEmpty();
						RE.RENode target = ParsePart(index, targetEnd, out nextIndex);
						RE.RENode result = BuildNonGreedly(target, targetEnd, next);
						return result;
					} 
				}
			}
			if (token.type == '\\')
			{
				if (token.c == '.' || token.c == '*')
				{
					RE.REChar node = new RE.REChar(token.c);
					node.next0 = next;
					return node;
				}
				if (token.c == '+')
				{
					index--;
					if (index >= 0)
					{
						RE.RENode targetEnd = new RE.REEmpty();
						RE.RENode target = ParsePart(index, targetEnd, out nextIndex);
						RE.RENode result = BuildOneOrMore(target, targetEnd, next);
						return result;
					}
				}
				if (token.c == '=')
				{
					index--;
					if (index >= 0)
					{
						RE.RENode targetEnd = new RE.REEmpty();
						RE.RENode target = ParsePart(index, targetEnd, out nextIndex);
						RE.RENode result = BuildOneOrNone(target, targetEnd, next);
						return result;
					}
				}
				if (token.c == ')')
				{
					index--;
					if (index < 0)
					{
						throw new FormatException("')' at start");
					}
					RE.RENode result = ParseSequence(index, next, out nextIndex);
					return result;
				}
				if (token.c == 'w')
				{
					RE.RE_W node = new RE.RE_W(false);
					node.next0 = next;
					return node;
				}
				if (token.c == 'W')
				{
					RE.RE_W node = new RE.RE_W(true);
					node.next0 = next;
					return node;
				}
				if (token.c == 's')
				{
					RE.RE_S node = new RE.RE_S(false);
					node.next0 = next;
					return node;
				}
				if (token.c == 'S')
				{
					RE.RE_S node = new RE.RE_S(true);
					node.next0 = next;
					return node;
				}
				if (token.c == 'a')
				{
					RE.RE_A node = new RE.RE_A(false);
					node.next0 = next;
					return node;
				}
				if (token.c == 'A')
				{
					RE.RE_A node = new RE.RE_A(true);
					node.next0 = next;
					return node;
				}
				if (token.c == 'd')
				{
					RE.RE_D node = new RE.RE_D(false);
					node.next0 = next;
					return node;
				}
				if (token.c == 'D')
				{
					RE.RE_D node = new RE.RE_D(true);
					node.next0 = next;
					return node;
				}
				if (token.c == 'h')
				{
					RE.RE_H node = new RE.RE_H(false);
					node.next0 = next;
					return node;
				}
				if (token.c == 'H')
				{
					RE.RE_H node = new RE.RE_H(true);
					node.next0 = next;
					return node;
				}
				if (token.c == 'l')
				{
					RE.RE_L node = new RE.RE_L(false);
					node.next0 = next;
					return node;
				}
				if (token.c == 'L')
				{
					RE.RE_L node = new RE.RE_L(true);
					node.next0 = next;
					return node;
				}
				if (token.c == 'o')
				{
					RE.RE_O node = new RE.RE_O(false);
					node.next0 = next;
					return node;
				}
				if (token.c == 'O')
				{
					RE.RE_O node = new RE.RE_O(true);
					node.next0 = next;
					return node;
				}
				if (token.c == 'p')
				{
					RE.RE_P node = new RE.RE_P(false);
					node.next0 = next;
					return node;
				}
				if (token.c == 'P')
				{
					RE.RE_P node = new RE.RE_P(true);
					node.next0 = next;
					return node;
				}
				if (token.c == 'u')
				{
					RE.RE_U node = new RE.RE_U(false);
					node.next0 = next;
					return node;
				}
				if (token.c == 'U')
				{
					RE.RE_U node = new RE.RE_U(true);
					node.next0 = next;
					return node;
				}
				if (token.c == 'x')
				{
					RE.RE_X node = new RE.RE_X(false);
					node.next0 = next;
					return node;
				}
				if (token.c == 'X')
				{
					RE.RE_X node = new RE.RE_X(true);
					node.next0 = next;
					return node;
				}
			}
			throw new FormatException("Can't parse part at " + index);
		}
		
		private RE.RENode BuildAlternate(
			RE.RENode branch0, RE.RENode branch0End,
			RE.RENode branch1, RE.RENode branch1End,
			RE.RENode next)
		{
			RE.RENode start = new RE.REEmpty();
			start.next0 = branch0;
			start.next1 = branch1;
			branch0End.next0 = next;
			branch1End.next0 = next;
			return start;
		}
		
		private RE.RENode BuildRepetition(
			RE.RENode body, RE.RENode bodyEnd,
			RE.RENode next)
		{
			RE.RENode start = new RE.REEmpty();
			start.next0 = body;
			bodyEnd.next0 = start;
			bodyEnd.next1 = start;
			start.next1 = next;
			return start;
		}
		
		private RE.RENode BuildOneOrMore(
			RE.RENode body, RE.RENode bodyEnd,
			RE.RENode next)
		{
			RE.RENode end = new RE.REEmpty();
			bodyEnd.next0 = end;
			bodyEnd.next1 = end;
			end.next0 = body;
			end.next1 = next;
			return body;
		}
		
		private RE.RENode BuildOneOrNone(
			RE.RENode body, RE.RENode bodyEnd,
			RE.RENode next)
		{
			RE.RENode start = new RE.REEmpty();
			bodyEnd.next0 = next;
			bodyEnd.next1 = next;
			start.next0 = body;
			start.next1 = next;
			return start;
		}
		
		private RE.RENode BuildNonGreedly(
			RE.RENode body, RE.RENode bodyEnd,
			RE.RENode next)
		{
			RE.RENode a = new RE.REEmpty();
			RE.RENode b = new RE.REEmpty();
			a.next0 = body;
			bodyEnd.next0 = b;
			bodyEnd.next1 = b;
			a.next1 = next;
			b.next0 = next;
			b.next1 = a;
			b.next1Low = true;
			return a;
		}
		
		private RE.RENode RemoveSuperfluousNodes(RE.RENode root)
		{
			if (root == null)
			{
				return root;
			}
			while (root.next0 == root.next1 && root.next0 != null)
			{
				root = root.next0;
			}
			Dictionary<RE.RENode, bool> nodes = new Dictionary<RE.RENode, bool>();
			Stack<RE.RENode> stack = new Stack<RE.RENode>();
			stack.Push(root);
			while (stack.Count > 0)
			{
				RE.RENode node = stack.Pop();
				nodes[node] = true;
				if (node.next0 != null)
				{
					if (node.next0.emptyEntry && node.next0.next0 == node.next0.next1)
					{
						node.next0 = node.next0.next0;
					}
					if (node.next0 != null && !nodes.ContainsKey(node.next0))
					{
						stack.Push(node.next0);
					}
				}
				if (node.next1 != null)
				{
					if (node.next1.emptyEntry && node.next1.next0 == node.next1.next1)
					{
						node.next1 = node.next1.next0;
					}
					if (node.next1 != null && !nodes.ContainsKey(node.next1))
					{
						stack.Push(node.next1);
					}
				}
			}
			return root;
		}
		
		private RE.RENode ParseRange(int index, RE.RENode next, out int nextIndex)
		{
			Stack<char> chars = new Stack<char>();
			RE.REInterval interval = null;
			bool negative = false;
			while (index >= 0)
			{
				REToken token = _tokens[index];
				index--;
				if (token.type == '\0')
				{
					if (token.c == '^' && index >= 0 && _tokens[index].type == '#' && _tokens[index].c == '[')
					{
						index--;
						negative = true;
						break;
					}
					if (token.c == '-' && chars.Count > 0 && index >= 0 && (
						_tokens[index].type != '#' && !(_tokens[index].type == '\0' && _tokens[index].c == '-') &&
						(_tokens[index].c != '^' || index <= 0 || _tokens[index - 1].type != '#')
					))
					{
						interval = new RE.REInterval(_tokens[index].c, chars.Pop(), interval);
						index--;
						continue;
					}
				}
				if (token.type == '#')
				{
					if (token.c == '[')
					{
						break;
					}
				}
				chars.Push(token.c);
			}
			nextIndex = index;
			RE.RENode range = new RE.RERange(chars.ToArray(), interval);
			if (negative)
			{
				range = new RE.RENot(range);
			}
			range.next0 = next;
			return range;
		}
	}
}