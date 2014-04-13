using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Text;
using System.Diagnostics;
using Microsoft.Win32;
using MulticaretEditor.KeyMapping;
using MulticaretEditor.Highlighting;
using MulticaretEditor;

public class AFrame : Control
{
	public AFrame()
	{
		TabStop = false;
	}

	private Control top;
	private Control right;

	protected void InitResizing(Control top, Control right)
	{
		this.top = top;
		this.right = right;
		top.MouseDown += OnTabBarMouseDown;
		top.MouseUp += OnTabBarMouseUp;
		right.MouseDown += OnSplitLineMouseDown;
		right.MouseUp += OnSplitLineMouseUp;
	}

	protected Nest nest;
	public Nest Nest { get { return nest; } }

	public void SetNest(Nest nest)
	{
		this.nest = nest;
	}

	virtual public Size MinSize { get { return new Size(100, 100); } }

	//--------------------------------------------------------------------------
	// Resizing X
	//--------------------------------------------------------------------------

	private int startX;
	private int startSizeX;
	private int startWidth;

	private void OnSplitLineMouseDown(object sender, MouseEventArgs e)
	{
		if (nest == null)
			return;
		Nest target = FindWidthTarget();
		if (target != null)
		{
			startX = Control.MousePosition.X;
			startSizeX = target.size;
			startWidth = target.frameSize.Width;
			right.MouseMove += OnSplitLineMouseMove;
		}
	}

	private void OnSplitLineMouseUp(object sender, MouseEventArgs e)
	{
		right.MouseMove -= OnSplitLineMouseMove;
	}

	private void OnSplitLineMouseMove(object sender, MouseEventArgs e)
	{
		if (nest == null)
			return;
		Nest target = FindWidthTarget();
		if (target != null)
		{
			int k = target.left ? -1 : 1;
			target.frameSize.Width = startWidth + k * (startX - Control.MousePosition.X);
			target.size = target.isPercents ? 100 * target.frameSize.Width / target.FullWidth : target.frameSize.Width;
			if (target.size < 0)
				target.size = 0;
			else if (target.isPercents && target.size > 100)
				target.size = 100;
			target.MainForm.DoResize();
		}
	}

	private Nest FindWidthTarget()
	{
		if (nest == null)
			return null;
		if (nest.hDivided && nest.left)
			return nest;
		for (Nest nestI = nest.parent; nestI != null; nestI = nestI.parent)
		{
			if (nestI.hDivided && !nestI.left)
				return nestI;
		}
		return null;
	}

	//--------------------------------------------------------------------------
	// Resizing Y
	//--------------------------------------------------------------------------

	private int startY;
	private int startSizeY;
	private int startHeight;

	private void OnTabBarMouseDown(object sender, MouseEventArgs e)
	{
		if (nest == null)
			return;
		Nest target = FindHeightTarget();
		if (target != null)
		{
			startY = Control.MousePosition.Y;
			startSizeY = target.size;
			startHeight = target.frameSize.Height;
			top.MouseMove += OnTabBarMouseMove;
		}
	}

	private void OnTabBarMouseUp(object sender, MouseEventArgs e)
	{
		top.MouseMove -= OnTabBarMouseMove;
	}

	private void OnTabBarMouseMove(object sender, MouseEventArgs e)
	{
		if (nest == null)
			return;
		Nest target = FindHeightTarget();
		if (target != null)
		{
			int k = target.left ? -1 : 1;
			target.frameSize.Height = startHeight + k * (startY - Control.MousePosition.Y);
			target.size = target.isPercents ? 100 * target.frameSize.Height / target.FullHeight : target.frameSize.Height;
			if (target.size < 0)
				target.size = 0;
			else if (target.isPercents && target.size > 100)
				target.size = 100;
			target.MainForm.DoResize();
		}
	}

	private Nest FindHeightTarget()
	{
		if (nest == null)
			return null;
		if (!nest.hDivided && !nest.left)
			return nest;
		for (Nest nestI = nest.parent; nestI != null; nestI = nestI.parent)
		{
			if (!nestI.hDivided && nestI.left)
				return nestI;
		}
		return null;
	}

	//--------------------------------------------------------------------------
	//
	//--------------------------------------------------------------------------
}
