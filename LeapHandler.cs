using System;
using Leap;

class LeapHandler
{
  private Fingers fingers;
  Leap.Vector leftHandPos;
  Leap.Vector rightHandPos;
  Leap.Vector nullVector = new Leap.Vector(0, 0, 0);
  Leap.IController controller;
  
  public LeapHandler(Fingers parent) {
    fingers = parent;

    controller = new Leap.Controller();

    controller.Connect += OnServiceConnect;
    controller.Disconnect += OnServiceDisconnect;
    controller.FrameReady += OnFrame;
    controller.Device += OnConnect;
    controller.DeviceLost += OnDisconnect;
    controller.DeviceFailure += OnDeviceFailure;
    controller.LogMessage += OnLogMessage;
  }

  public void OnInit(Controller controller)
  {
    Console.WriteLine("Leap Initialized");
  }

  public void OnConnect(object sender, DeviceEventArgs args)
  {
    Console.WriteLine("Leap Connected");

    controller.SetPolicy(Leap.Controller.PolicyFlag.POLICY_ALLOW_PAUSE_RESUME);
    controller.SetPolicy(Leap.Controller.PolicyFlag.POLICY_BACKGROUND_FRAMES);
    controller.SetPolicy(Leap.Controller.PolicyFlag.POLICY_OPTIMIZE_HMD);
  }

  public void OnDisconnect(object sender, DeviceEventArgs args)
  {
    Console.WriteLine("Leap Disconnected");
  }

  public void OnFrame(object sender, FrameEventArgs args)
  {
    // Get the most recent frame and report some basic information
    Frame frame = args.frame;

    leftHandPos = nullVector;
    rightHandPos = nullVector;
    foreach (Hand hand in frame.Hands)
    {
      foreach (Finger finger in hand.Fingers) {
        if (finger.Type != Finger.FingerType.TYPE_INDEX) continue;
        Bone mcp = finger.Bone(Bone.BoneType.TYPE_METACARPAL);

        // I have no idea why, but IsLeft seems to be reversed
        if (hand.IsLeft) {
          leftHandPos = mcp.PrevJoint;
        } else {
          rightHandPos = mcp.PrevJoint;
        }
      }
    }

    if (frame.Hands.Count != 0)
    {
      fingers.UpdateHands(leftHandPos, rightHandPos);
    }
  }

  public void OnServiceConnect(object sender, ConnectionEventArgs args)
  {
    Console.WriteLine("Service Connected");
  }

  public void OnServiceDisconnect(object sender, ConnectionLostEventArgs args)
  {
    Console.WriteLine("Service Disconnected");
  }

  public void OnServiceChange(Controller controller)
  {
    Console.WriteLine("Service Changed");
  }

  public void OnDeviceFailure(object sender, DeviceFailureEventArgs args)
  {
    Console.WriteLine("Device Error");
    Console.WriteLine("  PNP ID:" + args.DeviceSerialNumber);
    Console.WriteLine("  Failure message:" + args.ErrorMessage);
  }

  public void OnLogMessage(object sender, LogEventArgs args)
  {
    switch (args.severity)
    {
      case Leap.MessageSeverity.MESSAGE_CRITICAL:
        Console.WriteLine("Leap Message: [Critical]");
        break;
      case Leap.MessageSeverity.MESSAGE_WARNING:
        Console.WriteLine("Leap Message: [Warning]");
        break;
      case Leap.MessageSeverity.MESSAGE_INFORMATION:
        Console.WriteLine("Leap Message: [Info]");
        break;
      case Leap.MessageSeverity.MESSAGE_UNKNOWN:
        Console.WriteLine("Leap Message: [Unknown]");
        break;
    }
    //Console.WriteLine("[{0}] {1}", args.timestamp, args.message);
  }
}