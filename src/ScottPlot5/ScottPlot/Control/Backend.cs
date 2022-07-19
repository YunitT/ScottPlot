﻿using ScottPlot.Control.EventArgs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ScottPlot.Control
{
    /// <summary>
    /// This class is intended to be instantiated in user controls
    /// and provides a convenient way to manage events and map events to actions.
    /// </summary>
    /// <typeparam name="T">The specific type of user control using this backend</typeparam>
    public class Backend<T> where T : IPlotControl
    {
        private readonly T Sender;

        private Plot Plot => Sender.Plot;

        private readonly HashSet<Key> CurrentlyPressedKeys = new();

        private Pixel? LastMousePosition = null;

        private readonly Dictionary<MouseButton, MouseDownEventArgs?> MouseInteractions = new();

        public delegate void MouseDownHandler<U, V>(U sender, V eventArgs);
        public delegate void MouseUpHandler<U, V>(U sender, V eventArgs);
        public delegate void MouseMoveHandler<U, V>(U sender, V eventArgs);
        public delegate void MouseDragHandler<U, V>(U sender, V eventArgs);
        public delegate void MouseDragEndHandler<U, V>(U sender, V eventArgs);
        public delegate void DoubleClickHandler<U, V>(U sender, V eventArgs);
        public delegate void MouseWheelHandler<U, V>(U sender, V eventArgs);
        public delegate void KeyDownHandler<U, V>(U sender, V eventArgs);
        public delegate void KeyUpHandler<U, V>(U sender, V eventArgs);

        public event MouseDownHandler<T, MouseDownEventArgs> MouseDown;
        public event MouseUpHandler<T, MouseUpEventArgs> MouseUp;
        public event MouseMoveHandler<T, MouseMoveEventArgs> MouseMove;
        public event MouseDragHandler<T, MouseDragEventArgs> MouseDrag;
        public event MouseDragEndHandler<T, MouseDragEventArgs> MouseDragEnd;
        public event DoubleClickHandler<T, MouseDownEventArgs> DoubleClick;
        public event MouseWheelHandler<T, MouseWheelEventArgs> MouseWheel;
        public event KeyDownHandler<T, KeyDownEventArgs> KeyDown;
        public event KeyUpHandler<T, KeyUpEventArgs> KeyUp;

        public float MinimumDragDistance = 5;

        public Backend(T sender)
        {
            Sender = sender;

            MouseDown += (T sender, MouseDownEventArgs e) => DefaultEventHandlers.MouseDown(sender, e);
            MouseUp += (T sender, MouseUpEventArgs e) => DefaultEventHandlers.MouseUp(sender, e);
            MouseMove += (T sender, MouseMoveEventArgs e) => DefaultEventHandlers.MouseMove(sender, e);
            MouseDrag += (T sender, MouseDragEventArgs e) => DefaultEventHandlers.MouseDrag(sender, e);
            MouseDragEnd += (T sender, MouseDragEventArgs e) => DefaultEventHandlers.MouseDragEnd(sender, e);
            DoubleClick += (T sender, MouseDownEventArgs e) => DefaultEventHandlers.DoubleClick(sender, e);
            MouseWheel += (T sender, MouseWheelEventArgs e) => DefaultEventHandlers.MouseWheel(sender, e);
            KeyDown += (T sender, KeyDownEventArgs e) => DefaultEventHandlers.KeyDown(sender, e);
            KeyUp += (T sender, KeyUpEventArgs e) => DefaultEventHandlers.KeyUp(sender, e);
        }

        public IReadOnlyCollection<Key> PressedKeys => CurrentlyPressedKeys.ToArray();

        public IEnumerable<MouseButton> PressedMouseButtons => MouseInteractions.Keys.Where(button => MouseInteractions[button] is not null);

        private MouseDownEventArgs? GetMouseInteraction(MouseButton button) => MouseInteractions.ContainsKey(button) ? MouseInteractions[button] : null;

        private void SetMouseInteraction(MouseButton button, MouseDownEventArgs? value) => MouseInteractions[button] = value;

        public Coordinate? MouseCoordinates => LastMousePosition.HasValue ? Plot.GetCoordinate(LastMousePosition.Value) : null;

        public bool IsDrag(Pixel from, Pixel to) => (from - to).Hypotenuse > MinimumDragDistance;

        public void TriggerMouseDown(Pixel position, MouseButton button)
        {
            var interaction = new MouseDownEventArgs(position, button, Plot.GetAxisLimits(), PressedKeys);
            SetMouseInteraction(button, interaction);
            MouseDown?.Invoke(Sender, interaction);
        }

        public void TriggerDoubleClick(Pixel position, MouseButton button)
        {
            DoubleClick?.Invoke(Sender, new(position, button, Plot.GetAxisLimits(), PressedKeys));
        }

        public void TriggerMouseUp(Pixel position, MouseButton button)
        {
            bool cancelledDrag = false;
            var interaction = GetMouseInteraction(button);
            if (interaction is not null && IsDrag(interaction.Position, position))
            {
                TriggerMouseDragEnd(interaction, position, button);
                cancelledDrag = true;
            }

            SetMouseInteraction(button, null);
            MouseUp?.Invoke(Sender, new(position, button, Plot.GetAxisLimits(), cancelledDrag));
        }

        public void TriggerMouseMove(Pixel position)
        {
            LastMousePosition = position;
            MouseMove?.Invoke(Sender, new(position, PressedKeys));

            for (MouseButton button = MouseButton.Mouse1; button <= MouseButton.Mouse3; button++)
            {
                var interaction = GetMouseInteraction(button);
                if (interaction is not null)
                {
                    var lastMouseDown = interaction;
                    if (IsDrag(lastMouseDown.Position, position))
                    {
                        TriggerMouseDrag(lastMouseDown, position, button);
                    }
                    else if (Plot.ZoomRectangle.IsVisible)
                    {
                        Plot.MouseZoomRectangleClear(applyZoom: false);
                    }
                }
            }
        }

        public void TriggerMouseWheel(Pixel position, float deltaX, float deltaY)
        {
            MouseWheel?.Invoke(Sender, new(position, deltaX, deltaY));
        }

        public void TriggerKeyDown(Key key)
        {
            CurrentlyPressedKeys.Add(key);
            KeyDown?.Invoke(Sender, new(key));
        }

        public void TriggerKeyUp(Key key)
        {
            CurrentlyPressedKeys.Remove(key);
            KeyUp?.Invoke(Sender, new(key));
        }

        private void TriggerMouseDrag(MouseDownEventArgs MouseDown, Pixel to, MouseButton button)
        {
            MouseDrag?.Invoke(Sender, new(MouseDown, to, button));
        }

        private void TriggerMouseDragEnd(MouseDownEventArgs MouseDown, Pixel to, MouseButton button)
        {
            MouseDragEnd?.Invoke(Sender, new(MouseDown, to, button));
        }
    }
}
