using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;

namespace MulticaretEditor
{
	public class Controller
	{
		private readonly LineArray lines;
		private readonly List<Selection> selections;

		public readonly History history;

		public Controller(LineArray lines)
		{
			this.lines = lines;
			this.selections = lines.selections;
			history = new History();
			ResetCommandsBatching();
		}

		public bool isReadonly;
		public bool needDispatchChange;
		public MacrosExecutor macrosExecutor;

		public LineArray Lines { get { return lines; } }

		public void InitText(string text)
		{
			lines.SetText(text);
			history.Reset();
			history.MarkAsSaved();
		}
		
		private void DoAfterMove()
		{
			ResetCommandsBatching();
		}

		public bool MoveRight(bool shift)
		{
			bool result = MoveRight(lines, shift);
			DoAfterMove();
			return result;
		}

		public bool MoveLeft(bool shift)
		{
			bool result = MoveLeft(lines, shift);
			DoAfterMove();
			return result;
		}

		public static bool MoveRight(LineArray lines, bool shift)
		{
			bool result = false;
			foreach (Selection selection in lines.selections)
			{
				if (!shift && !selection.Empty)
				{
					if (selection.caret != selection.anchor)
						result = true;
					int index = selection.Right;
					selection.caret = index;
					selection.anchor = index;
				}
				else
				{
					if (selection.caret < lines.charsCount - 1 && lines.GetText(selection.caret, 2) == "\r\n")
					{
						selection.caret += 2;
						result = true;
					}
					else if (selection.caret < lines.charsCount)
					{
						selection.caret++;
						result = true;
					}
					if (!shift && selection.anchor != selection.caret)
					{
						selection.anchor = selection.caret;
						result = true;
					}
				}
				Place place = lines.PlaceOf(selection.caret);
				lines.SetPreferredPos(selection, place);
			}
			return result;
		}

		public static bool MoveLeft(LineArray lines, bool shift)
		{
			bool result = false;
			foreach (Selection selection in lines.selections)
			{
				if (!shift && !selection.Empty)
				{
					if (selection.caret != selection.anchor)
						result = true;
					int index = selection.Left;
					selection.caret = index;
					selection.anchor = index;
				}
				else
				{
					if (selection.caret > 1 && lines.GetText(selection.caret - 2, 2) == "\r\n")
					{
						selection.caret -= 2;
						result = true;
					}
					else if (selection.caret > 0)
					{
						selection.caret--;
						result = true;
					}
					if (!shift && selection.anchor != selection.caret)
					{
						selection.anchor = selection.caret;
						result = true;
					}
				}
				Place place = lines.PlaceOf(selection.caret);
				lines.SetPreferredPos(selection, place);
			}
			return result;
		}

		public bool MoveUp(bool shift)
		{
			bool result = false;
			if (lines.wordWrap && selections.Count == 1)
			{
				Selection selection = lines.LastSelection;
				Place place = lines.PlaceOf(selection.caret);
				Line line = lines[place.iLine];
				Pos pos = line.WWPosOfIndex(place.iChar);
				Place newPlace = place;
				if (pos.iy > 0)
				{
					newPlace = new Place(line.WWNormalIndexOfPos(selection.wwPreferredPos, pos.iy - 1), place.iLine);
					result = true;
				}
				else if (place.iLine > 0)
				{
					line = lines[place.iLine - 1];
					newPlace = new Place(line.WWNormalIndexOfPos(selection.wwPreferredPos, line.cutOffs.count), place.iLine - 1);
					result = true;
				}
				selection.caret = lines.IndexOf(newPlace);
				if (!shift && selection.anchor != selection.caret)
				{
					selection.anchor = selection.caret;
					result = true;
				}
			}
			else
			{
				foreach (Selection selection in selections)
				{
					Place place = lines.PlaceOf(selection.caret);
					if (place.iLine > 0)
					{
						Line line = lines[place.iLine - 1];
						place = new Place(Math.Min(line.chars.Count, line.NormalIndexOfPos(selection.preferredPos)), place.iLine - 1);
						result = true;
					}
					selection.caret = lines.IndexOf(place);
					if (!shift && selection.anchor != selection.caret)
					{
						selection.anchor = selection.caret;
						result = true;
					}
				}
			}
			DoAfterMove();
			return result;
		}

		public bool MoveDown(bool shift)
		{
			bool result = false;
			if (lines.wordWrap && selections.Count == 1)
			{
				Selection selection = lines.LastSelection;
				Place place = lines.PlaceOf(selection.caret);
				Line line = lines[place.iLine];
				Pos pos = line.WWPosOfIndex(place.iChar);
				Place newPlace = place;
				if (pos.iy < line.cutOffs.count)
				{
					newPlace = new Place(line.WWNormalIndexOfPos(selection.wwPreferredPos, pos.iy + 1), place.iLine);
					result = true;
				}
				else if (place.iLine < lines.LinesCount - 1)
				{
					line = lines[place.iLine + 1];
					newPlace = new Place(line.WWNormalIndexOfPos(selection.wwPreferredPos, 0), place.iLine + 1);
					result = true;
				}
				selection.caret = lines.IndexOf(newPlace);
				if (!shift && selection.anchor != selection.caret)
				{
					selection.anchor = selection.caret;
					result = true;
				}
			}
			else
			{
				foreach (Selection selection in selections)
				{
					Place place = lines.PlaceOf(selection.caret);
					if (place.iLine < lines.LinesCount - 1)
					{
						Line line = lines[place.iLine + 1];
						place = new Place(Math.Min(line.chars.Count, line.NormalIndexOfPos(selection.preferredPos)), place.iLine + 1);
						result = true;
					}
					selection.caret = lines.IndexOf(place);
					if (!shift && selection.anchor != selection.caret)
					{
						selection.anchor = selection.caret;
						result = true;
					}
				}
			}
			DoAfterMove();
			return result;
		}

