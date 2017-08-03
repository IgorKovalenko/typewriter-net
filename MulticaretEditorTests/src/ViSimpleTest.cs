using System;
using NUnit.Framework;
using MulticaretEditor;
using System.Windows.Forms;

namespace UnitTests
{
	[TestFixture]
	public class ViSimpleTest : ControllerTestBase
	{
		private Receiver receiver;
		
		private void SetViMode(bool viMode)
		{
			receiver.SetViMode(viMode ? ViMode.Normal : ViMode.Insert);
			Assert.AreEqual(viMode ? ViMode.Normal : ViMode.Insert, receiver.ViMode);
		}
		
		private void EscapeNormalViMode()
		{
			bool scrollToCursor;
			receiver.DoKeyDown(Keys.OemOpenBrackets | Keys.Control, out scrollToCursor);
			Assert.AreEqual(ViMode.Normal, receiver.ViMode);
		}
		
		private ViSimpleTest Press(string keys)
		{
			foreach (char c in keys)
			{
				string viShortcut;
				bool scrollToCursor;
				receiver.DoKeyPress(c, out viShortcut, out scrollToCursor);
			}
			return this;
		}
		
		private ViSimpleTest Press(Keys keysData)
		{
			bool scrollToCursor;
			receiver.DoKeyDown(keysData, out scrollToCursor);
			return this;
		}
		
		private ViSimpleTest PressCommandMode()
		{
			bool scrollToCursor;
			receiver.DoKeyDown(Keys.Control | Keys.OemOpenBrackets, out scrollToCursor);
			return this;
		}
		
		private ViSimpleTest Put(int iChar, int iLine, bool shift)
		{
			controller.PutCursor(new Place(iChar, iLine), shift);
			return this;
		}
		
		private ViSimpleTest Put(int iChar, int iLine)
		{
			controller.PutCursor(new Place(iChar, iLine), false);
			AssertSelection().Both(iChar, iLine);
			return this;
		}
		
		[SetUp]
		public void SetUp()
		{
			ClipboardExecutor.Reset(true);
			Init();
			lines.lineBreak = "\n";
			receiver = new Receiver(controller, ViMode.Insert, false);
			SetViMode(true);
		}
		
		[Test]
		public void fF()
		{
			lines.SetText(
			//	 0123456789012345678901
				"Du hast\n" +
				"Du hast mich\n" +
				"abcd  ,.;.;.asdf234234");
			
			Put(3, 1).Press("fa").AssertSelection().Both(4, 1).NoNext();
			Put(3, 1).Press("fs").AssertSelection().Both(5, 1).NoNext();
			Put(4, 2).Press("2f;").AssertSelection().Both(10, 2).NoNext();
			
			Put(3, 1).Press("Fu").AssertSelection().Both(1, 1).NoNext();
			Put(3, 1).Press("F ").AssertSelection().Both(2, 1).NoNext();
			Put(12, 2).Press("2F;").AssertSelection().Both(8, 2).NoNext();
		}
		
		[Test]
		public void tT()
		{
			lines.SetText(
			//	 0123456789012345678901
				"Du hast\n" +
				"Du hast mich\n" +
				"abcd  ,.;.;.asdf234234");
			
			Put(3, 1).Press("ti").AssertSelection().Both(8, 1).NoNext();
			Put(3, 1).Press("ta").AssertSelection().Both(3, 1).NoNext();			
			Put(3, 1).Press("Tu").AssertSelection().Both(2, 1).NoNext();
		}
		
		[Test]
		public void tT_RepeatNuance()
		{
			lines.SetText(
			//	 0123456789012345678901
				"Du hast\n" +
				"Du hast mich\n" +
				"abcd  ,.;.;.asdf234234");
			
			Put(4, 2).Press("2t;").AssertSelection().Both(9, 2).NoNext();
			Put(7, 2).Press("2t;").AssertSelection().Both(9, 2).NoNext();
			Put(12, 2).Press("2T;").AssertSelection().Both(9, 2).NoNext();
			Put(11, 2).Press("2T;").AssertSelection().Both(9, 2).NoNext();
			Put(7, 2).Press("t;").AssertSelection().Both(7, 2).NoNext();
			Put(11, 2).Press("T;").AssertSelection().Both(11, 2).NoNext();
		}
		
		[Test]
		public void fFtT_RepeatNuance2()
		{
			lines.SetText(
			//	 0123456789012345678901234
				"aaaa((cccc(dddd))eeeeeee)");
			
			Put(18, 0).Press("2F(").AssertSelection().Both(5, 0).NoNext();
			Press("F(").AssertSelection().Both(4, 0).NoNext();
			Press("2t)").AssertSelection().Both(15, 0).NoNext();
			Press("2T)").AssertSelection().Both(16, 0).NoNext();
			Put(6, 0).Press("2f)").AssertSelection().Both(16, 0).NoNext();
		}
		
