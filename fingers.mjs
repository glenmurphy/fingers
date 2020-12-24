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

    // Adjusts smoothing; 1 is no smoothing, 0 is so smooth it doesn't move. Experimental to see
    // if it's necessary (the Leap doesn't jitter much, and it might slow head rotation)
    this.trackingSpeed = 0.5; 

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

    // Position of the mount relative to eyes
    this.mountPositionOffset = {
      x : 0,
      y : 140, // depth
      z : 50  // height
    }

    // offset (mostly used to offset Leap mounting angles)
    this.mountAngleOffset = {
      x : 0,  // HMD is mounted pointed to the left
      y : 5   // HMD is slightly down
    }

    // scaling angle to screen pixels / HMD fov
    this.inputAngleScale = {
      x : 12,
      y : 18
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
    var x = -pos[0] + this.mountPositionOffset.x; // horizontal position
    var y = pos[1] + this.mountPositionOffset.y;  // depth
    var z = pos[2] + this.mountPositionOffset.z;  // vertical position

    var h = Math.atan(x / y) * 180 / Math.PI + this.mountAngleOffset.x;
    var v = Math.atan(z / y) * 180 / Math.PI + this.mountAngleOffset.y;

    // Input smoothing
    if (!this.lastH) this.lastH = h;
    if (!this.lastV) this.lastV = v;

    h = this.lastH * (1 - this.trackingSpeed) + h * this.trackingSpeed;
    v = this.lastV * (1 - this.trackingSpeed) + v * this.trackingSpeed;

    this.lastH = h;
    this.lastV = v;

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