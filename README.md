# Fingers

This is a quick hack to let DCS use the Leap Motion and a North Loop (the
ring thing that came with the North Focals) to press buttons.

### Requirements

* Leap Motion
* A Loop from North (you can't get these anymore)
* Deno
* Chrome
* A PC with Bluetooth

### Installation/Usage

* deno run -A --unstable fingers.mjs
* Open ./loop/index.html in Chrome, press 'start' and pair the North Loop

### Other

* [Twitter thread, video](https://twitter.com/gmurphy/status/1341829602138681345)
* [OnShape Model for the Pimax/Leap mount](https://cad.onshape.com/documents/ae5a6cb30a9eb6d1e482df71/w/023af4907bc823d27392def4/e/ad8553e8c3b3b2fdd51e0683)
* This uses Chrome and the WebBluetooth API, which feels like a total hack, but
  it's still less of a hack than getting Bluetooth LE working in Node/Deno