using System;
using System.Collections.Generic;
using NUnit.Framework;
using MulticaretEditor;
using System.Windows.Forms;

namespace UnitTests
{
	[TestFixture]
	public class ViCommandParserTest
	{
		private ViCommandParser parser;
		
		[SetUp]
		public void SetUp()
		{
			parser = new ViCommandParser();
		}
		
		private bool AddKey(char c)
		{
			return parser.AddKey(new ViChar(c, false));
		}
		
		private bool AddKey(char c, bool control)
		{
			return parser.AddKey(new ViChar(c, control));
		}
		
		private void AssertParsed(string expected)
		{
			Assert.AreEqual(expected,
				parser.count + ":action:" + parser.action + ";move:" + parser.move + ";moveChar:" + parser.moveChar);
		}
		
		[Test]
		public void Move_hjkl()
		{
			Assert.AreEqual(true, AddKey('h'));
			AssertParsed("1:action:\\0;move:h;moveChar:\\0");
			
			Assert.AreEqual(true, AddKey('j'));
			AssertParsed("1:action:\\0;move:j;moveChar:\\0");
			
			Assert.AreEqual(true, AddKey('k'));
			AssertParsed("1:action:\\0;move:k;moveChar:\\0");
			
			Assert.AreEqual(true, AddKey('l'));
			AssertParsed("1:action:\\0;move:l;moveChar:\\0");
		}
		
		[Test]
		public void Move_repeat_hj()
		{
			Assert.AreEqual(false, AddKey('2'));
			Assert.AreEqual(true, AddKey('h'));
			AssertParsed("2:action:\\0;move:h;moveChar:\\0");
			
			Assert.AreEqual(false, AddKey('5'));
			Assert.AreEqual(false, AddKey('0'));
			Assert.AreEqual(true, AddKey('j'));
			AssertParsed("50:action:\\0;move:j;moveChar:\\0");
			
			Assert.AreEqual(false, AddKey('2'));
			Assert.AreEqual(false, AddKey('3'));
			Assert.AreEqual(false, AddKey('5'));
			Assert.AreEqual(true, AddKey('j'));
			AssertParsed("235:action:\\0;move:j;moveChar:\\0");
		}
		
		[Test]
		public void Repeat_delete_hjkl()
		{
			Assert.AreEqual(false, AddKey('d'));
			Assert.AreEqual(true, AddKey('h'));
			AssertParsed("1:action:d;move:h;moveChar:\\0");
			
			Assert.AreEqual(false, AddKey('7'));
			Assert.AreEqual(false, AddKey('d'));
			Assert.AreEqual(true, AddKey('j'));
			AssertParsed("7:action:d;move:j;moveChar:\\0");
			
			Assert.AreEqual(false, AddKey('3'));
			Assert.AreEqual(false, AddKey('5'));
			Assert.AreEqual(false, AddKey('d'));
			Assert.AreEqual(true, AddKey('k'));
			AssertParsed("35:action:d;move:k;moveChar:\\0");
			
			Assert.AreEqual(false, AddKey('d'));
			Assert.AreEqual(true, AddKey('l'));
			AssertParsed("1:action:d;move:l;moveChar:\\0");
		}
		
		[Test]
		public void Repeat_delete_h_inversed()
		{
			Assert.AreEqual(false, AddKey('d'));
			Assert.AreEqual(true, AddKey('h'));
			AssertParsed("1:action:d;move:h;moveChar:\\0");
			
			Assert.AreEqual(false, AddKey('d'));
			Assert.AreEqual(false, AddKey('7'));
			Assert.AreEqual(true, AddKey('h'));
			AssertParsed("7:action:d;move:h;moveChar:\\0");
			
			Assert.AreEqual(false, AddKey('3'));
			Assert.AreEqual(false, AddKey('d'));
			Assert.AreEqual(false, AddKey('5'));
			Assert.AreEqual(true, AddKey('h'));
			AssertParsed("5:action:d;move:h;moveChar:\\0");
		}
		
