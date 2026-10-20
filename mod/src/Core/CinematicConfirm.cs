using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace P3Makoto.Core;

/// <summary>Modal cinematic confirmation using the game's actual actions, with scoped cleanup.</summary>
internal sealed class CinematicConfirm : IDisposable
{
	private static readonly string[] Actions = { "ui_select", "ui_confirm", "ui_accept" };
	private readonly Control _focus;
	private readonly Control? _previous;
	private readonly NHotkeyManager? _hotkeys;
	private readonly Action _pressed, _released;
	private Action? _confirm;
	private ulong _readyAt;
	private bool _disposed;

	public CinematicConfirm(Node lifetime)
	{
		_focus = new Control { Name = "P3CinematicFocus", FocusMode = Control.FocusModeEnum.All,
			MouseFilter = Control.MouseFilterEnum.Ignore };
		lifetime.AddChild(_focus);
		_previous = _focus.GetViewport().GuiGetFocusOwner();
		NodePath self = _focus.GetPath();
		_focus.FocusNeighborLeft = _focus.FocusNeighborRight = _focus.FocusNeighborTop = _focus.FocusNeighborBottom = self;
		_focus.FocusNext = _focus.FocusPrevious = self;
		_focus.GrabFocus();
		_pressed = Confirm;
		_released = () => { };
		_hotkeys = NHotkeyManager.Instance;
		_hotkeys?.AddBlockingScreen(_focus);
		foreach (string action in Actions)
		{
			if (!InputMap.HasAction(action)) continue;
			_hotkeys?.PushHotkeyPressedBinding(action, _pressed);
			_hotkeys?.PushHotkeyReleasedBinding(action, _released);
		}
		_focus.GuiInput += ev =>
		{
			foreach (string action in Actions)
			{
				if (InputMap.HasAction(action) && ev.IsActionPressed(action) && !ev.IsEcho())
				{
					_focus.AcceptEvent();
					Confirm();
					return;
				}
			}
		};
		lifetime.TreeExiting += Dispose;
	}

	public void Arm(Action confirm)
	{
		_confirm = confirm;
		_readyAt = Time.GetTicksMsec() + 400;
	}

	public void Disarm() => _confirm = null;

	private void Confirm()
	{
		if (_disposed || Time.GetTicksMsec() < _readyAt) return;
		Action? confirm = _confirm;
		_confirm = null; // one action even when a button maps to several aliases
		if (confirm != null) ModEntry.Safe("cinematic confirm", confirm);
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_confirm = null;
		if (_hotkeys != null && GodotObject.IsInstanceValid(_hotkeys))
		{
			foreach (string action in Actions)
			{
				if (!InputMap.HasAction(action)) continue;
				_hotkeys.RemoveHotkeyPressedBinding(action, _pressed);
				_hotkeys.RemoveHotkeyReleasedBinding(action, _released);
			}
			_hotkeys.RemoveBlockingScreen(_focus);
		}
		if (GodotObject.IsInstanceValid(_focus))
		{
			_focus.ReleaseFocus();
			_focus.FocusMode = Control.FocusModeEnum.None;
		}
		if (_previous != null && GodotObject.IsInstanceValid(_previous) && _previous.IsInsideTree()
			&& _previous.IsVisibleInTree()) _previous.GrabFocus();
	}
}