		[Test]
		public void S6_0()
		{
			lines.SetText(
			//	 012345678901234
				"Du hast\n" +
				"   Du hast mich\n" +
				"   a");
			
			Put(4, 0).Press("0").AssertSelection().Both(0, 0).NoNext();
			Put(4, 1).Press("0").AssertSelection().Both(0, 1).NoNext();
			
			Put(4, 0).Press("^").AssertSelection().Both(0, 0).NoNext();
			
			Put(14, 1).Press("^").AssertSelection().Both(3, 1).NoNext();
			Put(4, 1).Press("^").AssertSelection().Both(3, 1).NoNext();
			Put(3, 1).Press("^").AssertSelection().Both(3, 1).NoNext();
			Put(2, 1).Press("^").AssertSelection().Both(3, 1).NoNext();
			Put(1, 1).Press("^").AssertSelection().Both(3, 1).NoNext();
			Put(0, 1).Press("^").AssertSelection().Both(3, 1).NoNext();
		}
		
		[Test]
		public void S6_OnlySpacesNuance()
		{
			lines.SetText(
			//	 012345678901234
				"Du hast\n" +
				"   \n" +
				"\n" +
				"a");
			
			Put(1, 1).Press("^").AssertSelection().Both(2, 1).NoNext();
			Put(0, 2).Press("^").AssertSelection().Both(0, 2).NoNext();
		}
		
		[Test]
		public void S4()
		{
			lines.SetText(
			//	 012345678901234
				"Du hast\n" +
				"   Du hast mich\n" +
				"   a");
			
			Put(4, 0).Press("$").AssertSelection().Both(6, 0).NoNext();
			Put(0, 0).Press("$").AssertSelection().Both(6, 0).NoNext();
			Put(1, 1).Press("$").AssertSelection().Both(14, 1).NoNext();
			Put(3, 2).Press("$").AssertSelection().Both(3, 2).NoNext();
		}
		
		[Test]
		public void S4_Repeat()
		{
			lines.SetText(
			//	 012345678901234
				"Du hast\n" +
				"   Du hast mich\n" +
				"   a");
			
			Put(4, 0).Press("2").Press("$").AssertSelection().Both(14, 1).NoNext();
			Put(4, 0).Press("3").Press("$").AssertSelection().Both(3, 2).NoNext();
			Put(4, 0).Press("4").Press("$").AssertSelection().Both(3, 2).NoNext();
		}
		
		[Test]
		public void gg_G()
		{
			lines.SetText(
			//	 012345678901234
				"Du hast\n" +
				"   Du hast mich\n" +
				"aaaaaaa");
			Put(4, 1).Press("g").Press("g").AssertSelection().Both(0, 0).NoNext();
			Put(4, 1).Press("G").AssertSelection().Both(0, 2).NoNext();
			Put(4, 2).Press("G").AssertSelection().Both(0, 2).NoNext();
		}
		
		[Test]
		public void hjkl()
		{
			lines.SetText(
			//	 012345678901234
				"Du hast\n" +
				"   Du hast mich\n" +
				"aaaaaaa");
			Put(4, 1).Press("j").AssertSelection().Both(4, 2).NoNext();
			Put(4, 1).Press("k").AssertSelection().Both(4, 0).NoNext();
			Put(4, 1).Press("h").AssertSelection().Both(3, 1).NoNext();
			Put(4, 1).Press("l").AssertSelection().Both(5, 1).NoNext();
			
			Put(4, 1).Press("hhh").AssertSelection().Both(1, 1).NoNext();
			Press("h").AssertSelection().Both(0, 1).NoNext();
			Press("h").AssertSelection().Both(0, 1).NoNext();
			
			Put(4, 1).Press("9l").AssertSelection().Both(13, 1).NoNext();
			Press("l").AssertSelection().Both(14, 1).NoNext();
			Press("l").AssertSelection().Both(14, 1).NoNext();
			
			Put(14, 1).Press("j").AssertSelection().Both(6, 2).NoNext();
			Press("j").AssertSelection().Both(6, 2).NoNext();
			
			Put(14, 1).Press("k").AssertSelection().Both(6, 0).NoNext();
			Press("k").AssertSelection().Both(6, 0).NoNext();
		}
		
		[Test]
		public void gjk_Simple()
		{
			lines.SetText(
			//	 012345678901234
				"Du hast\n" +
				"   Du hast mich\n" +
				"aaaaaaa");
			Put(4, 1).Press("gj").AssertSelection().Both(4, 2).NoNext();
			Put(4, 1).Press("gk").AssertSelection().Both(4, 0).NoNext();
		}
		
		[Test]
		public void gjk_Subline()
		{
			lines.SetText(
			//	 012345678901234
				"Abcd efg "/*br*/ +
				"hidklmnop\n" +
				"   qrstuw\n" +
				"aaaaaaa");
			lines.wordWrap = true;
			lines.wwValidator.Validate(10);
			Assert.AreEqual(4, lines.wwSizeY);
			Put(4, 0).Press("gj").AssertSelection().Both(13, 0).NoNext();
			Put(13, 0).Press("gk").AssertSelection().Both(4, 0).NoNext();
		}
		