		public void MoveEnd(bool shift)
		{
			if (lines.wordWrap && selections.Count == 1)
			{
				Selection selection = lines.LastSelection;
				Place place = lines.PlaceOf(selection.caret);
				Line line = lines[place.iLine];
				if (line.cutOffs.count > 0)
				{
					Pos pos = line.WWPosOfIndex(place.iChar);
					if (pos.iy < line.cutOffs.count)
					{
						int sublineStart = line.cutOffs.buffer[pos.iy].iChar;
						if (place.iChar < sublineStart - 1)
						{
							Place newPlace = new Place(sublineStart - 1, place.iLine);
							selection.caret = lines.IndexOf(newPlace);
							selection.SetEmptyIfNotShift(shift);
							lines.SetPreferredPos(selection, newPlace);
							DoAfterMove();
							return;
						}
					}
				}
			}
			foreach (Selection selection in selections)
			{
				Place caret = lines.PlaceOf(selection.caret);
				caret.iChar = lines[caret.iLine].NormalCount;
				selection.caret = lines.IndexOf(caret);
				selection.SetEmptyIfNotShift(shift);
				lines.SetPreferredPos(selection, caret);
			}
			DoAfterMove();
		}

		public void MoveHome(bool shift)
		{
			if (lines.wordWrap && selections.Count == 1)
			{
				Selection selection = lines.LastSelection;
				Place place = lines.PlaceOf(selection.caret);
				Line line = lines[place.iLine];
				if (line.cutOffs.count > 0)
				{
					Pos pos = line.WWPosOfIndex(place.iChar);
					if (pos.iy > 0)
					{
						int sublineStart = line.cutOffs.buffer[pos.iy - 1].iChar;
						if (place.iChar - sublineStart > 0)
						{
							Place newPlace = new Place(sublineStart, place.iLine);
							selection.caret = lines.IndexOf(newPlace);
							selection.SetEmptyIfNotShift(shift);
							lines.SetPreferredPos(selection, newPlace);
							DoAfterMove();
							return;
						}
					}
				}
			}
			foreach (Selection selection in selections)
			{
				Place caret = lines.PlaceOf(selection.caret);
				Line line = lines[caret.iLine];
				int charsCount = line.NormalCount;
				int minIChar = 0;
				while (minIChar < charsCount && char.IsWhiteSpace(line.chars[minIChar].c))
				{
					minIChar++;
				}
				caret.iChar = caret.iChar > minIChar ? minIChar : 0;
				selection.caret = lines.IndexOf(caret);
				selection.SetEmptyIfNotShift(shift);
				lines.SetPreferredPos(selection, caret);
			}
			DoAfterMove();
		}

		public void DocumentStart(bool shift)
		{
			foreach (Selection selection in selections)
			{
				selection.caret = 0;
				selection.SetEmptyIfNotShift(shift);
			}
			lines.JoinSelections();
			lines.LastSelection.preferredPos = 0;
			DoAfterMove();
		}

		public void DocumentEnd(bool shift)
		{
			foreach (Selection selection in selections)
			{
				selection.caret = lines.charsCount;
				selection.SetEmptyIfNotShift(shift);
			}
			lines.JoinSelections();
			Place place = lines.PlaceOf(lines.charsCount);
			lines.SetPreferredPos(lines.LastSelection, place);
			DoAfterMove();
		}

		public void PutCursor(Pos pos, bool moving)
		{
			PutCursor(lines.PlaceOf(pos), moving);
		}

		public void PutCursor(Place place, bool moving)
		{
			Selection selection = selections[selections.Count - 1];
			Place caret = lines.Normalize(place);
			selection.caret = lines.IndexOf(caret);
			if (!moving)
				selection.anchor = selection.caret;
			Line line = lines[caret.iLine];
			lines.SetPreferredPos(selection, caret);
			DoAfterMove();
		}

		public enum CharType
		{
			Identifier,
			Space,
			Punctuation,
			Special
		}

		private static CharType GetCharType(char c)
        {
			if (c == ' ' || c == '\t')
				return CharType.Space;
			if (c == '\r' || c == '\n' || c == '\0')
				return CharType.Special;
			return char.IsLetterOrDigit(c) || c == '_' ? CharType.Identifier : CharType.Punctuation;
        }
        
        public static bool IsSpaceOrNewLine(char c)
        {
			return c == ' ' || c == '\t' || c == '\r' || c == '\n';
        }

		public void MoveWordRight(bool shift)
		{
			MoveWordRight(lines, shift);
			DoAfterMove();
		}

		public void MoveWordLeft(bool shift)
		{
			MoveWordLeft(lines, shift);
			DoAfterMove();
		}

		public static void MoveWordRight(LineArray lines, bool shift)
		{
			foreach (Selection selection in lines.selections)
			{
				PlaceIterator iterator = lines.GetCharIterator(selection.caret);

				bool wasSpace = false;
				while (GetCharType(iterator.RightChar) == CharType.Space)
				{
					wasSpace = true;
					if (!iterator.MoveRightWithRN())
						break;
				}
				bool wasIdentifier = false;
				CharType type = GetCharType(iterator.RightChar);
				if (type == CharType.Identifier || type == CharType.Punctuation)
				{
					CharType typeI = type;
					while (typeI == type)
					{
						wasIdentifier = true;
						if (!iterator.MoveRightWithRN())
							break;
						typeI = GetCharType(iterator.RightChar);
					}
				}
				if (!wasIdentifier && (!wasSpace || iterator.RightChar != '\n' && iterator.RightChar != '\r'))
					iterator.MoveRightWithRN();

				selection.caret = iterator.Position;
				if (!shift)
					selection.anchor = iterator.Position;
				lines.SetPreferredPos(selection, iterator.Place);
			}
		}

