// Ported from Logical2.GraphCore (originally adapted from Unity's GraphView
// reference source: https://unity3d.com/legal/licenses/Unity_Reference_Only_License).
using System;

namespace Soup.Editor
{
    public enum Direction
    {
        Input = 0,
        Output = 1
    }

    public enum Orientation
    {
        Horizontal,
        Vertical
    }

    public enum PortCapacity
    {
        Single = 0,
        Multi = 1,
    }

    [Flags]
    public enum Capabilities
    {
        Selectable = 1 << 0,
        Collapsible = 1 << 1,
        Resizable = 1 << 2,
        Movable = 1 << 3,
        Deletable = 1 << 4,
        Droppable = 1 << 5,
        Ascendable = 1 << 6,
        Renamable = 1 << 7,
        Copiable = 1 << 8,
        Snappable = 1 << 9,
        Groupable = 1 << 10,
        Stackable = 1 << 11
    }
}