		[Test]
		public void gjk_EndPosition()
		{
			lines.SetText(
			//	 012345678901234
				"Abcd efg "/*br*/ +
				"hidk\n" +
				"   qrstuw\n" +
				"aaaaaaa\n" +
				"bbbbbbbb");
			lines.wordWrap = true;
			lines.wwValidator.Validate(10);
			Assert.AreEqual(5, lines.wwSizeY);
			Put(8, 0).Press("gj").AssertSelection().Both(12, 0).NoNext();
			Put(7, 3).Press("gk").AssertSelection().Both(6, 2).NoNext();
		}
		
		[Test]
		public void hjkl_preferredPosition()
		{
			lines.SetText(
			//	 012345678901234
				"Du hast\n" +
				"   Du hast mich\n" +
				"aaaaaaa");

			Put(14, 1);
			Press("jk").AssertSelection().Both(14, 1).NoNext();
			Press("kj").AssertSelection().Both(14, 1).NoNext();
		}
		
		[Test]
		public void hjkl_preferredPosition_withTab()
		{
			lines.SetText(
			//	 012345678901234
				"Du hast\n" +
			    "\tDu hast mich\n" +
				"aaaaaaa");

			Put(10, 1);
			Press("jk").AssertSelection().Both(10, 1).NoNext();
			Press("kj").AssertSelection().Both(10, 1).NoNext();
		}
		
		[Test]
		public void hjkl_DocumentEnd()
		{
			lines.SetText(
				"aaa\n" +
				"");

			Put(2, 0).Press("j").AssertSelection().Both(0, 1).NoNext();
		}
		
		[Test]
		public void S4_preferredPos()
		{
			lines.SetText(
			//	 012345678901234
				"Du hast\n" +
				  "\tDu hast mich\n" +
				"   a");
			
			Put(4, 0).Press("$").AssertSelection().Both(6, 0).NoNext();
			Press("j").AssertSelection().Both(3, 1).NoNext();
		}
		
		[Test]
		public void r()
		{
			lines.SetText("Du hast");
			Put(3, 0).Press("rx").AssertSelection().Both(3, 0).NoNext();
			AssertText("Du xast");
		}
		
		[Test]
		public void r_Repeat()
		{
			lines.SetText("Du hast");
			Put(3, 0).Press("3rx").AssertSelection().Both(5, 0).NoNext();
			AssertText("Du xxxt");
		}
		
		[Test]
		public void r_RepeatExtremal()
		{
			lines.SetText("Du hast");
			Put(3, 0).Press("3rx").AssertSelection().Both(5, 0).NoNext();
			AssertText("Du xxxt");
			Put(3, 0).Press("4ry").AssertSelection().Both(6, 0).NoNext();
			AssertText("Du yyyy");
			Put(3, 0).Press("5rz").AssertSelection().Both(3, 0).NoNext();
			AssertText("Du yyyy");
		}
		
		[Test]
		public void r_PreferredPos()
		{
			lines.SetText("Du hast\nDu hast mich");
			Put(3, 0).Press("rx").AssertSelection().Both(3, 0).NoNext();
			AssertText("Du xast\nDu hast mich");
			Press("j").AssertSelection().Both(3, 1);
		}
		
		[Test]
		public void r_Selection()
		{
			lines.SetText("Du hast\nDu hast mich");
			Put(3, 0).Put(2, 1, true).Press("rx").AssertSelection().Both(3, 0).NoNext();
			AssertText("Du xxxx\nxx hast mich");
		}
		
		[Test]
		public void cw()
		{
			lines.SetText("Du hast\nDu hast mich");
			
			Put(3, 1).Press("cw").AssertSelection().Both(3, 1).NoNext();
			Press("NEW_WORD").PressCommandMode().AssertSelection().Both(10, 1).NoNext();
			AssertText("Du hast\nDu NEW_WORD mich");
			
			Put(0, 1).Press("2cw").AssertSelection().Both(0, 1).NoNext();
			Press("AAA").PressCommandMode().AssertSelection().Both(2, 1).NoNext();
			AssertText("Du hast\nAAA mich");
		}
		
		[Ignore]
		[Test]
		public void w_DocumentEnd()
		{
			lines.SetText("Abcdef");
			Put(2, 0).Press("w").AssertSelection().Both(5, 0);
		}
		
		[Test]
		public void cb()
		{
			lines.SetText("Du hast\nDu hast mich");
			
			Put(8, 1).Press("cb").AssertSelection().Both(3, 1).NoNext();
			Press("BBB").PressCommandMode().AssertSelection().Both(5, 1).NoNext();
			AssertText("Du hast\nDu BBBmich");
		}
		
		[Test]
		public void cB()
		{
			lines.SetText("Du a,hast!! mich");
			Put(11, 0).Press("cB").AssertSelection().Both(3, 0).NoNext();
			Press("BBB").PressCommandMode().AssertSelection().Both(5, 0).NoNext();
			AssertText("Du BBB mich");
			
			lines.SetText("Du a,hast!! mich");
			Put(12, 0).Press("cB").AssertSelection().Both(3, 0).NoNext();
			Press("BBB").PressCommandMode().AssertSelection().Both(5, 0).NoNext();
			AssertText("Du BBBmich");
		}
		
