// Ported from Logical2.GraphCore, rewritten for Soup: instead of
// mutating view selection directly, clicks dispatch SelectElementsCommand.
// The dispatcher applies it to SelectionState synchronously and the observer
// reflects it back onto the views before the gesture continues, so
// click-then-drag still sees up-to-date selection.
using System.Collections.Generic;
using Soup.Editor.Core.Commands.CommandDefinitions;
using Soup.Editor.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Manipulators
{
    public class SelectableManipulator : MouseManipulator
    {
        private GraphElement m_Element;

        public SelectableManipulator()
        {
            activators.Add(new() { button = MouseButton.LeftMouse });
            activators.Add(new() { button = MouseButton.LeftMouse, modifiers = EventModifiers.Shift });
            activators.Add(new()
            {
                button = MouseButton.LeftMouse,
                modifiers = PlatformUtils.IsMac ? EventModifiers.Command : EventModifiers.Control
            });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            m_Element = target as GraphElement;
            if (m_Element == null)
            {
                throw new("SelectableManipulator can only be added to GraphElement elements");
            }

            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            m_Element = null;
        }

        protected void OnMouseDown(MouseDownEvent e)
        {
            if (!CanStartManipulation(e))
            {
                return;
            }

            if (m_Element.Graph == null || !m_Element.IsSelectable() || m_Element.Id == null)
            {
                return;
            }

            // Do not stop the propagation since selection should happen before other use cases (e.g. dragging).
            bool additive = e.shiftKey;
            bool subtractive = e.actionKey;
            bool exclusive = !(additive ^ subtractive);

            if (exclusive)
            {
                // Clicking an already-selected element keeps the current
                // (possibly multi-)selection so it can be dragged as a group.
                if (!m_Element.Selected)
                {
                    m_Element.Graph.RequestCommand(new SelectGraphElementsCommand(new List<string> { m_Element.Id }, GraphSelectionMode.Replace));
                }
            }
            else
            {
                m_Element.Graph.RequestCommand(new SelectGraphElementsCommand(new List<string> { m_Element.Id }, GraphSelectionMode.Toggle));
            }
        }
    }
}