		public static void MoveWordLeft(LineArray lines, bool shift)
		{
			foreach (Selection selection in lines.selections)
			{
				PlaceIterator iterator = lines.GetCharIterator(selection.caret);

				bool wasSpace = false;
				while (GetCharType(iterator.LeftChar) == CharType.Space)
				{
					wasSpace = true;
					if (!iterator.MoveLeftWithRN())
						break;
				}
				bool wasIdentifier = false;
				CharType type = GetCharType(iterator.LeftChar);
				if (type == CharType.Identifier || type == CharType.Punctuation)
				{
					CharType typeI = type;
					while (typeI == type)
					{
						wasIdentifier = true;
						if (!iterator.MoveLeftWithRN())
							break;
						typeI = GetCharType(iterator.LeftChar);
					}
				}
				if (!wasIdentifier && (!wasSpace || iterator.LeftChar != '\n' && iterator.LeftChar != '\r'))
					iterator.MoveLeftWithRN();

				selection.caret = iterator.Position;
				if (!shift)
					selection.anchor = iterator.Position;
				lines.SetPreferredPos(selection, iterator.Place);
			}
		}

		public void PutCursorDown()
		{
			if (lines.selections.Count > 1)
			{
				if (lines.selections[lines.selections.Count - 2].caret > lines.LastSelection.caret)
				{
					lines.selections.RemoveAt(lines.selections.Count - 1);
					DoAfterMove();
					return;
				}
			}
			int preferredPos = lines.LastSelection.preferredPos;
			int wwPreferredPos = lines.LastSelection.wwPreferredPos;
			Pos pos = lines.PosOf(lines.PlaceOf(lines.LastSelection.caret));
			if (pos.iy < lines.LinesCount - 1)
			{
				pos.ix = preferredPos;
				pos.iy++;
				PutNewCursor(pos);
				lines.LastSelection.preferredPos = preferredPos;
				lines.LastSelection.wwPreferredPos = wwPreferredPos;
			}
			DoAfterMove();
		}

		public void PutCursorUp()
		{
			if (lines.selections.Count > 1)
			{
				if (lines.selections[lines.selections.Count - 2].caret < lines.LastSelection.caret)
				{
					lines.selections.RemoveAt(lines.selections.Count - 1);
					DoAfterMove();
					return;
				}
			}
			int preferredPos = lines.LastSelection.preferredPos;
			int wwPreferredPos = lines.LastSelection.wwPreferredPos;
			Pos pos = lines.PosOf(lines.PlaceOf(lines.LastSelection.caret));
			if (pos.iy > 0)
			{
				pos.ix = preferredPos;
				pos.iy--;
				PutNewCursor(pos);
				lines.LastSelection.preferredPos = preferredPos;
				lines.LastSelection.wwPreferredPos = wwPreferredPos;
			}
			DoAfterMove();
		}

		public void PutNewCursor(Pos pos)
		{
			PutNewCursor(lines.PlaceOf(pos));
		}

		public void PutNewCursor(Place place)
		{
			int caret = lines.IndexOf(place);
			bool contains = false;
			foreach (Selection selection in selections)
			{
				if (selection.Contains(caret))
				{
					contains = true;
					break;
				}
			}
			if (!contains)
			{
				lines.selections.Add(new Selection());
			}
			else
			{
				ClearMinorSelections();
			}
			PutCursor(place, false);
		}

		public bool ClearMinorSelections()
		{
			if (lines.selections.Count > 1)
			{
				lines.selections.RemoveRange(1, lines.selections.Count - 1);
				return true;
			}
			return false;
		}

		public bool ClearFirstMinorSelections()
		{
			if (lines.selections.Count > 1)
			{
				lines.selections.RemoveRange(0, lines.selections.Count - 1);
				return true;
			}
			return false;
		}

		public void SelectAll()
		{
			ClearMinorSelections();
			Selection selection = lines.LastSelection;
			selection.anchor = lines.charsCount;
			selection.caret = 0;
			DoAfterMove();
		}

		public void SelectAllToEnd()
		{
			ClearMinorSelections();
			Selection selection = lines.LastSelection;
			selection.anchor = 0;
			selection.caret = lines.charsCount;
			DoAfterMove();
		}

		private CommandType lastCommandType;
		private long lastTime;

		private void ResetCommandsBatching()
		{
			lastCommandType = CommandType.None;
			lastTime = 0;
		}
		
		public long debugNowMilliseconds = -1;
		
		public long GetNowMilliseconds()
		{
			return debugNowMilliseconds != -1 ?
				debugNowMilliseconds :
				(long)(new TimeSpan(DateTime.UtcNow.Ticks).TotalMilliseconds);
		}

		private bool Execute(Command command)
		{
			if (isReadonly && command.type.changesText)
				return false;
			command.lines = lines;
			command.selections = selections;
			long time = GetNowMilliseconds();
			if (command.type != lastCommandType && !command.type.helped &&
				!(lastCommandType != null && lastCommandType.helped))
			{
				if (history.LastCommand != null)
					history.LastCommand.marked = true;
				lastCommandType = command.type;
				lastTime = time;
			}
			else if (time - lastTime > 1000)
			{
				if (history.LastCommand != null)
					history.LastCommand.marked = true;
				lastCommandType = command.type;
				lastTime = time;
			}
			bool result = command.Init();
			if (result)
				history.ExecuteInited(command);
			needDispatchChange = true;
			return result;
		}