		[Test]
		public void x()
		{
			lines.SetText("Du hast\nDu hast mich");
			
			Put(3, 0).Press("x").AssertSelection().Both(3, 0).NoNext();
			AssertText("Du ast\nDu hast mich");
			
			Put(3, 0).Press("2x").AssertSelection().Both(3, 0).NoNext();
			AssertText("Du t\nDu hast mich");
			
			Put(8, 1).Press("5x").AssertSelection().Both(7, 1).NoNext();
			AssertText("Du t\nDu hast ");
		}
		
		[Test]
		public void NumberG()
		{
			lines.SetText("Du hast\nDu hast mich\nDu hast mich");
			
			Put(3, 1).Press("1G").AssertSelection().Both(0, 0).NoNext();
			Put(3, 1).Press("2G").AssertSelection().Both(0, 1).NoNext();
			Put(3, 1).Press("3G").AssertSelection().Both(0, 2).NoNext();
			Put(3, 1).Press("4G").AssertSelection().Both(0, 2).NoNext();
		}
		
		[Test]
		public void NumberG_Tabbed()
		{
			lines.SetText("Du hast\n    Du hast mich\n\t\tDu hast mich");
			
			Put(3, 1).Press("1G").AssertSelection().Both(0, 0).NoNext();
			Put(3, 1).Press("2G").AssertSelection().Both(4, 1).NoNext();
			Put(3, 1).Press("3G").AssertSelection().Both(2, 2).NoNext();
			Put(3, 1).Press("4G").AssertSelection().Both(2, 2).NoNext();
		}
		
		[Test]
		public void y()
		{
			lines.SetText("Du hast mich");
			
			PutToViClipboard("");
			Put(3, 0).Press("yw");
			AssertViClipboard("hast ");
			AssertSelection().Both(3, 0).NoNext();
		}
		
		[Test]
		public void p()
		{
			lines.SetText("Du hast mich");	
			
			PutToViClipboard("AAA");
			Put(3, 0).Press("p");
			AssertText("Du hAAAast mich");
			AssertSelection().Both(6, 0).NoNext();
		}
		
		[Test]
		public void p_Undo()
		{
			lines.SetText("Du hast mich");
			
			PutToViClipboard("AAA");
			Put(3, 0).Press("p");
			AssertText("Du hAAAast mich");
			AssertSelection("#1").Both(6, 0).NoNext();
			Press("u");
			AssertText("Du hast mich");
			AssertSelection("#2").Both(3, 0);
		}
		
		[Test]
		public void p_Redo_Controvertial()
		{
			lines.SetText("Du hast mich");
			
			PutToViClipboard("AAA");
			Put(3, 0).Press("p");
			AssertText("Du hAAAast mich");
			AssertSelection("#1").Both(6, 0).NoNext();
			Press("u");
			AssertText("Du hast mich");
			AssertSelection("#2").Both(3, 0);
			Press(Keys.Control | Keys.R);
			AssertText("Du hAAAast mich");
			AssertSelection("#3").Both(6, 0);
		}
		
		[Test]
		public void P()
		{
			lines.SetText("Du hast mich");	
			
			PutToViClipboard("AAA");
			Put(3, 0).Press("P");
			AssertText("Du AAAhast mich");
			AssertSelection().Both(5, 0).NoNext();
		}
		
		[Test]
		public void P_Undo()
		{
			lines.SetText("Du hast mich");	
			
			PutToViClipboard("AAA");
			Put(3, 0).Press("P");
			AssertText("Du AAAhast mich");
			AssertSelection("#1").Both(5, 0).NoNext();
			
			Press("u");
			AssertText("Du hast mich");
			AssertSelection("#2").Both(3, 0);
			Press(Keys.Control | Keys.R);
			AssertText("Du AAAhast mich");
			AssertSelection("#3").Both(5, 0);
		}
		
		[Test]
		public void p_UndoRedo_Repeat()
		{
			lines.SetText("Du hast mich gefragt");
			Put(3, 0).Press("dw").AssertText("Du mich gefragt");
			Press("x").AssertText("Du ich gefragt");
			AssertSelection().Both(3, 0);
			
			Press("2u").AssertText("Du hast mich gefragt");
			AssertSelection().Both(3, 0);
			
			Press("2").Press(Keys.Control | Keys.R).AssertText("Du ich gefragt");
			AssertSelection().Both(3, 0);
		}
		
		[Test]
		public void dw_cw()
		{
			lines.SetText("Du hast mich gefragt");
			Put(0, 0).Press("2dw").AssertText("mich gefragt");
			
			lines.SetText("Du hast mich gefragt");
			Put(0, 0).Press("2cw").Press("AAA").PressCommandMode().AssertText("AAA mich gefragt");
		}
		
