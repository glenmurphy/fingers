using System;
using System.Collections.Generic;
using System.Threading.Tasks; // Dictionary
using Windows.Storage.Streams; // DataReader

// BetterScanner
using System.Runtime.InteropServices;
using System.Threading;

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

// https://stackoverflow.com/questions/37307301/ble-scan-interval-windows-10/37328965
class BetterScanner {
    /// <summary>
    /// The BLUETOOTH_FIND_RADIO_PARAMS structure facilitates enumerating installed Bluetooth radios.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct BLUETOOTH_FIND_RADIO_PARAM
    {
        internal UInt32 dwSize;
        internal void Initialize()
        {
            this.dwSize = (UInt32)Marshal.SizeOf(typeof(BLUETOOTH_FIND_RADIO_PARAM));
        }
    }

    /// <summary>
    /// Closes an open object handle.
    /// </summary>
    /// <param name="handle">[In] A valid handle to an open object.</param>
    /// <returns>If the function succeeds, the return value is nonzero. If the function fails, the return value is zero. To get extended error information, call GetLastError.</returns>
    [DllImport("Kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);

    /// <summary>
    /// Finds the first bluetooth radio present in device manager
    /// </summary>
    /// <param name="pbtfrp">Pointer to a BLUETOOTH_FIND_RADIO_PARAMS structure</param>
    /// <param name="phRadio">Pointer to where the first enumerated radio handle will be returned. When no longer needed, this handle must be closed via CloseHandle.</param>
    /// <returns>In addition to the handle indicated by phRadio, calling this function will also create a HBLUETOOTH_RADIO_FIND handle for use with the BluetoothFindNextRadio function.
    /// When this handle is no longer needed, it must be closed via the BluetoothFindRadioClose.
    /// Returns NULL upon failure. Call the GetLastError function for more information on the error. The following table describe common errors:</returns>
    [DllImport("irprops.cpl", SetLastError = true)]
    static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAM pbtfrp, out IntPtr phRadio);

    [StructLayout(LayoutKind.Sequential)]
    private struct LE_SCAN_REQUEST
    {
        internal int scanType;
        internal ushort scanInterval;
        internal ushort scanWindow;
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode,
    ref LE_SCAN_REQUEST lpInBuffer, uint nInBufferSize,
    IntPtr lpOutBuffer, uint nOutBufferSize,
    out uint lpBytesReturned, IntPtr lpOverlapped);

    /// <summary>
    /// Starts scanning for LE devices.
    /// Example: BetterScanner.StartScanner(0, 29, 29)
    /// </summary>
    /// <param name="scanType">0 = Passive, 1 = Active</param>
    /// <param name="scanInterval">Interval in 0.625 ms units</param>
    /// <param name="scanWindow">Window in 0.625 ms units</param>
    public static void StartScanner(int scanType, ushort scanInterval, ushort scanWindow)
    {
        var thread = new Thread(() =>
        {
            BLUETOOTH_FIND_RADIO_PARAM param = new BLUETOOTH_FIND_RADIO_PARAM();
            param.Initialize();
            IntPtr handle;
            BluetoothFindFirstRadio(ref param, out handle);
            uint outsize;
            LE_SCAN_REQUEST req = new LE_SCAN_REQUEST { scanType = scanType, scanInterval = scanInterval, scanWindow = scanWindow };
            DeviceIoControl(handle, 0x41118c, ref req, 8, IntPtr.Zero, 0, out outsize, IntPtr.Zero);
        });
        thread.Start();
    }
}

class LoopListener
{
  Fingers fingers;
  private Dictionary<LoopButton, Boolean> state = new Dictionary<LoopButton, Boolean>();
  private Dictionary<ulong, BluetoothLEDevice> loops = new Dictionary<ulong, BluetoothLEDevice>();
  private Dictionary<ulong, GattCharacteristic> characteristics = new Dictionary<ulong, GattCharacteristic>();

  private Guid loopService = new Guid("39de08dc-624e-4d6f-8e42-e1adb7d92fe1");
  private Guid loopChar = new Guid("53b2ad55-c810-4c75-8a25-e1883a081ef6");//);