		public void Undo()
		{
			ResetCommandsBatching();
			bool changed = false;
			while (true)
			{
				if (history.Undo())
					changed = true;
				if (history.LastCommand == null || history.LastCommand.marked)
					break;
			}
			if (changed)
				needDispatchChange = true;
		}

		public void Redo()
		{
			ResetCommandsBatching();
			while (true)
			{
				if (history.NextCommand == null)
					break;
				if (history.NextCommand.marked)
				{
					history.Redo();
					break;
				}
				history.Redo();
			}
		}

		public void Backspace()
		{
			Execute(new BackspaceCommand());
		}

		public void Delete()
		{
			Execute(new DeleteCommand());
		}

		public void InsertText(string text)
		{
			if (lines.spacesInsteadTabs && text == "\t")
				text = new string(' ', lines.tabSize);
			Execute(new InsertTextCommand(text, null, true));
		}

		public void InsertLineBreak()
		{
			lines.JoinSelections();
			string[] texts = new string[selections.Count];
			for (int i = 0; i < selections.Count; i++)
			{
				Selection selection = selections[i];
				Place place = lines.PlaceOf(selection.Left);
				Line line = lines[place.iLine];
				texts[i] = lines.lineBreak + GetLineBreakFirstSpaces(line, place.iChar);
			}
			Execute(new InsertTextCommand(null, texts, true));
		}

		private static string GetLineBreakFirstSpaces(Line line, int iChar)
		{
			int count = line.chars.Count;
			int spacesCount = 0;
			for (int i = 0; i < count; i++)
			{
				char c = line.chars[i].c;
				if (c != '\t' && c != ' ')
					break;
				spacesCount++;
			}
			if (iChar >= spacesCount)
			{
				StringBuilder builder = new StringBuilder();
				for (int i = 0; i < count; i++)
				{
					char c = line.chars[i].c;
					if (c != '\t' && c != ' ')
						break;
					builder.Append(c);
				}
				return builder.ToString();
			}
			return "";
		}

		public void Copy()
		{
			Execute(new CopyCommand('*'));
		}

		public void Cut()
		{
			Copy();
			EraseSelection();
		}

		public void EraseSelection()
		{
			Execute(new EraseSelectionCommand());
		}

		public void Paste()
		{
			Execute(new PasteCommand('*'));
		}

		public bool ShiftLeft()
		{
			return Execute(new ShiftCommand(true));
		}

		public bool ShiftRight()
		{
			return Execute(new ShiftCommand(false));
		}

		public bool RemoveWordLeft()
		{
			return Execute(new RemoveWordCommand(true));
		}

		public bool RemoveWordRight()
		{
			return Execute(new RemoveWordCommand(false));
		}

		public bool MoveLineUp()
		{
			return Execute(new MoveLineCommand(true));
		}

		public bool MoveLineDown()
		{
			return Execute(new MoveLineCommand(false));
		}
		
		public string GetWord(Place place)
		{
			int position;
			int count;
			GetWordSelection(place, out position, out count);
			return lines.GetText(position, count);
		}
		
		public string GetLeftWord(Place place)
		{
			Line line = lines[place.iLine];
			int normalCount = line.NormalCount;
			int left;
			if (normalCount > 0)
			{
				int iChar = place.iChar;
				if (iChar > normalCount)
					iChar = normalCount;
				left = iChar;
				while (left > 0 && GetCharType(line.chars[left - 1].c) == CharType.Identifier)
				{
					left--;
				}
				if (left < iChar)
				{
					StringBuilder builder = new StringBuilder();
					for (int i = left; i < iChar; i++)
					{
						builder.Append(line[i].c);
					}
					return builder.ToString();
				}
			}
			return "";
		}
		
		private void GetWordSelection(Place place, out int position, out int count)
		{
			Line line = lines[place.iLine];
			int normalCount = line.NormalCount;
			int left;
			int right;
			if (normalCount > 0)
			{
				int iChar = place.iChar;
				if (iChar >= normalCount)
					iChar = normalCount - 1;
				CharType charType = GetCharType(line.chars[iChar].c);
				left = iChar;
				while (left > 0 && GetCharType(line.chars[left - 1].c) == charType)
				{
					left--;
				}
				right = iChar + 1;
				while (right < normalCount && GetCharType(line.chars[right].c) == charType)
				{
					right++;
				}
			}
			else
			{
				left = 0;
				right = 0;
			}
			position = lines.IndexOf(new Place(left, place.iLine));
			count = right - left;
		}

		public void SelectWordAtPlace(Place place, bool newSelection)
		{
			int position;
			int count;
			GetWordSelection(place, out position, out count);
			if (newSelection)
			{
				selections.Add(new Selection());
			}
			else
			{
				ClearMinorSelections();
			}
			Selection selection = lines.LastSelection;
			selection.anchor = position;
			selection.caret = position + count;
			lines.JoinSelections();
		}

		public void SelectNextText()
		{
			if (lines.LastSelection.Empty)
			{
				foreach (Selection selection in selections)
				{
					if (selection.Empty)
					{
						Place place = lines.PlaceOf(selection.caret);
						int position;
						int count;
						GetWordSelection(place, out position, out count);
						selection.anchor = position;
						selection.caret = position + count;
						Place caret = lines.PlaceOf(selection.caret);
						lines.SetPreferredPos(selection, caret);
					}
				}
			}
			else
			{
				Selection lastSelection = lines.LastSelection;
				string text = lines.GetText(lastSelection.Left, lastSelection.Count);
				int position = lines.IndexOf(text, lastSelection.Right);
				if (position == -1)
					position = lines.IndexOf(text, 0);
				while (true)
				{
					if (position == -1 || !lines.IntersectSelections(position, position + text.Length))
						break;
					int newPosition = lines.IndexOf(text, position + text.Length);
					if (newPosition == position)
						break;
					position = newPosition;
				}
				if (position != -1)
				{
					Selection selection = new Selection();
					selection.anchor = position;
					selection.caret = position + text.Length;
					Place caret = lines.PlaceOf(selection.caret);
					lines.SetPreferredPos(selection, caret);
					selections.Add(selection);
				}
			}
		}
		
