using System;
using System.Collections.Generic;
using System.Threading.Tasks; // Dictionary
using Windows.Storage.Streams; // DataReader

// Bluetooth
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

public enum LoopButton
{
  CENTER = 1,
  UP = 2,
  DOWN = 4,
  FWD = 8,
  BACK = 16
}

class LoopListener
{
  Fingers fingers;
  private Dictionary<LoopButton, Boolean> state = new Dictionary<LoopButton, Boolean>();
  private Dictionary<ulong, BluetoothLEDevice> loops = new Dictionary<ulong, BluetoothLEDevice>();
  private Dictionary<ulong, GattSession> sessions = new Dictionary<ulong, GattSession>();
  private Dictionary<string, GattCharacteristic> characteristics = new Dictionary<string, GattCharacteristic>();

  private Guid loopService = new Guid("39de08dc-624e-4d6f-8e42-e1adb7d92fe1");//);
  private Guid loopChar = new Guid("53b2ad55-c810-4c75-8a25-e1883a081ef6");//);

  public void CharHandler(GattCharacteristic sender, GattValueChangedEventArgs args, ulong addr)
  {
    DataReader reader = DataReader.FromBuffer(args.CharacteristicValue);
    byte[] input = new byte[reader.UnconsumedBufferLength];
    reader.ReadBytes(input);

    uint pressed = input[4]; // 4 for actual loop

    foreach (LoopButton button in Enum.GetValues(typeof(LoopButton)))
    {
      if ((pressed & (uint)button) == (uint)button)
      {
        if (state[button] != true)
        {
          fingers.HandleLoopEvent(button, true, addr);
          state[button] = true;
        }
      }
      else if (state[button] == true)
      {
        fingers.HandleLoopEvent(button, false, addr);
        state[button] = false;
      }
    }
  }

  public async void Subscribe(GattCharacteristic characteristic, ulong addr)
  {
    GattCommunicationStatus status =
      await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
        GattClientCharacteristicConfigurationDescriptorValue.Notify);

    if (status == GattCommunicationStatus.Success)
    {
      characteristic.ValueChanged += (sender, args) => CharHandler(sender, args, addr);//CharHandler;
      Console.WriteLine("Loop paired");

      // The characteristic can get GCed, causing us to stop getting notifications, so we 
      // keep track of it
      characteristics[characteristic.Uuid.ToString()] = characteristic;
    }
    else
    {
      Console.WriteLine("Error pairing with Loop");
    }
  }


  public async void ConnectDevice(ulong addr)
  {
    Console.WriteLine((loops.ContainsKey(addr) ? "Reconnecting" : "Connecting") + " to loop...");

    BluetoothLEDevice loop = await BluetoothLEDevice.FromBluetoothAddressAsync(addr);

    // Maintain the connection (not sure this does anything)
    GattSession s = await GattSession.FromDeviceIdAsync(BluetoothDeviceId.FromId(loop.DeviceId));
    s.MaintainConnection = true;

    GattDeviceServicesResult serviceResult = await loop.GetGattServicesAsync(BluetoothCacheMode.Uncached);

    foreach (GattDeviceService service in serviceResult.Services)
    {
      if (service.Uuid.Equals(GattServiceUuids.Battery))
      {
        GattCharacteristicsResult charResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);

        foreach (GattCharacteristic characteristic in charResult.Characteristics)
        {
          if (!characteristic.Uuid.Equals(GattCharacteristicUuids.BatteryLevel))
            continue;
          
          GattReadResult batt = await characteristic.ReadValueAsync();

          DataReader reader = DataReader.FromBuffer(batt.Value);
          byte[] input = new byte[reader.UnconsumedBufferLength];
          reader.ReadBytes(input);
          Console.WriteLine("Loop battery: {0}%", input[0]);
          break;
        }
      }
      else if (service.Uuid.Equals(loopService))
      {
        GattCharacteristicsResult charResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);

        foreach (GattCharacteristic characteristic in charResult.Characteristics)
        {
          if (characteristic.Uuid.Equals(loopChar))
          {
            Subscribe(characteristic, addr);
            break;
          }
        }
      }
    }

    // Prevent GC of the device and session
    loops[addr] = loop;
    sessions[addr] = s;
  }

  public void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
  {
    foreach (var p in eventArgs.Advertisement.ServiceUuids)
    {
      if (p == loopService)
      {
        ConnectDevice(eventArgs.BluetoothAddress);
      }
    }
  }

  public async void Watch() {
    while(true) {
      // Starting watching for advertisements
      BluetoothLEAdvertisementWatcher watcher = new BluetoothLEAdvertisementWatcher();
      watcher.ScanningMode = BluetoothLEScanningMode.Active;

      // Only activate the watcher when we're recieving values >= -80
      watcher.SignalStrengthFilter.InRangeThresholdInDBm = -80;

      // Stop watching if the value drops below -90 (user walked away)
      watcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -90;

      // Register callback for when we see an advertisements
      watcher.Received += OnAdvertisementReceived;

      // Wait 5 seconds to make sure the device is really out of range
      watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(5000);
      watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(1000);
      
      watcher.Start();
      await Task.Delay(30000);
      watcher.Stop();

      // If we've found a loop, stop this tomfoolery
      //if (loops.Count > 0)
      //  return;
    }
  }

  public LoopListener(Fingers parent)
  {
    fingers = parent;

    // Initialize loop state
    foreach (LoopButton button in Enum.GetValues(typeof(LoopButton)))
    {
      state[button] = false;
    }

    Watch();
  }
}