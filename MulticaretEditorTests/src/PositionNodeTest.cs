using System;
using NUnit.Framework;
using MulticaretEditor;

namespace UnitTests
{
	[TestFixture]
	public class PositionNodeTest
	{
		private MacrosExecutor _executor;
		
		private void AssertPosition(string expectedFile, int expectedPosition, PositionNode was)
		{
			Assert.AreEqual(expectedFile + ":" + expectedPosition, was != null ? was.file.path + ":" + was.position : "null");
		}
		
		[SetUp]
		public void SetUp()
		{
			_executor = new MacrosExecutor(null, 3);
			_executor.ViSetCurrentFile("File0");
		}
		
		[Test]
		public void Simple()
		{
			_executor.ViPositionAdd(1);
			_executor.ViSetCurrentFile("File1");
			_executor.ViPositionAdd(2);
			AssertPosition("File0", 1, _executor.ViPositionPrev());
			AssertPosition("File1", 2, _executor.ViPositionNext());
			AssertPosition("File0", 1, _executor.ViPositionPrev());
		}
		
		[Test]
		public void Simple_DontFailIfNoOneElement()
		{
			Assert.AreEqual(null, _executor.ViPositionPrev());
			Assert.AreEqual(null, _executor.ViPositionNext());
		}
		
		[Test]
		public void WorksAfterMaxCountReached()
		{
			_executor.ViPositionAdd(1);
			_executor.ViPositionAdd(2);
			_executor.ViPositionAdd(3);
			_executor.ViPositionAdd(4);
			AssertPosition("File0", 3, _executor.ViPositionPrev());
			AssertPosition("File0", 2, _executor.ViPositionPrev());
			AssertPosition("File0", 3, _executor.ViPositionNext());
			AssertPosition("File0", 4, _executor.ViPositionNext());
		}
		
		[Test]
		public void NullAfterMaxCountOverflow()
		{
			_executor.ViPositionAdd(1);
			_executor.ViPositionAdd(2);
			_executor.ViPositionAdd(3);
			_executor.ViPositionAdd(4);
			AssertPosition("File0", 3, _executor.ViPositionPrev());
			AssertPosition("File0", 2, _executor.ViPositionPrev());
			Assert.AreEqual(null, _executor.ViPositionPrev());
			Assert.AreEqual(null, _executor.ViPositionPrev());
			AssertPosition("File0", 3, _executor.ViPositionNext());
			AssertPosition("File0", 4, _executor.ViPositionNext());
			Assert.AreEqual(null, _executor.ViPositionNext());
		}
		
		[Test]
		public void PositionHistory()
		{
			_executor.ViPositionAdd(1);
			_executor.ViPositionAdd(2);
			_executor.ViPositionAdd(3);
			Assert.AreEqual(3, _executor.positionHistory.Length);
			AssertPosition("File0", 1, _executor.positionHistory[0]);
			AssertPosition("File0", 2, _executor.positionHistory[1]);
			AssertPosition("File0", 3, _executor.positionHistory[2]);
			
			AssertPosition("File0", 2, _executor.ViPositionPrev());
			AssertPosition("File0", 1, _executor.ViPositionPrev());
			AssertPosition("File0", 1, _executor.positionHistory[0]);
			AssertPosition("File0", 2, _executor.positionHistory[1]);
			AssertPosition("File0", 3, _executor.positionHistory[2]);
			_executor.ViPositionAdd(4);
			AssertPosition("File0", 1, _executor.positionHistory[0]);
			AssertPosition("File0", 4, _executor.positionHistory[1]);
			Assert.AreEqual(null, _executor.positionHistory[2]);
		}
	}
}
