// From https://github.com/Bojidarist/SendInputsDemo
// Also consider https://stackoverflow.com/questions/5094398/how-to-programmatically-mouse-move-click-right-click-and-keypress-etc-in-winfo

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
public class Winput
{
    [Flags]
    public enum MouseEventF
    {
        Move = 0x0001,
        LeftDown = 0x0002,
        LeftUp = 0x0004,
        RightDown = 0x0008,
        RightUp = 0x0010,
        MiddleDown = 0x0020,
        MiddleUp = 0x0040,
        Wheel = 0x0800,
        XDown = 0x0080,
        XUp = 0x0100
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        public uint Type;
        public MouseInput Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        internal int dx;
        internal int dy;
        internal int mouseData;
        internal MouseEventF dwFlags;
        internal uint time;
        internal UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, int cButtons, uint dwExtraInfo);

    [DllImport("User32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInputStructure);

    public static void SetCursorPosition(int x, int y)
    {
        SetCursorPos(x, y);
    }

    public static void ScrollMouse(int amount)
    {
        Input[] i = new Input[1];

        i[0] = new Input();
        i[0].Type = 0;
        i[0].Data.dx = 0;
        i[0].Data.dy = 0;
        i[0].Data.dwFlags = MouseEventF.Wheel;
        i[0].Data.mouseData = amount;

        SendInput(1, i, Marshal.SizeOf(i[0]));
        //mouse_event((uint)MouseEventF.Wheel, 0, 0, amount, 0);
    }

    public static void MouseButton(MouseEventF button)
    {
        Input[] i = new Input[1];

        i[0] = new Input();
        i[0].Type = 0;
        i[0].Data.dx = 0;
        i[0].Data.dy = 0;
        i[0].Data.dwFlags = button;

        SendInput(1, i, Marshal.SizeOf(i[0]));

        //mouse_event((uint)button, 0, 0, 0, 0);
    }
}