  public void CharHandler(GattCharacteristic sender, GattValueChangedEventArgs args, ulong addr)
  {
    DataReader reader = DataReader.FromBuffer(args.CharacteristicValue);
    byte[] input = new byte[reader.UnconsumedBufferLength];
    reader.ReadBytes(input);

    uint pressed = input[4]; // 4 for actual loop
    //Console.WriteLine(input);

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

  public void ConnectionStatusHandler(BluetoothLEDevice device, Object o)
  {
    Console.WriteLine("{0}: {1}", device.BluetoothAddress.ToString("X"), device.ConnectionStatus);
    if (device.ConnectionStatus == BluetoothConnectionStatus.Connected)
    {
      // ConnectDevice(device.BluetoothAddress);
    }
    if (device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
    {
      loops[device.BluetoothAddress] = null;
      characteristics[device.BluetoothAddress] = null;
    }
  }

  public async void Subscribe(GattCharacteristic characteristic, ulong addr)
  {
    GattCommunicationStatus status =
      await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
        GattClientCharacteristicConfigurationDescriptorValue.Notify);

    if (status == GattCommunicationStatus.Success)
    {
      characteristic.ValueChanged += (sender, args) => CharHandler(sender, args, addr);
      Console.WriteLine("Loop paired");

      // The characteristic can get GCed, causing us to stop getting notifications, so we 
      // keep track of it
      characteristics[addr] = characteristic;
    }
    else
    {
      Console.WriteLine("Error pairing with Loop");
    }
  }

  public async void ConnectDevice(ulong addr)
  {
    string addrString = addr.ToString("X");
    Console.WriteLine(((loops.ContainsKey(addr) ? "Reconnecting" : "Connecting") + " to loop ({0})"), addrString);

    BluetoothLEDevice loop = await BluetoothLEDevice.FromBluetoothAddressAsync(addr);
    loop.ConnectionStatusChanged += ConnectionStatusHandler;
    
    // Maintain the connection (not sure this does anything)
    Console.WriteLine("{0}: Maintaining connection ...", addrString);
    GattSession s =
        await GattSession.FromDeviceIdAsync(BluetoothDeviceId.FromId(loop.DeviceId));
    s.MaintainConnection = true;
    
    Console.WriteLine("{0}: Getting services ...", addrString);
    GattDeviceServicesResult serviceResult =
        await loop.GetGattServicesAsync(BluetoothCacheMode.Uncached);

    foreach (GattDeviceService service in serviceResult.Services)
    {
      if (service.Uuid.Equals(loopService))
      {
        Console.WriteLine("{0}: Finding characteristics ...", addrString);
        GattCharacteristicsResult charResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);

        foreach (GattCharacteristic characteristic in charResult.Characteristics)
        {
          if (characteristic.Uuid.Equals(loopChar))
          {
            Subscribe(characteristic, addr);
            // Prevent GC of the device and session
            loops[addr] = loop;
            break;
          }
        }
      }
      else if (service.Uuid.Equals(GattServiceUuids.Battery))
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
    }
    
  }

  public void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
  {
    foreach (var p in eventArgs.Advertisement.ServiceUuids)
    {
      //Console.WriteLine("{0}: {1}", eventArgs.BluetoothAddress.ToString("X"), p);
      if (p == loopService)
      {
        ConnectDevice(eventArgs.BluetoothAddress);
      }
    }
  }

  public async void Watch() {
    BluetoothLEAdvertisementWatcher watcher = new BluetoothLEAdvertisementWatcher();
    watcher.ScanningMode = BluetoothLEScanningMode.Passive;

    // Only activate the watcher when we're recieving values >= -80
    // watcher.SignalStrengthFilter.InRangeThresholdInDBm = -80;

    // Stop watching if the value drops below -90 (user walked away)
    // watcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -90;

    // Register callback for when we see an advertisements
    watcher.Received += OnAdvertisementReceived;

    // Wait 5 seconds to make sure the device is really out of range
    watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(10000);
    watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(100);
    watcher.Start();
    
    var thread = new Thread(() => {
      BetterScanner.StartScanner(0, 10, 11);
    });
    thread.Start();

    // JIGGLE THE CORD
    // https://stackoverflow.com/questions/38596667/bluetoothleadvertisementwatcher-doesnt-work
    while(true) {
      // Starting watching for advertisements
      BluetoothLEAdvertisementWatcher w = new BluetoothLEAdvertisementWatcher();
      w.ScanningMode = BluetoothLEScanningMode.Passive;
      w.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(100);
      w.Received += (s, a) => { };
      w.Start();
      await Task.Delay(2500);
      w.Stop();
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