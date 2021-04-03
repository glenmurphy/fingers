using System;
using System.Numerics; // Vector
using System.Windows.Forms; // SystemInformation
using System.Diagnostics; // Debug
public enum RingStatus
{
    SEARCHING = 1,
    CONNECTING = 2,
    CONNECTED = 4,
}

public class Fingers
{
    // how long to pause cursor movement after mouseDown events; not useful in DCS, but helps prevent
    // accidental scrolling/dragging in other places (e.g. text editors)
    private static int clickPause = 0;

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

    // When choosing between two hands, how many degrees we should bias towards sticking with the
    // currently tracked hand.
    private static float overlap = 2;

    // Scroll tracking
    // DCS uses both individual events (for discrete things like channel selectors) as well as amounts
    // for analog things like brightness controls - need to strike a balance between making one too
    // sensitive and the other too insensitive
    private static int ScrollDetentAmount = 750;


    // Internal variables
    // The inputScreenRatio setup is translated using inputAngleScale, which is set in 
    // HandleDCSWindow; here are some reasonable default values
    Vector2 inputAngleScale = new Vector2(14.4f, 21.6f);

    // The last hand we tracked
    HandData currentHand;

    public FingersApp.MainWindow ui;

    Vector2 screenCenter;
    Vector2 resetPoint;

    ulong leftRingAddr = 0;
    ulong rightRingAddr = 0;

    bool leftButtonDown = false;
    bool rightButtonDown = false;
    long lastClicked = 0;
    bool cursorEnabled = true;

    LoopListener loop;
    LeapHandler leap;

    public Fingers(FingersApp.MainWindow mainWindow)
    {
        ui = mainWindow;

        // Configuration variables
        screenCenter = new Vector2(SystemInformation.VirtualScreen.Width / 2, SystemInformation.VirtualScreen.Height / 2);

        resetPoint = new Vector2(screenCenter.X, screenCenter.Y * (float)1.25);

        DCS.Monitor(this);

        loop = new LoopListener(this);
        leap = new LeapHandler(this);

        SetLeapProfile(FingersApp.Properties.Settings.Default.LeapProfile, true);

        // Keep this process running
        // Console.ReadLine();
    }

    public void HandleDCSWindow(Vector4 dim)
    {
        Debug.WriteLine("DCS Window Size Adjustment: {0}", dim);
        inputAngleScale = new Vector2(dim.W / inputScreenWidthDegrees,
                                      dim.Z / inputScreenHeightDegrees);
    }

    // Get the angle of the position (in HandData position) relative to the eye in degrees
    public Vector2 GetRelativeAngle(ref Vector3 pos)
    {
        Vector2 angle;
        angle.X = (float)Math.Atan2(pos.X, pos.Z) * 180 / (float)Math.PI;
        angle.Y = -(float)Math.Atan2(pos.Y, pos.Z) * 180 / (float)Math.PI;
        return angle;
    }

    public Vector2 GetScreenPosition(Vector3 pos)
    {
        Vector2 angle = GetRelativeAngle(ref pos);

        return new Vector2(
          angle.X * inputAngleScale.X,
          angle.Y * inputAngleScale.Y
        );
    }

    public bool IsDragging()
    {
        return (leftButtonDown || rightButtonDown);
    }

    public HandData GetActiveHand(HandData left, HandData right)
    {
        HandData activeHand = new HandData() { isActive = false };

        // Match to existing hand if we're in some kind of drag event (so we don't snap to another
        // hand partway through).
        if (IsDragging() && currentHand.isActive)
        {
            if (currentHand.isLeft && left.isActive)
                return left;
            else if (!currentHand.isLeft && right.isActive)
                return right;
            else
                return activeHand;
        }

        if (!left.isActive && right.isActive)
            return right;
        else if (left.isActive && !right.isActive)
            return left;
        else if (left.isActive && right.isActive)
        {
            // Figure out which hand to focus on by distance away from the middle
            float l = Math.Abs(GetRelativeAngle(ref left.pos).X);
            float r = Math.Abs(GetRelativeAngle(ref right.pos).X);

            // If we're currently tracking a hand, bias away from the other hand by a certain amount of
            // angle (defined in overlap)
            if (currentHand.isActive && currentHand.isLeft)
                r += overlap;
            else if (currentHand.isActive && !currentHand.isLeft)
                l += overlap;
            return (l < r) ? left : right;
        }

        return activeHand;
    }

    public void HandleLeapConnected()
    {
        ui.Dispatcher.Invoke(() =>
        {
            ui.SetLeapStatus("Connected");
        });
    }
    public void HandleLeapDisconnected()
    {
        ui.Dispatcher.Invoke(() =>
        {
            ui.SetLeapStatus("Connecting...");
        });
    }

    public void HandleHands(HandData left, HandData right)
    {
        HandData activeHand = GetActiveHand(left, right);

        if (!activeHand.isActive && currentHand.isActive)
        {
            DisengageHand();
            return;
        }

        currentHand = activeHand;
        SetCursorPos(GetScreenPosition(activeHand.pos));
    }

