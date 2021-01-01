using System;
using System.Numerics; // Vector
using System.Windows.Forms; // SystemInformation

public class Fingers
{
  // Used to figure out what direction FWD is on the Loop; will deprecate when we support multiple
  // loops (when I get multiple loops)
  private static bool useRightHand = false;

  // how long to pause cursor movement after mouseDown events; not useful in DCS, but helps prevent
  // accidental scrolling/dragging in other places (e.g. text editors)
  private static int clickPause = 0;

  // Angle of the mount in degrees - x/y/z are the positional axis the angle impacts; these have the
  // effect of pushing the cursor in the direction of the offset, so you can use this if you want
  // your cursor pushed more in a specific direction
  private static Vector3 mountAngleOffset = new Vector3(0, 0, 10);

  // Position of the eye relative to the leap (in mm, using the Leap coordinate system), in the
  // leap's rotation system (so 'down' is parallel to its front screen)
  private static Vector3 eyePositionOffset = new Vector3(0, -110, -73);

  // DCS translates mouse movement over its window into a 2D plane in the game world - you can see
  // this window when you bring up the ESC menu where it is fixed in place, while it follows the
  // head position at other times (this is why you get two different mouse behaviors). A mouse
  // cursor in a corner of the DCS window always translates into the same position in-game,
  // regardless of the player's FOV or the size/ratio of the main DCS window, so we need to
  // compensate for the stretching DCS does to map input over its desktop window of varying size and
  // aspect ratio to its inner window of fixed size and aspect ratio.
  //
  // This virtual screen appears to be 100 degrees wide with 16:9 aspect ratio
  private static float inputScreenRatio = 16f / 10f;
  private static float inputScreenWidthDegrees = 100f;
  private static float inputScreenHeightDegrees = inputScreenWidthDegrees / inputScreenRatio;

  // The above is translated using this variable, which is set in HandleDCSWindow; here are some
  // reasonable default values
  Vector2 inputAngleScale = new Vector2(14.4f, 21.6f);

  // The last hand we tracked
  HandData currentHand;

  // Scroll tracking
  // DCS uses both individual events (for discrete things like channel selectors) as well as amounts
  // for analog things like brightness controls - need to strike a balance between making one too
  // sensitive and the other too insensitive
  private static float ScrollDetentDegrees = 15;
  private static int ScrollDetentAmount = 750;
  private static int ScrollClickTime = 200;

  long scrollInitTime = 0;
  Boolean scrolled = false;
  float scrollLastAngle = 0;

  Vector2 screenCenter;
  Vector2 resetPoint;

  bool leftButtonDown = false;
  bool rightButtonDown = false;
  long lastClicked = 0;
  bool cursorEnabled = true;

  public Fingers()
  {
    screenCenter = new Vector2(SystemInformation.VirtualScreen.Width / 2, SystemInformation.VirtualScreen.Height / 2);

    resetPoint = new Vector2(screenCenter.X, screenCenter.Y * (float)1.25);

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
    inputAngleScale = new Vector2(dim.W / inputScreenWidthDegrees,
                                  dim.Z / inputScreenHeightDegrees);
  }

  public Vector2 getScreenPosition(Leap.Vector pos)
  {
    float x = -pos[0] - eyePositionOffset.X; // horizontal
    float y = pos[1] - eyePositionOffset.Y; // depth
    float z = pos[2] + eyePositionOffset.Z; // upness

    float h = (float)Math.Atan2(x, y) * 180 / (float)Math.PI + mountAngleOffset.X;
    float v = (float)Math.Atan2(z, y) * 180 / (float)Math.PI + mountAngleOffset.Z;

    return new Vector2(
      h * inputAngleScale.X,
      v * inputAngleScale.Y
    );
  }

  public bool IsDragging() {
    return (scrollInitTime != 0 || leftButtonDown || rightButtonDown);
  }