		[Test]
		public void e()
		{
			//             0123456789012345678901234567
			lines.SetText("Du hast ;. AAAA..lkd d  asdf");
			Put(3, 0).Press("e").AssertSelection().Both(6, 0);
			Press("e").AssertSelection().Both(9, 0);
			Put(8, 0).Press("e").AssertSelection().Both(9, 0);
			Press("e").AssertSelection().Both(14, 0);
			Press("e").AssertSelection().Both(16, 0);
			Press("e").AssertSelection().Both(19, 0);
			Press("e").AssertSelection().Both(21, 0);
			Press("e").AssertSelection().Both(27, 0);
			Put(20, 0).Press("e").AssertSelection().Both(21, 0);
			Put(22, 0).Press("e").AssertSelection().Both(27, 0);
			Put(23, 0).Press("e").AssertSelection().Both(27, 0);
		}
		
		[Test]
		public void Number_e_de()
		{
			//             0123456789012345678901234567
			lines.SetText("Du hast ;. AAAA..lkd d  asdf");
			Put(0, 0).Press("2e").AssertSelection().Both(6, 0);
			Put(8, 0).Press("5e").AssertSelection().Both(21, 0);
			Put(8, 0).Press("d2e").AssertSelection().Both(8, 0);
			AssertText("Du hast ..lkd d  asdf");
		}
		
		[Test]
		public void ce()
		{
			lines.SetText("Du hast mich");
			Put(3, 0).Press("ce").AssertSelection().Both(3, 0);
			Press("AAA").PressCommandMode().AssertText("Du AAA mich");
			AssertSelection().Both(5, 0);
		}
		
		[Test]
		public void de()
		{
			//             0123456789012345678901234567
			lines.SetText("Du hast ;. AAAA..lkd d  asdf");
			Put(3, 0).Press("de").AssertText("Du  ;. AAAA..lkd d  asdf");
			AssertSelection().Both(3, 0);
			
			//             0123456789012345678901234567
			lines.SetText("Du hast ;. AAAA..lkd d  asdf");
			Put(15, 0).Press("de").AssertText("Du hast ;. AAAAlkd d  asdf");
			AssertSelection().Both(15, 0);
		}
		
		[Test]
		public void de_AtEnd()
		{
			lines.SetText(
			//   012345678901
				"Du hast mich\n" +
				"aaaa");
			Put(6, 0).Press("de").AssertText(
				"Du has\n" +
				"aaaa");
			AssertSelection().Both(5, 0);
		}
		
		[Test]
		public void dW_cW()
		{
			lines.SetText("Du hast mich gefragt");
			Put(0, 0).Press("2dW").AssertText("mich gefragt");
			
			lines.SetText("Du,a,. hast! mich gefragt");
			Put(0, 0).Press("2dW").AssertText("mich gefragt");
			
			lines.SetText("Du hast mich gefragt");
			Put(0, 0).Press("2cW").Press("AAA").PressCommandMode().AssertText("AAA mich gefragt");
			
			lines.SetText("Du,a,. hast! mich gefragt");
			Put(0, 0).Press("2cW").Press("AAA").PressCommandMode().AssertText("AAA mich gefragt");
		}
		
		[Test]
		public void dE()
		{
			//             0123456789012345678901234567
			lines.SetText("Du hast ;. AAAA..lkd d  asdf");
			Put(3, 0).Press("dE").AssertText("Du  ;. AAAA..lkd d  asdf");
			AssertSelection().Both(3, 0);
			
			//             0123456789012345678901234567
			lines.SetText("Du hast ;. AAAA..lkd d  asdf");
			Put(11, 0).Press("dE").AssertText("Du hast ;.  d  asdf");
			AssertSelection().Both(11, 0);
		}
		
		[Test]
		public void J()
		{
			lines.SetText(
			//   012345678901
				"Du hast mich\n" +
				"aaaa\n" +
				"bbbb");
			Put(3, 0).Press("J").AssertText("Du hast mich aaaa\nbbbb");
			AssertSelection().Both(12, 0);
		}
		
		[Test]
		public void J_Indented()
		{
			lines.SetText(
			//   012345678901
				"Du hast mich\n" +
				"\taaaa\n" +
				"bbbb");
			Put(3, 0).Press("J").AssertText("Du hast mich aaaa\nbbbb");
			AssertSelection().Both(12, 0);
		}
		
		[Test]
		public void Registers()
		{
			lines.SetText(
			//   012345678901
				"Du hast mich\n" +
				"aaaa\n" +
				"bbbb");
			Put(3, 0).Press("\"ayw").AssertText("Du hast mich\naaaa\nbbbb");
			Assert.AreEqual("hast ", ClipboardExecutor.GetFromRegister('a'));
			
			Put(8, 0).Press("\"bye").AssertText("Du hast mich\naaaa\nbbbb");
			Put(3, 0).Press("ye").AssertText("Du hast mich\naaaa\nbbbb");
			Assert.AreEqual("mich", ClipboardExecutor.GetFromRegister('b'));
			Assert.AreEqual("hast ", ClipboardExecutor.GetFromRegister('a'));
			Assert.AreEqual("hast", ClipboardExecutor.GetFromRegister('\0'));
			
			Put(0, 1).Press("\"ap").AssertText("Du hast mich\nahast aaa\nbbbb");
			Put(0, 2).Press("\"bp").AssertText("Du hast mich\nahast aaa\nbmichbbb");
			Put(0, 2).Press("p").AssertText("Du hast mich\nahast aaa\nbhastmichbbb");
		}
		