		public void SelectAllMatches()
		{
			Selection lastSelection = lines.LastSelection;
			if (lastSelection.Empty)
			{
				SelectNextText();
			}
			if (lastSelection.Empty)
			{
				return;
			}
			string all = lines.GetText();
			string text = all.Substring(lastSelection.Left, lastSelection.Count);
			int start = 0;
			bool first = true;
			while (true)
			{
				int length = text.Length;
				int index =  all.IndexOf(text, start);
				if (index == -1)
				{
					break;
				}
				if (first)
				{
					first = false;
										
					ClearMinorSelections();
					PutCursor(lines.PlaceOf(index), false);
					PutCursor(lines.PlaceOf(index + length), true);
				}
				else
				{
					PutNewCursor(lines.PlaceOf(index));
					PutCursor(lines.PlaceOf(index + length), true);
				}
				start = index + length;
			}
		}

		public void ChangeCase(bool upper)
		{
			lines.JoinSelections();
			string[] texts = new string[selections.Count];
			bool needChange = false;
			for (int i = 0; i < selections.Count; i++)
			{
				Selection selection = selections[i];
				string text = lines.GetText(selection.Left, selection.Count);
				if (text.Length > 0)
					needChange = true;
				texts[i] = upper ? text.ToUpperInvariant() : text.ToLowerInvariant();
			}
			if (needChange)
				Execute(new InsertTextCommand(null, texts, false));
		}

		public bool AllSelectionsEmpty { get { return lines.AllSelectionsEmpty; } }

		public void ScrollPage(bool isUp, bool withSelection)
		{
			lines.scroller.ScrollPage(isUp, this, withSelection);
		}

		public void ScrollRelative(int x, int y)
		{
			lines.scroller.ScrollRelative(x, y);
		}

		public void NeedScrollToCaret()
		{
			lines.scroller.needScrollToCaret = true;
		}

		public Place SoftNormalizedPlaceOf(int index)
		{
			return lines.SoftNormalizedPlaceOf(index);
		}

		public int SelectionsCount { get { return selections.Count; } }
		public IEnumerable<Selection> Selections { get { return selections; } }
		public Selection LastSelection { get { return lines.LastSelection; } }

		public void RemoveSelections(List<Selection> selections)
		{
			foreach (Selection selection in selections)
			{
				lines.selections.Remove(selection);
			}
			lines.JoinSelections();
		}

		public void JoinSelections()
		{
			lines.JoinSelections();
		}

		public void SetStyleRange(StyleRange range)
		{
			lines.SetStyleRange(range);
		}

		public void SetStyleRanges(List<StyleRange> ranges)
		{
			foreach (StyleRange range in ranges)
			{
				lines.SetStyleRange(range);
			}
		}

		private int markLeft = -1;
		private int markRight = -1;
		private int markCount = -1;
		private bool markEnabled = true;

		public void MarkWordOnPaint(bool enabled)
		{
			Selection selection = selections[0];
			if (selection.Empty || !enabled)
			{
				if (lines.marksByLine.Count != 0)
					lines.marksByLine.Clear();
				lines.markedWord = null;
				markLeft = -1;
				markRight = -1;
				return;
			}
			if (selection.Left == markLeft && selection.Right == markRight && markCount == selections.Count && markEnabled == enabled)
				return;
			markLeft = selection.Left;
			markRight = selection.Right;
			markCount = selections.Count;
			markEnabled = enabled;
			Place leftPlace = lines.PlaceOf(selection.Left);
			Place rightPlace = lines.PlaceOf(selection.Right);
			if (leftPlace.iLine != rightPlace.iLine)
			{
				if (lines.marksByLine.Count != 0)
					lines.marksByLine.Clear();
				lines.markedWord = null;
				return;
			}
			Line line = lines[leftPlace.iLine];
			string word = null;
			if ((leftPlace.iChar == 0 || GetCharType(line.chars[leftPlace.iChar - 1].c) != CharType.Identifier) &&
				(rightPlace.iChar == line.chars.Count || GetCharType(line.chars[rightPlace.iChar].c) != CharType.Identifier))
			{
				StringBuilder builder = new StringBuilder();
				for (int i = leftPlace.iChar; i < rightPlace.iChar; i++)
				{
					char c = line.chars[i].c;
					if (GetCharType(c) != CharType.Identifier)
					{
						builder = null;
						break;
					}
					builder.Append(c);
				}
				if (builder != null && builder.Length != 0)
					word = builder.ToString();
			}
			if (word == null)
			{
				if (lines.marksByLine.Count != 0)
					lines.marksByLine.Clear();
				lines.markedWord = null;
				return;
			}
			lines.markedWord = word;
			RegexOptions regexOptions = RegexOptions.CultureInvariant;
			if (word.Length < 50)
				regexOptions |= RegexOptions.Compiled;
			Regex regex = new Regex("\\b" + word + "\\b", regexOptions);

			Dictionary<int, bool> selectionLefts = new Dictionary<int, bool>();
			PredictableList<int> indexList = new PredictableList<int>();
			for (int i = selections.Count; i-- > 0;)
			{
				selectionLefts[selections[i].Left] = true;
			}
			lines.marksByLine.Clear();
			int charOffset = 0;
			for (int i = 0; i < lines.blocksCount; i++)
			{
				LineBlock block = lines.blocks[i];
				for (int j = 0; j < block.count; j++)
				{
					Line lineI = block.array[j];
					MatchCollection matches = regex.Matches(lineI.Text);
					int count = matches.Count;
					if (count > 0)
					{
						indexList.Clear();
						for (int k = 0; k < count; k++)
						{
							int matchIndex = matches[k].Index;
							if (!selectionLefts.ContainsKey(charOffset + matchIndex))
								indexList.Add(matchIndex);
						}
						if (indexList.count > 0)
							lines.marksByLine[block.offset + j] = indexList.ToArray();
					}
					charOffset += lineI.chars.Count;
				}
			}
		}

