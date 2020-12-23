// A websockets server to accept button presses from a Chrome tab that is using the
// web bluetooth API (because Deno doesn't implement it yet)
import { serve } from "https://deno.land/std/http/server.ts";
import {
  acceptWebSocket,
} from "https://deno.land/std@0.81.0/ws/mod.ts";

export class Buttons {
  constructor(buttonCallback) {
    this.buttonCallback = buttonCallback;
    this.start();
  }

  async start() {
    for await (const req of serve(`:18000`)) {
      const { conn, r: bufReader, w: bufWriter, headers } = req;
      acceptWebSocket({ conn, bufReader, bufWriter, headers, })
        .then(this.handleWs.bind(this))
        .catch(async (err) => {
          console.error(`failed to accept websocket: ${err}`);
          await req.respond({ status: 400 });
        });
    }
  }

  async handleWs(sock) {
    console.log("Chrome connected");
    try {
      for await (const ev of sock) {
        if (typeof ev === "string") {
          this.buttonCallback(ev);
        }
      }
    } catch (err) {
      console.error(`failed to receive frame: ${err}`);
      if (!sock.isClosed) {
        await sock.close(1000).catch(console.error);
      }
    }
  }
}