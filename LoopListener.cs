using System;
using System.Collections.Generic;
using System.Threading.Tasks; // Dictionary
using Windows.Storage.Streams; // DataReader

// Bluetooth
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

enum LoopButton {
  CENTER,
  UP,
  DOWN,
  FWD,
  BACK
}

class LoopListener {
  Fingers fingers;
  private static Dictionary<ulong, BluetoothLEDevice> loops = new Dictionary<ulong, BluetoothLEDevice>();
  private static Dictionary<ulong, GattSession> sessions = new Dictionary<ulong, GattSession>();
  private static Dictionary<string, GattCharacteristic> characteristics = new Dictionary<string, GattCharacteristic>();

  private Guid loopService = new Guid("39de08dc-624e-4d6f-8e42-e1adb7d92fe1");
  private Guid loopChar = new Guid("53b2ad55-c810-4c75-8a25-e1883a081ef6");

  public void CharHandler(GattCharacteristic sender, GattValueChangedEventArgs args) {
    var reader = DataReader.FromBuffer(args.CharacteristicValue);
    byte[] input = new byte[reader.UnconsumedBufferLength];
    reader.ReadBytes(input);

    uint pressed = input[4];

    if ((pressed & 1) == 1)
      fingers.HandleButton(LoopButton.CENTER);
    if ((pressed & 2) == 2)
      fingers.HandleButton(LoopButton.UP);
    if ((pressed & 4) == 4)
      fingers.HandleButton(LoopButton.DOWN);
    if ((pressed & 8) == 8)
      fingers.HandleButton(LoopButton.FWD);
    if ((pressed & 16) == 16)
      fingers.HandleButton(LoopButton.BACK);
  }

  /*
  public async void MaintainSubscription(GattCharacteristic characteristic) {
    while (true) {
      GattCommunicationStatus status = 
          await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
          GattClientCharacteristicConfigurationDescriptorValue.Notify);
      
      if (status == GattCommunicationStatus.Success) {
        Console.WriteLine("Maintained");
      }

      await Task.Delay(10000);
    }
  }
  */

  public async void Subscribe(GattCharacteristic characteristic) {
    GattCommunicationStatus status =
      await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
        GattClientCharacteristicConfigurationDescriptorValue.Notify);

    if (status == GattCommunicationStatus.Success) {
      characteristic.ValueChanged += CharHandler;
      Console.WriteLine("Loop paired");
    } else {
      Console.WriteLine("Error pairing with Loop");
    }
    //MaintainSubscription(characteristic);

    characteristics.Add(characteristic.Uuid.ToString(), characteristic);
  }

  public async void ConnectDevice(ulong addr) {
    if (loops.ContainsKey(addr))
      return;

    BluetoothLEDevice loop = await BluetoothLEDevice.FromBluetoothAddressAsync(addr);

    // Maintain the connection
    GattSession s = await GattSession.FromDeviceIdAsync(BluetoothDeviceId.FromId(loop.DeviceId));
    s.MaintainConnection = true;

    GattDeviceServicesResult serviceResult = await loop.GetGattServicesAsync(BluetoothCacheMode.Uncached);
    GattCharacteristicsResult charResult;

    foreach (var service in serviceResult.Services) {
      if (service.Uuid != loopService) continue;

      charResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
      foreach (GattCharacteristic characteristic in charResult.Characteristics) {
        if (characteristic.Uuid != loopChar) continue;
        Subscribe(characteristic);
      }
    }

    loops.Add(addr, loop);
    sessions.Add(addr, s);
  }

  public void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs) {
    foreach (var p in eventArgs.Advertisement.ServiceUuids) {
      if (p == loopService) {
        ConnectDevice(eventArgs.BluetoothAddress);
      }
    }
  }

  public LoopListener(Fingers parent) {
    fingers = parent;
    // https://docs.microsoft.com/en-us/windows/uwp/devices-sensors/gatt-client

    var watcher = new BluetoothLEAdvertisementWatcher();
    watcher.ScanningMode = BluetoothLEScanningMode.Active;

    // Only activate the watcher when we're recieving values >= -80
    watcher.SignalStrengthFilter.InRangeThresholdInDBm = -80;

    // Stop watching if the value drops below -90 (user walked away)
    watcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -90;

    // Register callback for when we see an advertisements
    watcher.Received += OnAdvertisementReceived;

    // Wait 5 seconds to make sure the device is really out of range
    watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(5000);
    watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(2000);

    // Starting watching for advertisements
    watcher.Start();
  }
}