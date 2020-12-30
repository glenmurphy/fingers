// From https://github.com/Bojidarist/SendInputsDemo
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public class Winput {
  [Flags]
  public enum MouseEventF {
    Move = 0x0001,
    LeftDown = 0x0002,
    LeftUp = 0x0004,
    RightDown = 0x0008,
    RightUp = 0x0010,
    MiddleDown = 0x0020,
    MiddleUp = 0x0040,
    VirtualDesk = 0x4000,
    Wheel = 0x0800,
    XDown = 0x0080,
    XUp = 0x0100
  }

  [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
  public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

  [DllImport("User32.dll")]
  private static extern bool SetCursorPos(int x, int y);

  public static void SetCursorPosition(int x, int y) {
    SetCursorPos(x, y);
  }

  public static void ScrollMouse(bool isUp) {
    mouse_event((uint) MouseEventF.Wheel, 0, 0, (uint) (isUp ? 50 : -50), 0);
  }

  public async static void ClickMouse(bool isLeft) {
    mouse_event((uint) (isLeft ? MouseEventF.LeftDown : MouseEventF.RightDown), 0, 0, 0, 0);
    await Task.Delay(96);
    mouse_event((uint) (isLeft ? MouseEventF.LeftUp : MouseEventF.RightUp), 0, 0, 0, 0);
  }
}