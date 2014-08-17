using System;
using System.Drawing;
using System.Collections.Generic;

public class FrameList
{
	private readonly MainForm mainForm;
	private readonly NestList list;

	public FrameList(MainForm mainForm)
	{
		this.mainForm = mainForm;
		list = new NestList(mainForm);
	}

	public bool NeedResize
	{
		get { return list.needResize; }
		set { list.needResize = value; }
	}

	public void Resize(int x, int y, Size size)
	{
		if (list.Head != null)
		{
			list.Head.Update();
			list.Head.Resize(x, y, size.Width, size.Height);
		}
	}

	public Nest AddParentNode()
	{
		return list.AddParent();
	}

	public Frame GetFocusedFrame()
	{
		for (Nest nestI = list.Head; nestI != null; nestI = nestI.Child)
		{
			if (nestI.Frame != null && nestI.Frame.Focused)
				return nestI.Frame;
		}
		return null;
	}

	public Buffer GetSelectedBuffer(BufferTag tags)
	{
		Frame frame = GetFocusedFrame();
		if (frame != null)
		{
			for (Nest nestI = frame.Nest; nestI != null; nestI = nestI.Child)
			{
				if (nestI.Frame != null && nestI.Frame.SelectedBuffer != null &&
					(nestI.Frame.SelectedBuffer.tags & tags) == tags)
					return nestI.Frame.SelectedBuffer;
			}
		}
		return null;
	}

	public Frame GetFirstFrame()
	{
		for (Nest nestI = list.Head; nestI != null; nestI = nestI.Child)
		{
			if (nestI.Frame != null)
				return nestI.Frame;
		}
		return null;
	}

	public Frame GetChildFrame(Frame frame)
	{
		Nest nest = frame != null ? frame.Nest : null;
		if (nest != null)
		{
			for (Nest nestI = nest.Child; nestI != null; nestI = nestI.Child)
			{
				if (nestI.Frame != null)
					return nestI.Frame;
			}
		}
		return null;
	}

	public void UpdateSettings(Settings settings, UpdatePhase phase)
	{
		for (Nest nestI = list.Head; nestI != null; nestI = nestI.Child)
		{
			if (nestI.AFrame != null)
				nestI.AFrame.UpdateSettings(settings, phase);
		}
	}

	public IEnumerable<Buffer> GetBuffers(BufferTag tags)
	{
		for (Nest nestI = list.Head; nestI != null; nestI = nestI.Child)
		{
			if (nestI.buffers != null)
			{
				for (int i = 0, count = nestI.buffers.list.Count; i < count; i++)
				{
					Buffer buffer = nestI.buffers.list[i];
					if ((buffer.tags & tags) == tags)
						yield return buffer;
				}
			}
		}
	}
}
