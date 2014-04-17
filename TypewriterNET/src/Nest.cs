using System.Collections.Generic;
using System.Drawing;
using System;

public class Nest : NestBase
{
	public Nest Child { get { return child; } }
	public Nest Parent { get { return parent; } }
	public NestList Owner { get { return owner; } }

	public bool hDivided;
	public bool left;
	public bool isPercents = true;
	public int size = 50;

	public Size minSize;
	public Size selfMinSize;
	public int FullWidth;
	public int FullHeight;

	public Size frameSize;
	public Size FrameSize { get { return frameSize; } }

	private readonly MainForm mainForm;
	public MainForm MainForm { get { return mainForm; } }

	public Nest(MainForm mainForm)
	{
		this.mainForm = mainForm;
	}

	private AFrame frame;
	public AFrame AFrame
	{
		get { return frame; }
		set
		{
			if (frame != value)
			{
				if (frame != null)
				{
					frame.SetNest(null);
					if (frame.Parent == mainForm)
						mainForm.Controls.Remove(frame);
				}
				frame = value;
				if (frame != null)
				{
					frame.SetNest(this);
					if (owner != null && frame.Parent != mainForm)
						mainForm.Controls.Add(frame);
				}
			}
		}
	}

	override protected void DoOnOwnerChange()
	{
		if (frame != null)
		{
			if (owner != null)
			{
				mainForm.Controls.Add(frame);
			}
			else if (frame.Parent == mainForm)
			{
				mainForm.Controls.Remove(frame);
			}
		}
	}

	public Frame Frame
	{
		get { return frame as Frame; }
	}

	public int GetSize(int nestSize)
	{
		return isPercents ? nestSize * size / 100 : size;
	}

	public void SetFrameSize(Size size)
	{
		this.frameSize = size;
		frame.Size = size;
	}

	public void Update()
	{
		selfMinSize = frame.MinSize;
		if (child != null)
		{
			child.Update();
			minSize = hDivided ?
				new Size(selfMinSize.Width + child.minSize.Width, Math.Max(selfMinSize.Height, child.minSize.Height)) :
				new Size(Math.Max(selfMinSize.Width, child.minSize.Width), selfMinSize.Height + child.minSize.Height);
		}
		else
		{
			minSize = selfMinSize;
		}
	}

	public void Resize(int x, int y, int width, int height)
	{
		if (width < minSize.Width)
			width = minSize.Width;
		if (height < minSize.Height)
			height = minSize.Height;
		PrivateResize(x, y, width, height);
	}

	private void PrivateResize(int x, int y, int width, int height)
	{
		FullWidth = width;
		FullHeight = height;
		if (child != null)
		{
			if (hDivided)
			{
				int size = GetSize(width);
				if (size < selfMinSize.Width)
					size = selfMinSize.Width;
				else if (width - size < child.minSize.Width)
					size = width - child.minSize.Width;
				SetFrameSize(new Size(size, height));
				if (left)
				{
					frame.Location = new Point(x, y);
					child.PrivateResize(x + size, y, width - size, height);
				}
				else
				{
					frame.Location = new Point(x + (width - size), y);
					child.PrivateResize(x, y, width - size, height);
				}
			}
			else
			{
				int size = GetSize(height);
				if (size < selfMinSize.Height)
					size = selfMinSize.Height;
				else if (height - size < child.minSize.Height)
					size = height - child.minSize.Height;
				SetFrameSize(new Size(width, size));
				if (left)
				{
					frame.Location = new Point(x, y);
					child.PrivateResize(x, y + size, width, height - size);
				}
				else
				{
					frame.Location = new Point(x, y + (height - size));
					child.PrivateResize(x, y, width, height - size);
				}
			}
		}
		else
		{
			frame.Location = new Point(x, y);
			SetFrameSize(new Size(width, height));
		}
	}
}
