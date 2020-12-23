# Fingers

This is a quick hack to let DCS VR use the Leap Motion and a North Loop (the
ring thing that came with the North Focals) to press buttons. See photos and video on the [Twitter thread](https://twitter.com/gmurphy/status/1341829602138681345).

### Requirements

* Leap Motion
* A Loop from North (came with the North Focals, which you can't buy anymore)
* Deno
* Chrome
* A PC with Bluetooth

### Installation/Usage

* Install the Leap Motion software, enable "Allow Web Apps" in the control panel
* deno run -A --unstable fingers.mjs
* Open ./loop/index.html in Chrome, press 'start' and pair the North Loop
* Edit inputAngleOffset and inputAngleScale in fingers.mjs to suit

### How it works

* It uses the Leap Motion WebSocket API to get the position of your hand
* It translates that into mouse cursor position, which DCS translates into the ingame cursor
* It uses Chrome WebBluetooth to get the state of the North Loop, and sends that state (via WebSocket) to the main script, which then activates different mouse buttons - we use the Loop because we need super reliable button presses of many types, and hand-tracking/pinching etc still can't do it at the reliability/speed we need.

### Other

* [OnShape Model for the Pimax/Leap mount](https://cad.onshape.com/documents/ae5a6cb30a9eb6d1e482df71/w/023af4907bc823d27392def4/e/ad8553e8c3b3b2fdd51e0683)
* This uses Chrome and the WebBluetooth API, which feels like a brittle hack, but
  it's still less of a brittle hack than getting Bluetooth LE working in Node/Deno
* You're almost certainly better off buying [PointCTRL](http://pointctrl.com/) - I made this because I'm still waiting for one :)