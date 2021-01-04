# Fingers

This lets Digital Combat Simulator (DCS) VR use the Leap Motion and a North Loop (the
ring thing that came with the North Focals) to press buttons. See photos and video on the [Twitter thread](https://twitter.com/gmurphy/status/1341829602138681345).

### Requirements

* Leap Motion
* A Loop from North (came with the North Focals, which you can't buy anymore)
* A Windows PC with Bluetooth

### Build instructions

- install the Leap SDK (I used 3.2)
- install [.NET SDK](https://dotnet.microsoft.com/download)
- `git clone https://github.com/glenmurphy/fingers.git`
- `dotnet add package System.Runtime.WindowsRuntime --version 4.7.0`
- make sure LeapSDK and Windows.winmd paths in fingers.csproj point to the right place
- configure mountAngleOffset and mountPositionOffset in fingers.cs
- cross your actual fingers because build instructions usually forget to mention all kinds of stuff the author didn't realize was necessary
- `dotnet run`

### Other

* [OnShape Model for the Pimax/Leap mount](https://cad.onshape.com/documents/ae5a6cb30a9eb6d1e482df71/w/023af4907bc823d27392def4/e/ad8553e8c3b3b2fdd51e0683)
* You're almost certainly better off buying [PointCTRL](http://pointctrl.com/) - I made this because I'm still waiting for one :)
* This was previously a Deno/Chrome app, but [swapped to C#](https://github.com/glenmurphy/fingers/commit/4073f3e6cb88f640333d66b5c22c00bcc68cfe3f)