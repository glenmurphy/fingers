using System;
using System.Numerics; // Vector
using System.Windows.Forms; // SystemInformation

public class Fingers
{
  bool cursorEnabled = true;
  bool useRightHand = false;

  // how long to pause cursor movement after mouseDown events; not useful in DCS, but helps prevent
  // accidental scrolling/dragging in other places (e.g. text editors)
  int clickPause = 0;
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
  //
  // Right now this is overriden by the experimental config code in Fingers()
  Vector2 inputAngleScale = new Vector2(14.4f, 21.6f);

  // Scroll tracking
  // DCS uses both individual events (for discrete things like channel selectors) as well as
  // amounts for analog things like brightness controls - need to strike a balance between making
  // one too sensitive and the other too insensitive
  private static float ScrollDetentDegrees = 15;
  private static int ScrollDetentAmount = 750;
  private static int ScrollClickTime = 200;

  long scrollInitTime = 0;
  Boolean scrollStarted = false;
  Boolean scrollIsLeft = false;
  Boolean scrolled = false;
  float scrollLastAngle = 0;

  public Fingers()
  {
    screenCenter = new Vector2(SystemInformation.VirtualScreen.Width / 2, SystemInformation.VirtualScreen.Height / 2);
    
    resetPoint = new Vector2(screenCenter.X, screenCenter.Y * (float)1.25);

    // experimental
    // we want to do this based on the width/height of the DCS window, but this is a good 
    // approximation for Pimax 'Small' FOV, which means it works well at 'normal', and should
    // work OK on smaller headsets
    float hScale = SystemInformation.VirtualScreen.Height / 100.0f; 
    float vScale = hScale * 1.5f;
    inputAngleScale = new Vector2(hScale, vScale);
    
    DCS.Monitor(this);

    Winput.SetCursorPosition((int)resetPoint.X, (int)resetPoint.Y);

    LeapHandler leap = new LeapHandler(this);
    LoopListener loop = new LoopListener(this);

    // Keep this process running
    Console.ReadLine();
  }

  public void HandleDCSWindow(Vector4 dim)
  {
    Console.WriteLine("DCS Window Size Adjustment: {0}", dim);

    // When you're using the VR mouse cursor mode, DCS translates mouse movement over 
    // its window into a fixed 2D plane in the game world - a mouse cursor a corner of 
    // the DCS window always translates into the same position in-game, regardless of 
    // the player's FOV or the size/ratio of the main DCS window, so we need to compensate
    // for the size of that window always mapping to the same thing. You can see this 
    // window when you bring up the ESC menu.
    //
    // Right now we assume (via testing) that that inner screen is 100 degrees wide with 16:9 aspect
    // ratio (this seems constant regardless of desktop res)
    float degreesWidth = 100f;
    float ratio = 16f/10f; 
    inputAngleScale = new Vector2(dim.W / degreesWidth, dim.Z / (degreesWidth / ratio));
  }

  public Vector2 getScreenPosition(Leap.Vector pos) {
    float x = -pos[0] - eyePositionOffset.X; // horizontal
    float y = pos[1] - eyePositionOffset.Y;  // depth
    float z = pos[2] + eyePositionOffset.Z;  // upness

    float h = (float)Math.Atan2(x, y) * 180 /(float)Math.PI + mountAngleOffset.X;
    float v = (float)Math.Atan2(z, y) * 180 /(float)Math.PI + mountAngleOffset.Z;

    return new Vector2(
      h * inputAngleScale.X,
      v * inputAngleScale.Y
    );
  }

  public void HandleHands(HandData left, HandData right)
  {
    HandData activeHand = new HandData() { isActive = false };

    if (!left.isActive && right.isActive) {
      activeHand = right;
    } else if (left.isActive && !right.isActive) {
      activeHand = left;
    } else if (left.isActive && right.isActive) {
      Vector2 l = getScreenPosition(left.pos);
      Vector2 r = getScreenPosition(right.pos);
      activeHand = (l.LengthSquared() < r.LengthSquared()) ? left : right;
    }

    if (scrollInitTime != 0) {
      if (scrollStarted) {
        HandData scrollHand = scrollIsLeft ? left : right;

        if (!scrollHand.isActive) {
          EndScroll();
          return;
        }
        while (scrollHand.angle > scrollLastAngle + ScrollDetentDegrees) {
          Scroll(-ScrollDetentAmount);
          scrollLastAngle += ScrollDetentDegrees;
        }
        while (scrollHand.angle < scrollLastAngle - ScrollDetentDegrees) {
          Scroll(ScrollDetentAmount);
          scrollLastAngle -= ScrollDetentDegrees;
        }
      } else if (activeHand.isActive) {
        // Start scrolling, set to initial positions
        scrollStarted = true;
        scrollIsLeft = activeHand.isLeft;
        scrollLastAngle = activeHand.angle;
      }
    } else if (activeHand.isActive) {
      SetCursorPos(getScreenPosition(activeHand.pos));
    }
  }

  public void ToggleCursorEnabled()
  {
    cursorEnabled = !cursorEnabled;
  }

  public long GetTime() {
    return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  }

  public void HandleLoopEvent(LoopButton b, Boolean pressed)
  {
    Console.WriteLine("{0} {1}", b, pressed ? "pressed" : "up");
    LoopButton fwdButton = (useRightHand ? LoopButton.FWD : LoopButton.BACK);
    LoopButton backButton = (useRightHand ? LoopButton.BACK : LoopButton.FWD);

    if (b == fwdButton) {
      if (pressed) {
        StartScroll();
      } else {
        // If we just tapped the button and didn't do anything, then send a single scroll event
        // this is useful for discrete controls
        if (GetTime() < scrollInitTime + ScrollClickTime && scrolled == false) {
          Scroll(ScrollDetentAmount);
        }
        EndScroll();
      }
    }

    if (pressed && (b == LoopButton.UP || !cursorEnabled)) {
      ToggleCursorEnabled();
      return;
    }

    if (b == LoopButton.CENTER && pressed) {
      Winput.MouseButton(Winput.MouseEventF.LeftDown);
      lastClicked = GetTime();
    } else if (b == LoopButton.CENTER && !pressed) {
      Winput.MouseButton(Winput.MouseEventF.LeftUp);
    } else if (b == backButton && pressed) {
      Winput.MouseButton(Winput.MouseEventF.RightDown);
      lastClicked = GetTime();
    } else if (b == backButton && !pressed) {
      Winput.MouseButton(Winput.MouseEventF.RightUp);
    } else if (b == LoopButton.DOWN && pressed) {
      Scroll(-ScrollDetentAmount);
    }
  }

  public void StartScroll()
  {
    Console.WriteLine("Starting scroll");
    scrollInitTime = GetTime();
    scrollStarted = false;
    scrolled = false;
    scrollIsLeft = false;
  }

  public void Scroll(int amount)
  {
    Console.WriteLine("Scroll {0}", amount);
    scrolled = true;
    Winput.ScrollMouse(amount);
  }

  public void EndScroll()
  {
    Console.WriteLine("Ending scroll");
    scrollInitTime = 0;
  }

  public void SetCursorPos(Vector2 pos) {
    if (!cursorEnabled || GetTime() < lastClicked + clickPause)
      return;
    
    Winput.SetCursorPosition((int)(screenCenter.X + pos.X), (int)(screenCenter.Y + pos.Y));
  }

  public static void Main()
  {
    new Fingers();
  }
}