		[Test]
		public void UndoRedo()
		{
			Assert.AreEqual(true, AddKey('u'));
			AssertParsed("1:action:u;move:\\0;moveChar:\\0");
			
			Assert.AreEqual(false, AddKey('7'));
			Assert.AreEqual(true, AddKey('u'));
			AssertParsed("7:action:u;move:\\0;moveChar:\\0");
			
			Assert.AreEqual(true, AddKey('r', true));
			AssertParsed("1:action:<C-r>;move:\\0;moveChar:\\0");
			
			Assert.AreEqual(false, AddKey('2'));
			Assert.AreEqual(false, AddKey('0'));
			Assert.AreEqual(true, AddKey('r', true));
			AssertParsed("20:action:<C-r>;move:\\0;moveChar:\\0");
		}
		
		[Test]
		public void Move_toChar()
		{
			Assert.AreEqual(false, AddKey('f'));
			Assert.AreEqual(true, AddKey('a'));
			AssertParsed("1:action:\\0;move:f;moveChar:a");
			
			Assert.AreEqual(false, AddKey('f'));
			Assert.AreEqual(true, AddKey('2'));
			AssertParsed("1:action:\\0;move:f;moveChar:2");
			
			Assert.AreEqual(false, AddKey('f'));
			Assert.AreEqual(true, AddKey('0'));
			AssertParsed("1:action:\\0;move:f;moveChar:0");
		}
		
		[Test]
		public void Repeat_delete_toChar()
		{
			Assert.AreEqual(false, AddKey('2'));
			Assert.AreEqual(false, AddKey('f'));
			Assert.AreEqual(true, AddKey('a'));
			AssertParsed("2:action:\\0;move:f;moveChar:a");
			
			Assert.AreEqual(false, AddKey('3'));
			Assert.AreEqual(false, AddKey('4'));
			Assert.AreEqual(false, AddKey('f'));
			Assert.AreEqual(true, AddKey('a'));
			AssertParsed("34:action:\\0;move:f;moveChar:a");
			
			Assert.AreEqual(false, AddKey('4'));
			Assert.AreEqual(false, AddKey('9'));
			Assert.AreEqual(false, AddKey('f'));
			Assert.AreEqual(true, AddKey('2'));
			AssertParsed("49:action:\\0;move:f;moveChar:2");
			
			Assert.AreEqual(false, AddKey('5'));
			Assert.AreEqual(false, AddKey('1'));
			Assert.AreEqual(false, AddKey('d'));
			Assert.AreEqual(false, AddKey('f'));
			Assert.AreEqual(true, AddKey('2'));
			AssertParsed("51:action:d;move:f;moveChar:2");
		}
		
		[Test]
		public void Repeat_delete_toChar_reversed()
		{
			Assert.AreEqual(false, AddKey('3'));
			Assert.AreEqual(false, AddKey('d'));
			Assert.AreEqual(false, AddKey('5'));
			Assert.AreEqual(false, AddKey('1'));
			Assert.AreEqual(false, AddKey('f'));
			Assert.AreEqual(true, AddKey('2'));
			AssertParsed("51:action:d;move:f;moveChar:2");
		}
		
		[Test]
		public void Move_word()
		{
			Assert.AreEqual(true, AddKey('w'));
			AssertParsed("1:action:\\0;move:w;moveChar:\\0");
			
			Assert.AreEqual(false, AddKey('2'));
			Assert.AreEqual(false, AddKey('d'));
			Assert.AreEqual(true, AddKey('w'));
			AssertParsed("2:action:d;move:w;moveChar:\\0");
			
			Assert.AreEqual(false, AddKey('d'));
			Assert.AreEqual(true, AddKey('b'));
			AssertParsed("1:action:d;move:b;moveChar:\\0");
		}
	}
}