  public HandData GetActiveHand(HandData left, HandData right)
  {
    HandData activeHand = new HandData() { isActive = false };

    // Match to existing hand if we're in some kind of drag event (so we don't snap to another
    // hand partway through).
    if (IsDragging() && currentHand.isActive) {
      if (currentHand.isLeft && left.isActive)
        return left;
      else if (!currentHand.isLeft && right.isActive)
        return right;
      else
      { 
        return activeHand;
      }
    }

    if (!left.isActive && right.isActive)
      return right;
    else if (left.isActive && !right.isActive)
      return left;
    else if (left.isActive && right.isActive)
    {
      Vector2 l = getScreenPosition(left.pos);
      Vector2 r = getScreenPosition(right.pos);
      return (l.LengthSquared() < r.LengthSquared()) ? left : right;
    }

    return activeHand;
  }

  public void HandleHands(HandData left, HandData right)
  {
    HandData activeHand = GetActiveHand(left, right);

    if (!activeHand.isActive && currentHand.isActive)
    {
      DisengageHand();
      return;
    }

    if (scrollInitTime != 0)
    {
      while (activeHand.angle > scrollLastAngle + ScrollDetentDegrees)
      {
        Scroll(-ScrollDetentAmount);
        scrollLastAngle += ScrollDetentDegrees;
      }
      while (activeHand.angle < scrollLastAngle - ScrollDetentDegrees)
      {
        Scroll(ScrollDetentAmount);
        scrollLastAngle -= ScrollDetentDegrees;
      }
    }

    currentHand = activeHand;
    SetCursorPos(getScreenPosition(activeHand.pos));
  }

  // Current hand left stopped being active
  // TODO: disengage left/right drag? might have unexpected side effects if the user didn't care
  // about the drag
  private void DisengageHand()
  {
    if (scrollInitTime != 0)
      EndScroll();
  }

  private void ToggleCursorEnabled()
  {
    cursorEnabled = !cursorEnabled;
  }

  private long GetTime()
  {
    return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  }

  public void HandleLoopEvent(LoopButton b, Boolean pressed)
  {
    Console.WriteLine("{0} {1}", b, pressed ? "pressed" : "up");
    LoopButton fwdButton = (useRightHand ? LoopButton.FWD : LoopButton.BACK);
    LoopButton backButton = (useRightHand ? LoopButton.BACK : LoopButton.FWD);

    if (b == fwdButton)
    {
      if (pressed)
      {
        if (currentHand.isActive)
          StartScroll();
        else
          Scroll(ScrollDetentAmount);
      }
      else
      {
        // If we just tapped the button and didn't do anything, then send a single scroll event;
        // this is useful for discrete controls
        if (GetTime() < scrollInitTime + ScrollClickTime && scrolled == false)
        {
          Scroll(ScrollDetentAmount);
        }
        EndScroll();
      }
    }

    if (pressed && (b == LoopButton.UP || !cursorEnabled))
    {
      ToggleCursorEnabled();
      return;
    }

    if (b == LoopButton.CENTER && pressed)
    {
      Winput.MouseButton(Winput.MouseEventF.LeftDown);
      leftButtonDown = true;
      lastClicked = GetTime();
    }
    else if (b == LoopButton.CENTER && !pressed)
    {
      Winput.MouseButton(Winput.MouseEventF.LeftUp);
      leftButtonDown = false;
    }
    else if (b == backButton && pressed)
    {
      Winput.MouseButton(Winput.MouseEventF.RightDown);
      rightButtonDown = true;
      lastClicked = GetTime();
    }
    else if (b == backButton && !pressed)
    {
      Winput.MouseButton(Winput.MouseEventF.RightUp);
      rightButtonDown = false;
    }
    else if (b == LoopButton.DOWN && pressed)
    {
      Scroll(-ScrollDetentAmount);
    }
  }

  private void StartScroll()
  {
    Console.WriteLine("Starting scroll");

    scrollInitTime = GetTime();
    scrolled = false;
    scrollLastAngle = currentHand.angle;
  }

  private void Scroll(int amount)
  {
    Console.WriteLine("Scroll {0}", amount);
    scrolled = true;
    Winput.ScrollMouse(amount);
  }

  private void EndScroll()
  {
    Console.WriteLine("Ending scroll");
    scrollInitTime = 0;
  }

  private void SetCursorPos(Vector2 pos)
  {
    if (!cursorEnabled || GetTime() < lastClicked + clickPause)
      return;

    Winput.SetCursorPosition((int)(screenCenter.X + pos.X), (int)(screenCenter.Y + pos.Y));
  }

  public static void Main()
  {
    new Fingers();
  }
}