		[Test]
		public void d_Registers()
		{
			lines.SetText("Abcd efghij klmnop");
			Put(5, 0).Press("\"adw").AssertText("Abcd klmnop");
			Assert.AreEqual("efghij ", ClipboardExecutor.GetFromRegister('a'));
			Assert.AreEqual("", ClipboardExecutor.GetFromRegister('\0'));
		}
		
		[Test]
		public void iw()
		{
			lines.SetText("One two three");
			Put(5, 0).Press("diw").AssertText("One  three");
			AssertSelection().Both(4, 0);
		}
		
		[Test]
		public void aw()
		{
			lines.SetText("One two three");
			Put(5, 0).Press("daw").AssertText("One three");
			AssertSelection().Both(4, 0);
		}
		
		[Test]
		public void s()
		{
			lines.SetText("01234567");
			Put(6, 0).Press("s").AssertSelection().Both(6, 0);
			Press("AB").PressCommandMode().AssertText("012345AB7");
			AssertSelection().Both(7, 0);
		}
		
		[Test]
		public void s_Repeat()
		{
			lines.SetText("01234567");
			Put(2, 0).Press("3sAB").PressCommandMode().AssertText("01AB567");
			AssertSelection().Both(3, 0);
		}
		
		[Test]
		public void dd()
		{
			lines.SetText(
				"Darf ich leben ohne Grenzen?\n" +
				"Nein, das darfst du nicht\n" +
				"Lieben trotz der Konsequenzen\n" +
				"Nein, das darfst du nicht");
			Put(2, 1).Press("dd").PressCommandMode().AssertText(
				"Darf ich leben ohne Grenzen?\n" +
				"Lieben trotz der Konsequenzen\n" +
				"Nein, das darfst du nicht");
			AssertSelection().Both(0, 1);
			Assert.AreEqual("Nein, das darfst du nicht\n", ClipboardExecutor.GetFromRegister('\0'));
			
			Put(2, 1).Press("\"ddd").PressCommandMode().AssertText(
				"Darf ich leben ohne Grenzen?\n" +
				"Nein, das darfst du nicht");
			Assert.AreEqual("Lieben trotz der Konsequenzen\n", ClipboardExecutor.GetFromRegister('d'));
		}
		
		[Test]
		public void dd_Repeat()
		{
			lines.SetText(
				"Darf ich leben ohne Grenzen?\n" +
				"Nein, das darfst du nicht\n" +
				"Lieben trotz der Konsequenzen\n" +
				"Nein, das darfst du nicht");
			Put(2, 1).Press("2dd").PressCommandMode().AssertText(
				"Darf ich leben ohne Grenzen?\n" +
				"Nein, das darfst du nicht");
			AssertSelection().Both(0, 1);
			Assert.AreEqual(
				"Nein, das darfst du nicht\n" +
				"Lieben trotz der Konsequenzen\n",
				ClipboardExecutor.GetFromRegister('\0'));
		}
		
		[Test]
		public void dd_Repeat_Overflow()
		{
			lines.SetText(
				"Darf ich leben ohne Grenzen?\n" +
				"Nein, das darfst du nicht\n" +
				"Lieben trotz der Konsequenzen\n" +
				"Nein, das darfst du nicht");
			Put(2, 2).Press("3dd").PressCommandMode().AssertText(
				"Darf ich leben ohne Grenzen?\n" +
				"Nein, das darfst du nicht");
			AssertSelection().Both(0, 1);
			Assert.AreEqual(
				"Lieben trotz der Konsequenzen\n" +
				"Nein, das darfst du nicht\n",
				ClipboardExecutor.GetFromRegister('\0'));
		}
		
		[Test]
		public void dd_Indented()
		{
			lines.SetText(
				"Darf ich leben ohne Grenzen?\n" +
				"Nein, das darfst du nicht\n" +
				"    Lieben trotz der Konsequenzen\n" +
				"    Nein, das darfst du nicht");
			Put(2, 1).Press("dd").PressCommandMode().AssertText(
				"Darf ich leben ohne Grenzen?\n" +
				"    Lieben trotz der Konsequenzen\n" +
				"    Nein, das darfst du nicht");
			AssertSelection().Both(4, 1);
		}
		
		[Test]
		public void dd_EndLine_SeveralLines()
		{
			lines.SetText(
				"Darf ich leben ohne Grenzen?\n" +
				"Nein, das darfst du nicht\n" +
				"Lieben trotz der Konsequenzen\n" +
				"Nein, das darfst du nicht");
			Put(3, 2).Press("2dd").PressCommandMode().AssertText(
				"Darf ich leben ohne Grenzen?\n" +
				"Nein, das darfst du nicht");
			AssertSelection().Both(0, 1);
			Assert.AreEqual(
				"Lieben trotz der Konsequenzen\n" +
				"Nein, das darfst du nicht\n",
				ClipboardExecutor.GetFromRegister('\0'));
		}
		