		private int markedBracketCaret = -1;
		private bool markedBracketEnabled = true;

		public void MarkBracketOnPaint(bool enabled)
		{
			if (!enabled)
			{
				markedBracketCaret = -1;
				lines.markedBracket = false;
				return;
			}
			Selection selection = selections[0];
			if (!selection.Empty || selections.Count != 1)
			{
				markedBracketCaret = -1;
				lines.markedBracket = false;
				return;
			}
			if (markedBracketCaret == selection.caret && markedBracketEnabled == enabled)
				return;
			markedBracketCaret = selection.caret;
			markedBracketEnabled = enabled;

			Place place = lines.PlaceOf(selection.caret);
			Line line = lines[place.iLine];
			int iChar = -1;
			int position = selection.caret;
			char c0 = '\0';
			if (place.iChar > 0)
			{
				c0 = line.chars[place.iChar - 1].c;
				if (c0 == '{' || c0 == '}' || c0 == '(' || c0 == ')')
				{
					iChar = place.iChar - 1;
					position--;
				}
			}
			if (iChar == -1 && place.iChar < line.chars.Count)
			{
				c0 = line.chars[place.iChar].c;
				if (c0 == '{' || c0 == '}' || c0 == '(' || c0 == ')')
					iChar = place.iChar;
			}
			if (iChar == -1)
			{
				lines.markedBracket = false;
				return;
			}
			char c1;
			bool direct;
			if (c0 == '{')
			{
				c1 = '}';
				direct = true;
			}
			else if (c0 == '}')
			{
				c1 = '{';
				direct = false;
			}
			else if (c0 == '(')
			{
				c1 = ')';
				direct = true;
			}
			else
			{
				c1 = '(';
				direct = false;
			}
			PlaceIterator iterator = lines.GetCharIterator(position);
			int depth = 1;
			while (direct ? iterator.MoveRight() : iterator.MoveLeft())
			{
				char c = iterator.RightChar;
				if (c == c0)
					depth++;
				else if (c == c1)
					depth--;
				if (depth <= 0)
					break;
			}
			if (depth <= 0)
			{
				lines.markedBracket = true;
				lines.markedBracket0 = new Place(iChar, place.iLine);
				lines.markedBracket1 = iterator.Place;
				return;
			}
			lines.markedBracket = false;
		}
		
		public bool FixLineBreaks()
		{
			return Execute(new FixLineBreaksCommand());
		}
		
		public void ViResetCommandsBatching()
		{
			ResetCommandsBatching();
		}
		
		public void ViMoveToCharLeft(char charToFind, bool shift, int count, bool at)
		{
			foreach (Selection selection in lines.selections)
			{
				Place place = lines.PlaceOf(selection.caret);
				Line line = lines[place.iLine];
				bool needAfterMove = at;
				for (int i = 0; i < count; i++)
				{
					int iChar = line.LeftIndexOfChar(charToFind, place.iChar - 1);
					if (iChar != -1)
					{
						place.iChar = iChar;
						selection.caret = lines.IndexOf(place);
						lines.SetPreferredPos(selection, place);
						needAfterMove &= true;
					}
					else
					{
						break;
					}
				}
				if (needAfterMove)
				{
					selection.caret++;
					lines.SetPreferredPos(selection, place);
				}
				if (!shift)
				{
					selection.anchor = selection.caret;
				}
			}
		}
		
		public void ViMoveToCharRight(char charToFind, bool shift, int count, bool at)
		{
			foreach (Selection selection in lines.selections)
			{
				Place place = lines.PlaceOf(selection.caret);
				Line line = lines[place.iLine];
				bool needAfterMove = at;
				for (int i = 0; i < count; i++)
				{
					int iChar = line.IndexOfChar(charToFind, place.iChar + 1);
					if (iChar != -1)
					{
						if (shift)
						{
							iChar++;
						}
						place.iChar = iChar;
						selection.caret = lines.IndexOf(place);
						lines.SetPreferredPos(selection, place);
						needAfterMove &= true;
					}
					else
					{
						break;
					}
				}
				if (needAfterMove)
				{
					selection.caret--;
					lines.SetPreferredPos(selection, place);
				}
				selection.SetEmptyIfNotShift(shift);
			}
		}
		
		public void ViCollapseSelections()
		{
			foreach (Selection selection in selections)
			{
				selection.caret = selection.anchor;
			}
			JoinSelections();
		}
		
