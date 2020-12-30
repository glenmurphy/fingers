using System;
using System.Numerics; // Vector
using System.Threading;
using System.Reflection; // Delegates
using System.Windows.Forms; // SystemInformation
// Leap
using Leap;

class Fingers
{
  bool cursorEnabled = true;
  bool useRightHand = false;
  long lastClicked = 0;

  Vector2 screenCenter;
  Vector2 resetPoint;

  // Angle of the mount in degrees - x/y/z are the positional axis the angle impacts; these 
  // have the effect of pushing the cursor in the direction of the offset, so you can use this
  // if you want your cursor pushed more in a specific direction
  Vector3 mountAngleOffset = new Vector3(0, 0, 10);

  // Position of the eye relative to the leap (in mm, using the Leap coordinate system), in the
  // leap's rotation system (so 'down' is parallel to the screen)
  Vector3 eyePositionOffset = new Vector3(0, -110, -73);

  // scaling angle to screen pixels / HMD fov
  // This can change as you change the position offset and ideally we would have a calibration
  // phase. You can test this by putting your hand in a fixed location and rotating your head:
  // - if the cursor stays in the same place in the cockpit you have this correct
  // - if the cursor seems to be pulled with the head, the values are too low (and vice-versa)
  Vector2 inputAngleScale = new Vector2(16, 24);

  public void UpdateHands(Leap.Vector left, Leap.Vector right)
  {
    if (left.Magnitude == 0 && right.Magnitude == 0) {
      return;
    } else if (left.Magnitude == 0 && right.Magnitude != 0) {
      SetCursorPos(getScreenPosition(right));
    } else if (left.Magnitude != 0 && right.Magnitude == 0) {
      SetCursorPos(getScreenPosition(left));
    } else {
      Vector2 l = getScreenPosition(left);
      Vector2 r = getScreenPosition(right);
      SetCursorPos((l.LengthSquared() < r.LengthSquared()) ? l : r);
    }
  }

  public void ToggleCursorEnabled()
  {
    cursorEnabled = !cursorEnabled;
  }

  public long GetTime() {
    return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  }

  public void HandleButton(LoopButton b)
  {
    Console.WriteLine(b);
    if (b == LoopButton.UP || !cursorEnabled) {
      ToggleCursorEnabled();
      return;
    }
    
    if (b == LoopButton.CENTER)
      Click(0);
    if (b == (useRightHand ? LoopButton.BACK : LoopButton.FWD))
      Click(1);

    if (b == (useRightHand ? LoopButton.FWD : LoopButton.BACK))
      Scroll(0);
    if (b == LoopButton.DOWN)
      Scroll(1);
    
    lastClicked = GetTime();
  }

  public void SetCursorPos(Vector2 pos) {
    if (!cursorEnabled || GetTime() < lastClicked + 96)
      return;
    
    Winput.SetCursorPosition((int)(screenCenter.X + pos.X), (int)(screenCenter.Y + pos.Y));
  }

  public void Click(Int16 button) {
    Winput.ClickMouse(button == 0);
  }

  public void Scroll(Int16 direction) {
    Winput.ScrollMouse(direction == 0);
  }

  public Vector2 getScreenPosition(Leap.Vector pos) {
    float x = -pos[0] - eyePositionOffset.X;
    float y = pos[1] - eyePositionOffset.Y;
    float z = pos[2] + eyePositionOffset.Z;

    float h = (float)Math.Atan(x / y) * 180 /(float)Math.PI + mountAngleOffset.X;
    float v = (float)Math.Atan(z / y) * 180 /(float)Math.PI + mountAngleOffset.Z;

    return new Vector2(
      h * inputAngleScale.X,
      v * inputAngleScale.Y
    );
  }

  public Fingers()
  {
    screenCenter = new Vector2(SystemInformation.VirtualScreen.Width / 2, SystemInformation.VirtualScreen.Height / 2);
    
    resetPoint = new Vector2(screenCenter.X, screenCenter.Y * (float)1.25);
    
    Winput.SetCursorPosition((int)resetPoint.X, (int)resetPoint.Y);

    LeapHandler leap = new LeapHandler(this);
    LoopListener loop = new LoopListener(this);

    // Keep this process running until Enter is pressed
    // Console.WriteLine("Press any key to quit...");
    Console.ReadLine();
  }

  public static void Main()
  {
    new Fingers();
  }
}