		[Test]
		public void dd_EndLine_SingleLine()
		{
			lines.SetText("Darf ich leben ohne Grenzen?");
			Put(3, 0).Press("2dd").PressCommandMode().AssertText("");
			AssertSelection().Both(0, 0);
			Assert.AreEqual("Darf ich leben ohne Grenzen?\n",
				ClipboardExecutor.GetFromRegister('\0'));
		}
		
		[Test]
		public void p_Lines()
		{
			lines.SetText(
				"Mein Leben könnte gar nicht besser sein\n" +
				"    Und plötzlich läufst du mir ins Messer rein\n" +
				"    Du sagst zum Leben fehlt mir der Mut");
			ClipboardExecutor.PutToRegister('\0', " aaaaaa\n");
			Put(10, 1).Press("p").AssertText(
				"Mein Leben könnte gar nicht besser sein\n" +
				"    Und plötzlich läufst du mir ins Messer rein\n" +
				" aaaaaa\n" +
				"    Du sagst zum Leben fehlt mir der Mut");
			AssertSelection().Both(1, 2);
		}
		
		[Test]
		public void p_Lines_Multiline()
		{
			lines.SetText(
				"Mein Leben könnte gar nicht besser sein\n" +
				"    Und plötzlich läufst du mir ins Messer rein\n" +
				"    Du sagst zum Leben fehlt mir der Mut");
			ClipboardExecutor.PutToRegister('\0', " aaaaaa\nbbbbbbbb\n");
			Put(10, 1).Press("p").AssertText(
				"Mein Leben könnte gar nicht besser sein\n" +
				"    Und plötzlich läufst du mir ins Messer rein\n" +
				" aaaaaa\n" +
				"bbbbbbbb\n" +
				"    Du sagst zum Leben fehlt mir der Mut");
			AssertSelection().Both(1, 2);
		}
		
		[Test]
		public void P_Lines()
		{
			lines.SetText(
				"Mein Leben könnte gar nicht besser sein\n" +
				"    Und plötzlich läufst du mir ins Messer rein\n" +
				"    Du sagst zum Leben fehlt mir der Mut");
			ClipboardExecutor.PutToRegister('\0', " aaaaaa\nbbbbbbbb\n");
			Put(10, 1).Press("P").AssertText(
				"Mein Leben könnte gar nicht besser sein\n" +
				" aaaaaa\n" +
				"bbbbbbbb\n" +
				"    Und plötzlich läufst du mir ins Messer rein\n" +
				"    Du sagst zum Leben fehlt mir der Mut");
			AssertSelection().Both(1, 1);
		}
		
		[Test]
		public void p_Lines_Repeat()
		{
			lines.SetText(
				"11111\n" +
				"222");
			ClipboardExecutor.PutToRegister('\0', "aa\nbb\n");
			Put(1, 0).Press("pp").AssertText(
				"11111\n" +
				"aa\n" +
				"aa\n" +
				"bb\n" +
				"bb\n" +
				"222");
			AssertSelection().Both(0, 2);
			
			lines.SetText(
				"11111\n" +
				"222");
			ClipboardExecutor.PutToRegister('\0', "aa\nbb\n");
			Put(1, 0).Press("2p").AssertText(
				"11111\n" +
				"aa\n" +
				"bb\n" +
				"aa\n" +
				"bb\n" +
				"222");
			AssertSelection().Both(0, 1);
		}
		
		[Test]
		public void Dot_dw()
		{
			lines.SetText("In meiner Hand ein Bild von dir");
			Put(3, 0).Press("2dw").AssertText("In ein Bild von dir");
			AssertSelection().Both(3, 0);
			Press(".").AssertText("In von dir");
			AssertSelection().Both(3, 0);
		}
		
		[Test]
		public void Dot_cw()
		{
			lines.SetText("In meiner Hand ein Bild von dir");
			Put(3, 0).Press("2cw[_]").AssertText("In [_] ein Bild von dir");
			EscapeNormalViMode();
			AssertSelection().Both(5, 0);
			Press(".").AssertText("In [_[_] Bild von dir");
			AssertSelection().Both(7, 0);
		}
		
		[Test]
		public void Dot_s()
		{
			lines.SetText("In meiner Hand ein Bild von dir");
			Put(3, 0).Press("2sX").AssertText("In Xiner Hand ein Bild von dir");
			EscapeNormalViMode();
			AssertSelection().Both(3, 0);
			Put(14, 0).Press(".").AssertText("In Xiner Hand Xn Bild von dir");
			AssertSelection().Both(14, 0);
		}
		
		[Test]
		public void Dot_df_Repeat()
		{
			lines.SetText("In meiner Hand ein Bild von dir");
			Put(3, 0).Press("dfi").AssertText("In ner Hand ein Bild von dir");
			AssertSelection().Both(3, 0);
			Press("2.").AssertText("In ld von dir");
			AssertSelection().Both(3, 0);
		}
		
		[Test]
		public void Dot_dw_MovesIgnoredByDot()
		{
			lines.SetText("In meiner Hand ein Bild von dir");
			Put(3, 0).Press("dw").AssertText("In Hand ein Bild von dir");
			AssertSelection().Both(3, 0);
			Press("e");
			AssertSelection().Both(6, 0);
			Press(".").AssertText("In Hanein Bild von dir");
		}
		
