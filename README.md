# Fingers

This lets Digital Combat Simulator (DCS) VR use the Leap Motion and a bluetooth ring to press buttons. See photos and video on the [Twitter thread](https://twitter.com/gmurphy/status/1341829602138681345).

### Requirements

* Leap Motion tracker
* A bluetooth ring (of custom design, details soon)
* A Windows PC with Bluetooth

### Build instructions

- Install the Leap 4.1 SDK (5.0 beta has also been tested and appears to work, but with occasional disconnects)
- `git clone https://github.com/glenmurphy/fingers.git`
- Open FingersApp.sln in Visual Studio 2019 (need to make sure you have .NET4.8 SDK installed)
- Make sure the LeapSDK and Windows.winmd paths in fingers.csproj point to the right place
- Cross your actual fingers because build instructions usually forget to mention all kinds of stuff the author didn't realize was necessary
- Build + Run

### Setup / Config
- Make sure your Leap understands which way is up - look in the vizualizer and flip it in the control panel if necessary; the auto orientation doesn't always work. This gets reset every time you update Leap, and is the most likely cause of problems.
- Configure mountAngleOffset and mountPositionOffset in fingers.cs to reflect the mounting position of your headset

### Other

* [OnShape Model for the Pimax/Leap mount](https://cad.onshape.com/documents/ae5a6cb30a9eb6d1e482df71/w/023af4907bc823d27392def4/e/ad8553e8c3b3b2fdd51e0683)
* You're almost certainly better off buying [PointCTRL](http://pointctrl.com/) - I made this because I'm still waiting for one :)
* This was previously a Deno/Chrome app, but [swapped to C#](https://github.com/glenmurphy/fingers/commit/4073f3e6cb88f640333d66b5c22c00bcc68cfe3f)
* Ultraleap dropping official C# support but making it unofficially available is why the LeapCS directory exists