		public void ViMoveWordRight(bool shift, bool change)
		{
			foreach (Selection selection in lines.selections)
			{
				PlaceIterator iterator = lines.GetCharIterator(selection.caret);
				if (GetCharType(iterator.RightChar) == CharType.Identifier)
				{
					while (GetCharType(iterator.RightChar) == CharType.Identifier)
					{
						if (!iterator.MoveRightWithRN())
							break;
					}
					if (!change)
					if (IsSpaceOrNewLine(iterator.RightChar))
					{
						while (IsSpaceOrNewLine(iterator.RightChar))
						{
							if (!iterator.MoveRightWithRN())
								break;
						}
					}
				}
				else if (GetCharType(iterator.RightChar) == CharType.Punctuation)
				{
					while (GetCharType(iterator.RightChar) == CharType.Punctuation)
					{
						if (!iterator.MoveRightWithRN())
							break;
					}
					if (!change)
					if (IsSpaceOrNewLine(iterator.RightChar))
					{
						while (IsSpaceOrNewLine(iterator.RightChar))
						{
							if (!iterator.MoveRightWithRN())
								break;
						}
					}
				}
				else if (IsSpaceOrNewLine(iterator.RightChar))
				{
					while (IsSpaceOrNewLine(iterator.RightChar))
					{
						if (!iterator.MoveRightWithRN())
							break;
					}
				}
				selection.caret = iterator.Position;
				selection.SetEmptyIfNotShift(shift);
				lines.SetPreferredPos(selection, iterator.Place);
			}
		}
		
		public void ViMoveWordE(bool shift)
		{
			foreach (Selection selection in lines.selections)
			{
				PlaceIterator iterator = lines.GetCharIterator(selection.caret);
				ViMoveWordE_Move(iterator, shift);
				selection.caret = iterator.Position;
				selection.SetEmptyIfNotShift(shift);
				lines.SetPreferredPos(selection, iterator.Place);
			}
		}
		
		private void ViMoveWordE_Move(PlaceIterator iterator, bool shift)
		{
			char c = iterator.RightChar;
			if (IsSpaceOrNewLine(c))
			{
				while (true)
				{
					if (!iterator.MoveRightWithRN())
						return;
					if (!IsSpaceOrNewLine(iterator.RightChar))
						break;
				}
				CharType type = GetCharType(iterator.RightChar);
				while (true)
				{
					if (!iterator.MoveRightWithRN())
						return;
					if (GetCharType(iterator.RightChar) != type)
						break;
				}
				if (!shift)
				{
					iterator.MoveLeftWithRN();
				}
			}
			else
			{
				if (!iterator.MoveRightWithRN())
					return;
				if (IsSpaceOrNewLine(iterator.RightChar))
				{
					while (true)
					{
						if (!iterator.MoveRightWithRN())
							return;
						if (!IsSpaceOrNewLine(iterator.RightChar))
							break;
					}
					CharType type = GetCharType(iterator.RightChar);
					while (true)
					{
						if (!iterator.MoveRightWithRN())
							return;
						if (GetCharType(iterator.RightChar) != type)
							break;
					}
					if (!shift)
					{
						iterator.MoveLeftWithRN();
					}
				}
				else
				{
					CharType type = GetCharType(iterator.RightChar);
					while (true)
					{
						if (!iterator.MoveRightWithRN())
							return;
						if (GetCharType(iterator.RightChar) != type)
							break;
					}
					if (!shift)
					{
						iterator.MoveLeftWithRN();
					}
				}
			}
		}
		
		public void ViMoveWordLeft(bool shift, bool change)
		{
			foreach (Selection selection in lines.selections)
			{
				PlaceIterator iterator = lines.GetCharIterator(selection.caret);
				iterator.MoveLeftWithRN();
				if (IsSpaceOrNewLine(iterator.RightChar))
				{
					while (IsSpaceOrNewLine(iterator.RightChar))
					{
						if (!iterator.MoveLeftWithRN())
							break;
					}
				}
				CharType type = GetCharType(iterator.RightChar);
				while (GetCharType(iterator.LeftChar) == type)
				{
					if (!iterator.MoveLeftWithRN())
						break;
				}
				selection.caret = iterator.Position;
				selection.SetEmptyIfNotShift(shift);
				lines.SetPreferredPos(selection, iterator.Place);
			}
		}
		
		public void ViMoveHome(bool shift, bool indented)
		{
			foreach (Selection selection in selections)
			{
				Place caret = lines.PlaceOf(selection.caret);
				Line line = lines[caret.iLine];
				int minIChar = 0;
				int charsCount = line.NormalCount;
				if (indented)
				{
					while (minIChar < charsCount && char.IsWhiteSpace(line.chars[minIChar].c))
					{
						minIChar++;
					}
				}
				caret.iChar = minIChar;
				if (!shift && caret.iChar >= line.NormalCount)
				{
					caret.iChar = line.NormalCount - 1;
				}
				if (caret.iChar < 0)
				{
					caret.iChar = 0;
				}
				selection.caret = lines.IndexOf(caret);
				selection.SetEmptyIfNotShift(shift);
				lines.SetPreferredPos(selection, caret);
			}
		}
		
		public void ViMoveEnd(bool shift, int count)
		{
			foreach (Selection selection in selections)
			{
				Place caret = lines.PlaceOf(selection.caret);
				if (count > 1)
				{
					caret.iLine += count - 1;
					if (caret.iLine >= lines.LinesCount)
					{
						caret.iLine = lines.LinesCount - 1;
					}
				}
				caret.iChar = lines[caret.iLine].NormalCount;
				selection.caret = lines.IndexOf(caret);
				selection.SetEmptyIfNotShift(shift);
			}
			ViFixPositions(true);
		}
		
		public void ViDocumentEnd(bool shift)
		{
			Place place = new Place(0, lines.LinesCount - 1);
			int position = lines.IndexOf(place);
			foreach (Selection selection in selections)
			{
				selection.caret = position;
				selection.SetEmptyIfNotShift(shift);
			}
			lines.JoinSelections();
			lines.SetPreferredPos(lines.LastSelection, place);
		}
		
		public void ViMoveLeft(bool shift)
		{
			foreach (Selection selection in lines.selections)
			{
				Place place = lines.PlaceOf(selection.caret);
				if (place.iChar > 0)
				{
					selection.caret--;
					selection.SetEmptyIfNotShift(shift);
				}
			}
			ViFixPositions(true);
		}
		
