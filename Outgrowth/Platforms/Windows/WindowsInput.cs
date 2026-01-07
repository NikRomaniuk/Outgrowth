#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Outgrowth.Platforms.Windows;

/// <summary>
/// Windows keyboard input handler. Hooks into Window.Content to capture key events.
/// Supports Left/A, Right/D for navigation, Esc for closing panels.
/// </summary>
public sealed class WindowsInput
{
    private UIElement? _root;
    private readonly Action _onLeftArrow;
    private readonly Action _onRightArrow;
    private readonly Action? _onEscape;
    private readonly Action? _onE;
    private readonly Action? _onQ;

    public WindowsInput(Action onLeftArrow, Action onRightArrow, Action? onEscape = null, Action? onE = null, Action? onQ = null)
    {
        _onLeftArrow = onLeftArrow;
        _onRightArrow = onRightArrow;
        _onEscape = onEscape;
        _onE = onE;
        _onQ = onQ;
    }

    public void Attach()
    {
        if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            is Microsoft.UI.Xaml.Window winWindow
            && winWindow.Content is UIElement root)
        {
            _root = root;
            root.KeyDown += OnKeyDown;
            root.IsTabStop = true;

            // handledEventsToo: true catches events even if children already handled them
            root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnAnyPointer), handledEventsToo: true);
            root.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnAnyPointer), handledEventsToo: true);
            root.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(OnAnyPointer), handledEventsToo: true);

            ForceFocus();
            System.Diagnostics.Debug.WriteLine("[WindowsInput] Attached to Window.Content root");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[WindowsInput] Failed to attach - Window.Content not found");
        }
    }

    public void Detach()
    {
        if (_root != null)
        {
            _root.KeyDown -= OnKeyDown;

            _root.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnAnyPointer));
            _root.RemoveHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnAnyPointer));
            _root.RemoveHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(OnAnyPointer));

            _root = null;
            
            System.Diagnostics.Debug.WriteLine("[WindowsInput] Detached from Window.Content root");
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[WindowsInput] KeyDown: {e.Key}");

        if (e.Key == VirtualKey.Left || e.Key == VirtualKey.A)
        {
            _onLeftArrow?.Invoke();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Right || e.Key == VirtualKey.D)
        {
            _onRightArrow?.Invoke();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            _onEscape?.Invoke();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.E)
        {
            _onE?.Invoke();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Q)
        {
            _onQ?.Invoke();
            e.Handled = true;
        }
    }

    private void OnAnyPointer(object sender, PointerRoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[WindowsInput] Pointer event detected - restoring focus");
        _root?.DispatcherQueue.TryEnqueue(ForceFocus);
    }

    private void ForceFocus()
    {
        if (_root == null) return;

        _root.IsTabStop = true;
        _root.Focus(FocusState.Programmatic);

        System.Diagnostics.Debug.WriteLine($"[WindowsInput] ForceFocus() -> FocusState={_root.FocusState}");
    }
}
#endif

