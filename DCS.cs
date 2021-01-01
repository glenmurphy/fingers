using System;
using System.Threading.Tasks;
using System.Numerics; // Vector
using System.Runtime.InteropServices; // DLL business
using System.Diagnostics; // Process

public class DCS
{
  [DllImport("user32.dll")]
  static extern IntPtr GetForegroundWindow();

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

  [StructLayout(LayoutKind.Sequential)]
  public struct RECT
  {
    public int Left;        // x position of upper-left corner
    public int Top;         // y position of upper-left corner
    public int Right;       // x position of lower-right corner
    public int Bottom;      // y position of lower-right corner
  }

  public static async void Monitor(Fingers parent)
  {
    Vector4 result = new Vector4();
    Vector4 lastResult = new Vector4();
    IntPtr hWnd;
    RECT rect;

    while (true)
    {
      hWnd = GetDCSWindow();
      if (hWnd != IntPtr.Zero)
      {
        GetWindowRect(hWnd, out rect);
        result.X = rect.Left;
        result.Y = rect.Top;
        result.Z = rect.Bottom - rect.Top; // height
        result.W = rect.Right - rect.Left; // width

        if (result.Z > 600 && !lastResult.Equals(result))
        {
          parent.HandleDCSWindow(result);
          lastResult = result;
        }
      }

      await Task.Delay(5000);
    }
  }

  public static IntPtr GetDCSWindow()
  {
    IntPtr hWnd = IntPtr.Zero;
    foreach (Process pList in Process.GetProcesses())
    {
      if (pList.MainWindowTitle.Equals("")) continue;

      if (pList.ProcessName.Equals("DCS") &&
          pList.MainWindowTitle.Contains("Digital Combat Simulator") &&
          !pList.MainWindowTitle.Contains("_server"))
      {
        return pList.MainWindowHandle;
      }
    }
    return hWnd;
  }
}