		[Test]
		public void yy()
		{
			lines.SetText("Oooo\naaaa\nccc\ndddddddd");
			
			Put(1, 1).Press("yy");
			Assert.AreEqual("aaaa\n", ClipboardExecutor.GetFromRegister('\0'));
			AssertSelection().Both(1, 1);
			
			Put(1, 1).Press("2yy");
			Assert.AreEqual("aaaa\nccc\n", ClipboardExecutor.GetFromRegister('\0'));
			AssertSelection().Both(1, 1);
		}
		
		[Test]
		public void xp()
		{
			lines.SetText("abcd");
			
			Put(1, 0).Press("xp");
			AssertText("acbd");
			AssertSelection().Both(2, 0);
		}
		
		[Test]
		public void RemoveLines()
		{
			lines.SetText("Oooo\naaaa\nccc\ndddddddd");
			Put(1, 1).Press("V").Press("j").Press("d");
			AssertText("Oooo\ndddddddd");
		}
		
		[Test]
		public void Shift()
		{
			lines.SetText("Oooo\naaaa\n\tccc\ndddddddd");
			Put(1, 1).Press(">>");
			AssertText("Oooo\n\taaaa\n\tccc\ndddddddd");
			Press("<<");
			AssertText("Oooo\naaaa\n\tccc\ndddddddd");
			Press("2<<");
			AssertText("Oooo\naaaa\nccc\ndddddddd");
		}
		
		[Test]
		public void Shift_UndoRedo()
		{
			lines.SetText("Oooo\naaaa\n\tccc\ndddddddd");
			Put(1, 1).Press(">>");
			AssertText("Oooo\n\taaaa\n\tccc\ndddddddd");
			controller.processor.Undo();
			lines.SetText("Oooo\naaaa\n\tccc\ndddddddd");
			controller.processor.Redo();
			AssertText("Oooo\n\taaaa\n\tccc\ndddddddd");
			Press("<<");
			AssertText("Oooo\naaaa\n\tccc\ndddddddd");
			controller.processor.Undo();
			AssertText("Oooo\n\taaaa\n\tccc\ndddddddd");
			controller.processor.Redo();
			AssertText("Oooo\naaaa\n\tccc\ndddddddd");
		}
		
		[Test]
		public void Shift_VISUAL()
		{
			lines.SetText("Oooo\naaaa\n\tccc\ndddddddd");
			Put(1, 1).Press("v").Press("2>");
			AssertText("Oooo\n\t\taaaa\n\tccc\ndddddddd");
		}
		
		[Test]
		public void C()
		{
			lines.SetText("Oooo\naaaa\n\tccc\ndddddddd");
			Put(1, 1).Press("C");
			AssertText("Oooo\na\n\tccc\ndddddddd");
			AssertViClipboard("aaa");
			AssertSelection().Both(1, 1).NoNext();
		}
		
		[Test]
		public void C_Repeat()
		{
			lines.SetText("Oooo\naaaa\n\tccc\ndddddddd");
			Put(1, 1).Press("2").Press("C");
			AssertText("Oooo\na\ndddddddd");
			AssertViClipboard("aaa\n\tccc");
			AssertSelection().Both(1, 1).NoNext();
		}
		
		[Test]
		public void D()
		{
			lines.SetText("Oooo\naaaa\n\tccc\ndddddddd");
			Put(2, 1).Press("D");
			AssertText("Oooo\naa\n\tccc\ndddddddd");
			AssertViClipboard("aa");
			AssertSelection().Both(1, 1).NoNext();
		}
		
		[Test]
		public void D_Repeat()
		{
			lines.SetText("Oooo\naaaa\n\tccc\ndddddddd");
			Put(1, 1).Press("2").Press("D");
			AssertText("Oooo\na\ndddddddd");
			AssertViClipboard("aaa\n\tccc");
			AssertSelection().Both(0, 1).NoNext();
		}
		
		[Test]
		public void cc()
		{
			lines.SetText("Oooo\naaaa\n\tccc\ndddddddd");
			Put(2, 1).Press("cc");
			AssertText("Oooo\n\n\tccc\ndddddddd");
			AssertViClipboard("aaaa\n");
			AssertSelection().Both(0, 1).NoNext();
		}
		
		[Test]
		public void cc_FirstLine()
		{
			lines.SetText("Oooo\naaaa\n\tccc\ndddddddd");
			Put(2, 0).Press("cc");
			AssertText("\naaaa\n\tccc\ndddddddd");
			AssertViClipboard("Oooo\n");
			AssertSelection().Both(0, 0).NoNext();
		}
		
		[Test]
		public void cc_LastLine()
		{
			lines.SetText("Oooo\naaaa\nccc\ndddddddd");
			Put(2, 3).Press("cc");
			AssertText("Oooo\naaaa\nccc\n");
			AssertViClipboard("dddddddd\n");
			AssertSelection().Both(0, 3).NoNext();
		}
	}
}