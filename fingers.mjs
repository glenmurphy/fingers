// The public version of AutoPilot only supports scrolling up; have submitted a PR here:
// https://github.com/littledivy/autopilot-deno/pull/37
// 
// But until that is merged, I'm using a local development build - un/comment the following
// lines as appropriate for your setup. My build also has a longer hold time for mouse
// clicks so they better register in DCS.

// import AutoPilot from 'https://raw.githubusercontent.com/divy-work/autopilot-deno/master/mod.ts';
import AutoPilot from '../../autopilot/autopilot-deno/mod.ts'; // dev build
import { Buttons } from "./buttons.mjs"

class Fingers {
  constructor() {
    this.useRightHand = false;
    
    this.pilot = new AutoPilot();
    this.enabled = true;
    this.lastClickTime = 0;
    this.start();

    console.log("Remember to open the Bluetooth UI at ./loop/index.html");
  }

  async start() {
    this.screenSize = await this.pilot.screenSize(); // height, width;

    this.screenCenter = {
      x : this.screenSize.width / 2,
      y : this.screenSize.height  / 2
    }

    this.resetPoint = {
      x : this.screenCenter.x,
      y : this.screenCenter.y * 1.25,
    }

    // Angle of the mount in degrees - x/y/z are the positional axis the angle impacts; these 
    // have the effect of pushing the cursor in the direction of the offset, so you can use this
    // if you want your cursor pushed more in a specific direction
    this.mountAngleOffset = {
      x : 0,   // horizontal (how many degrees RIGHT the leap is pointing)
      y : 0,   // clockwise rotation (not used)
      z : 10   // vertical (how many degrees DOWN the leap is pointing)
    }

    // Position of the eye relative to the leap (in mm, using the Leap coordinate system), in the
    // leap's rotation system (so 'down' is parallel to the screen)
    this.eyePositionOffset = {
      x : 0, // horizontal distance from leap to eye; negative means the leap is right of the eye
      y : -110, // depth distance from leap to eye; negative means the leap is in front of the eye
      z : -73   // vertical distance from leap to eye; negative means the leap is above the eye
    }

    // scaling angle to screen pixels / HMD fov
    // This can change as you change the position offset and ideally we would have a calibration
    // phase. You can test this by putting your hand in a fixed location and rotating your head:
    // - if the cursor stays in the same place in the cockpit you have this correct
    // - if the cursor seems to be pulled with the head, the values are too low (and vice-versa)
    this.inputAngleScale = {
      x : 16,
      y : 24
    }
    
    this.connect();
    this.buttons = new Buttons(this.handleButton.bind(this));
  }

  connect() {
    this.disconnect();

    // Leap WebSocket API
    this.ws = new WebSocket("ws://localhost:6437/v7.json");
    this.ws.onopen = this.handleOpen.bind(this);
    this.ws.onmessage = this.handleMessage.bind(this);
    this.ws.onclose = this.connect.bind(this);
  }

  disconnect() {
    if (!this.ws) return;
    try {
      this.ws.close();
    } catch(e) {}
  }

  toggleEnabled() {
    if (this.enabled) {
      console.log("Disabling...");
      this.moveMouse(this.resetPoint.x, this.resetPoint.y);
      this.enabled = false;
    } else {
      console.log("Enabling...");
      this.enabled = true;
    }
  }

  handleOpen() {
    this.ws.send(JSON.stringify({"background": true}));  // optimize for running in the background
    this.ws.send(JSON.stringify({"focused": true}));     // claim focus
    this.ws.send(JSON.stringify({"optimizeHMD": true})); // optimize for HMDs
  }

  handleButton(type) {
    console.log(type);
    
    if (type == "up" || !this.enabled) {
      this.toggleEnabled();
      return;
    }
    
    if (type == "center")
      this.pilot.click("left")
    if (type == (this.useRightHand ? "back" : "fwd"))
      this.pilot.click("right")

    if (type == (this.useRightHand ? "fwd" : "back"))
      this.pilot.scroll("up");
    if (type == "down")
      this.pilot.scroll("down");
    
    this.lastClickTime = new Date().getTime();
  }

  moveMouse(x, y) {
    x = Math.max(0, Math.min(x, this.screenSize.width - 1));
    y = Math.max(0, Math.min(y, this.screenSize.height - 1));

    if (this.enabled && this.lastClickTime < new Date().getTime() - 100) {
      this.pilot.moveMouse(x, y);
    }
  }

  handleMessage(e) {
    try {
      var data = JSON.parse(e.data);
      if (data['hands'])
        this.handleHands(data);
    } catch(e) {
      console.log(e);
      console.log("Unexpected hand data");
    }
  }

  findPreferredHand(hands) {
    for (const hand of hands) {
      if (hand['type'] == (this.useRightHand ? "right" : "left")) {
        return hand;
      }
    }
  }

  handleHands(data) {
    const hand = this.findPreferredHand(data['hands']);
    if (!hand) return;

    const pointables = data['pointables'];
    for (const pointable of pointables) {
      if (pointable['handId'] != hand['id']) continue;
      if (pointable['type'] != 1) continue;

       // Knuckle of the index finger
      this.handlePointer(pointable['mcpPosition']);
      break;
    }
  }

  handlePointer(pos) {
    var x = -pos[0] - this.eyePositionOffset.x; // horizontal position
    var y = pos[1] - this.eyePositionOffset.y;  // depth
    var z = pos[2] + this.eyePositionOffset.z;  // vertical position

    //console.log(`${parseInt(x)}, ${parseInt(y)}, ${parseInt(z)}`)

    var h = Math.atan(x / y) * 180 / Math.PI + this.mountAngleOffset.x;
    var v = Math.atan(z / y) * 180 / Math.PI + this.mountAngleOffset.z;

    if (!this.enabled)
      return;

    this.moveMouse(
      parseInt(this.screenCenter.x + h * this.inputAngleScale.x),
      parseInt(this.screenCenter.y + v * this.inputAngleScale.y));
  }
}

if (import.meta.main) {
  new Fingers();
}