		public void ViMoveRight(bool shift)
		{
			foreach (Selection selection in lines.selections)
			{
				Place place = lines.PlaceOf(selection.caret);
				Line line = lines[place.iLine];
				if (place.iChar < line.NormalCount)
				{
					place.iChar++;
					selection.caret = lines.IndexOf(place);
					selection.SetEmptyIfNotShift(shift);
				}
			}
			ViFixPositions(true);
		}
		
		public void ViMoveUp(bool shift)
		{
			foreach (Selection selection in selections)
			{
				Place place = lines.PlaceOf(selection.caret);
				if (place.iLine > 0)
				{
					place.iLine--;
					place = ViGetPreferredPlace(selection, place);
					selection.caret = lines.IndexOf(place);
					selection.SetEmptyIfNotShift(shift);
				}
			}
			ViFixPositions(false);
		}
		
		public void ViMoveDown(bool shift)
		{
			foreach (Selection selection in selections)
			{
				Place place = lines.PlaceOf(selection.caret);
				if (place.iLine < lines.LinesCount - 1)
				{
					place.iLine++;
					place = ViGetPreferredPlace(selection, place);
					selection.caret = lines.IndexOf(place);
					selection.SetEmptyIfNotShift(shift);
				}
			}
			ViFixPositions(false);
		}
		
		private void ViFixPositions(bool setPreferredPos)
		{
			foreach (Selection selection in lines.selections)
			{
				Place place = lines.PlaceOf(selection.caret);
				if (selection.Empty)
				{
					Line line = lines[place.iLine];
					int count = line.NormalCount;
					if (count > 0 && place.iChar >= count)
					{
						place.iChar = count - 1;
						selection.caret = lines.IndexOf(place);
						selection.anchor = selection.caret;
					}
				}
				if (setPreferredPos)
				{
					lines.SetPreferredPos(selection, place);
				}
			}
		}
		
		public void ViMoveRightFromCursor()
		{
			foreach (Selection selection in lines.selections)
			{
				Place place = lines.PlaceOf(selection.caret);
				if (selection.Empty)
				{
					Line line = lines[place.iLine];
					if (place.iChar < line.NormalCount)
					{
						place.iChar++;
						selection.caret = lines.IndexOf(place);
						selection.anchor = selection.caret;
						lines.SetPreferredPos(selection, place);
					}
				}
			}
		}
		
		public void ViReplaceChar(char c, int count)
		{
			foreach (Selection selection in selections)
			{
				if (selection.Empty)
				{
					Place place = lines.PlaceOf(selection.anchor);
					Line line = lines[place.iLine];
					if (place.iChar + count <= line.NormalCount)
					{
						selection.caret += count;
					}
				}
			}
			lines.JoinSelections();
			string[] texts = new string[selections.Count];
			for (int i = 0, selectionsCount = selections.Count; i < selectionsCount; i++)
			{
				Selection selection = lines.selections[i];
				if (selection.Count == 1)
				{
					texts[i] = c + "";
				}
				else
				{
					string text = lines.GetText(selection.Left, selection.Count);
					StringBuilder builder = new StringBuilder();
					for (int j = 0; j < text.Length; j++)
					{
						if (text[j] == '\n' || text[j] == '\r')
						{
							builder.Append(text[j]);
						}
						else
						{
							builder.Append(c);
						}
					}
					texts[i] = builder.ToString();
				}
			}
			Execute(new InsertTextCommand(null, texts, true));
			if (selections.Count == texts.Length)
			{
				for (int i = 0, selectionsCount = selections.Count; i < selectionsCount; i++)
				{
					Selection selection = lines.selections[i];
					selection.caret -= texts[i].Length;
					selection.anchor = selection.caret;
				}
			}
		}
		
		public void ViCut()
		{
			Copy();
			EraseSelection();
			ViFixPositions(true);
		}
		
		public void ViCopy(char register)
		{
			Execute(new CopyCommand(register));
		}
		
		public void ViSavePositions()
		{
			Execute(new ViSavePositions());
		}
		
		public void ViJ()
		{
			ViCollapseSelections();
			string[] texts = new string[selections.Count];
			for (int i = 0, selectionsCount = selections.Count; i < selectionsCount; i++)
			{
				Selection selection = lines.selections[i];
				if (selection.Empty)
				{
					Place place = lines.PlaceOf(selection.anchor);
					if (place.iLine < lines.LinesCount - 1)
					{
						Line line = lines[place.iLine];
						place.iChar = line.NormalCount;
						selection.anchor = lines.IndexOf(place);
						selection.caret = selection.anchor + line.GetRN().Length;
						texts[i] = " ";
					}
					else
					{
						texts[i] = "";
					}
				}
			}
			Execute(new InsertTextCommand(null, texts, true));
			ViMoveLeft(false);
		}
		
		public void ViPaste(char register)
		{
			Execute(new PasteCommand(register));
			for (int i = 0, selectionsCount = selections.Count; i < selectionsCount; i++)
			{
				Selection selection = lines.selections[i];
				selection.caret--;
				selection.anchor = selection.caret;
			}
		}
		
		public void ViGoToLine(int iLine, bool shift)
		{
			ClearMinorSelections();
			Place place = new Place(0, CommonHelper.Clamp(iLine, 0, lines.LinesCount - 1));
			Line line = lines[place.iLine];
			place.iChar = line.GetFirstSpaces();
			LastSelection.caret = lines.IndexOf(place);
			LastSelection.SetEmptyIfNotShift(shift);
		}
		
		private Place ViGetPreferredPlace(Selection selection, Place place)
		{
			Line line = lines[place.iLine];
			return new Place(line.NormalIndexOfPos(selection.preferredPos), place.iLine);
		}
	}
}