    // Called when the current hand stops being active; currently we leave left/right mouse buttons
    // down because a user might have them down for other reasons (e.g. holding temporary switches)
    private void DisengageHand()
    {

    }

    private void ToggleCursorEnabled()
    {
        cursorEnabled = !cursorEnabled;
    }

    private long GetTime()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void HandleLoopConnecting(ulong addr)
    {
        if (leftRingAddr != 0 && rightRingAddr != 0)
        {
            return;
        }
        else if (leftRingAddr != 0 || FingersApp.Properties.Settings.Default.RightRingID == addr)
        {
            if (rightRingAddr != addr) ui.Dispatcher.Invoke(() =>
            {
                ui.SetRightRingStatus(RingStatus.CONNECTING, 0);
            });
        }
        else if (leftRingAddr != addr) 
        {
            ui.Dispatcher.Invoke(() =>
            {
                ui.SetLeftRingStatus(RingStatus.CONNECTING, 0);
            });
        }
    }

    public void HandleLoopConnected(ulong addr)
    {
        String addrString = addr.ToString("X");
        if (leftRingAddr != 0 || addr.Equals(FingersApp.Properties.Settings.Default.RightRingID))
        {
            rightRingAddr = addr;
            FingersApp.Properties.Settings.Default.RightRingID = addr;
            FingersApp.Properties.Settings.Default.Save();
        }
        else
        {
            leftRingAddr = addr;
            FingersApp.Properties.Settings.Default.LeftRingID = addr;
            FingersApp.Properties.Settings.Default.Save();
        }
        UpdateRingStatus();
    }

    public void SetLeapProfile(String name, bool updateUI)
    {
        if (name.Equals("Joculus"))
        {
            leap.SetProfile(5, 0, 0, 0, -18, 100);
        }
        else if (name.Equals("Generic"))
        {
            leap.SetProfile(0, 0, 0, 0, 0, 90);
        }
        else
        {
            name = "Pimax";
            leap.SetProfile(10, 0, 0, 0, -45, 68);
        }

        if (updateUI)
        {
            ui.Dispatcher.Invoke(() =>
            {
                ui.SelectLeapProfile(name);
            });
        }
        FingersApp.Properties.Settings.Default.LeapProfile = name;
        FingersApp.Properties.Settings.Default.Save();
    }

    private void UpdateRingStatus()
    {
        if (leftRingAddr > 0)
            ui.Dispatcher.Invoke(() => { ui.SetLeftRingStatus(RingStatus.CONNECTED, leftRingAddr); });
        else
            ui.Dispatcher.Invoke(() => { ui.SetLeftRingStatus(RingStatus.SEARCHING, 0); });


        if (rightRingAddr > 0)
            ui.Dispatcher.Invoke(() => { ui.SetRightRingStatus(RingStatus.CONNECTED, rightRingAddr); });
        else
            ui.Dispatcher.Invoke(() => { ui.SetRightRingStatus(RingStatus.SEARCHING, 0); });
    }
    public void SwapRings()
    {
        FingersApp.Properties.Settings.Default.LeftRingID = rightRingAddr;
        FingersApp.Properties.Settings.Default.RightRingID = leftRingAddr;
        leftRingAddr = FingersApp.Properties.Settings.Default.LeftRingID;
        rightRingAddr = FingersApp.Properties.Settings.Default.RightRingID;
        FingersApp.Properties.Settings.Default.Save();

        UpdateRingStatus();
    }

    public void HandleLoopEvent(LoopButton b, Boolean pressed, ulong addr)
    {
        Debug.WriteLine("{0}: {1} {2}", addr.ToString("X"), b, pressed ? "pressed" : "released");
        if (addr == rightRingAddr)
        {
            if (b == LoopButton.FWD) b = LoopButton.BACK;
            else if (b == LoopButton.BACK) b = LoopButton.FWD;
        }

        ui.Dispatcher.Invoke(() =>
        {
            ui.SetButtonStatus(b, pressed, (addr == rightRingAddr));
        });

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
        else if (b == LoopButton.BACK && pressed)
        {
            Winput.MouseButton(Winput.MouseEventF.RightDown);
            rightButtonDown = true;
            lastClicked = GetTime();
        }
        else if (b == LoopButton.BACK && !pressed)
        {
            Winput.MouseButton(Winput.MouseEventF.RightUp);
            rightButtonDown = false;
        }
        else if (b == LoopButton.FWD && pressed)
        {
            Scroll(ScrollDetentAmount);
        }
        else if (b == LoopButton.DOWN && pressed)
        {
            Scroll(-ScrollDetentAmount);
        }
    }

    private void Scroll(int amount)
    {
        //Console.WriteLine("Scroll {0}", amount);
        Winput.ScrollMouse(amount);
    }

    private void SetCursorPos(Vector2 pos)
    {
        if (!cursorEnabled || GetTime() < lastClicked + clickPause)
            return;

        Winput.SetCursorPosition((int)(screenCenter.X + pos.X), (int)(screenCenter.Y + pos.Y));
    }
}