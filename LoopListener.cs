using System;
using System.Collections.Generic;
using System.Threading.Tasks; // Dictionary
using Windows.Storage.Streams; // DataReader
using System.Diagnostics; // Debug
using System.Runtime.CompilerServices; // MethodImpl

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
class BetterScanner
{
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
    private Dictionary<ulong, uint> loopState = new Dictionary<ulong, uint>();
    private Dictionary<ulong, uint> battery = new Dictionary<ulong, uint>();
    private Dictionary<ulong, BluetoothLEDevice> loops = new Dictionary<ulong, BluetoothLEDevice>();
    private Dictionary<ulong, GattSession> sessions = new Dictionary<ulong, GattSession>();
    private Dictionary<ulong, GattDeviceService> services = new Dictionary<ulong, GattDeviceService>();
    private Dictionary<ulong, GattCharacteristic> characteristics = new Dictionary<ulong, GattCharacteristic>();

    private Guid loopService = new Guid("39de08dc-624e-4d6f-8e42-e1adb7d92fe1");
    private Guid loopChar = new Guid("53b2ad55-c810-4c75-8a25-e1883a081ef7");

    public void CharHandler(GattCharacteristic sender, GattValueChangedEventArgs args, ulong addr)
    {
        DataReader reader = DataReader.FromBuffer(args.CharacteristicValue);
        byte[] input = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(input);

        uint state = input[0]; // 4 for actual loop
        uint batt = input[1] + 100u;

        if (!battery.ContainsKey(addr) || battery[addr] != batt)
        {
            Debug.WriteLine("{0}: Battery: {1}v", addr.ToString("X"), (float)batt / 100f);
            fingers.ui.Dispatcher.Invoke(() => {
                // Disconnect the ring if the battery is too low
                // The ring should have disconnected itself by now, so this might
                // not be necessary
                if (batt < 160) {
                    Task.Run<bool>(async () => await DisconnectDevice(addr));
                    fingers.HandleLoopDisconnected(addr);
                } else {
                    fingers.HandleLoopBattery(addr, batt);
                }
            });
        }
        battery[addr] = batt;

        //Debug.WriteLine(input);
        foreach (LoopButton button in Enum.GetValues(typeof(LoopButton)))
        {
            uint b = (uint)button;

            if ((state & b) == b && (loopState[addr] & b) == 0)
            {
                fingers.ui.Dispatcher.Invoke(() => { fingers.HandleLoopEvent(button, true, addr); });
            }
            if ((state & b) == 0 && (loopState[addr] & b) == b)
            {
                fingers.ui.Dispatcher.Invoke(() => { fingers.HandleLoopEvent(button, false, addr); });
            }
        }
        loopState[addr] = state;
    }

    public void ConnectionStatusHandler(BluetoothLEDevice device, Object o)
    {
        Debug.WriteLine("{0}: {1}", device.BluetoothAddress.ToString("X"), device.ConnectionStatus);
        if (device.ConnectionStatus == BluetoothConnectionStatus.Connected)
        {
            // ConnectDevice(device.BluetoothAddress);
        }
        if (device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
        {
            fingers.ui.Dispatcher.Invoke(() => { fingers.HandleLoopDisconnected(device.BluetoothAddress); });

            Task.Run<bool>(async () => await DisconnectDevice(device.BluetoothAddress));
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
            Debug.WriteLine("Paired", addr.ToString("X")); // I actually don't understand how this outputs [addr]: paired

            fingers.ui.Dispatcher.Invoke(() => { fingers.HandleLoopConnected(addr); });

            // The characteristic can get GCed, causing us to stop getting notifications, so we 
            // keep track of it
            characteristics[addr] = characteristic;

            // Set up button state
            loopState[addr] = 0;
        }
        else
        {
            Debug.WriteLine("{0}: PAIRING ERROR", addr.ToString("X"));
        }
    }

    public async void ConnectDevice(ulong addr)
    {
        string addrString = addr.ToString("X");

        Debug.WriteLine("{0}: {1}",
          addrString, loops.ContainsKey(addr) ? "Updating connection" : "Connecting");
        fingers.ui.Dispatcher.Invoke(() => { fingers.HandleLoopConnecting(addr); });
        BluetoothLEDevice loop = await BluetoothLEDevice.FromBluetoothAddressAsync(addr);
        loop.ConnectionStatusChanged += ConnectionStatusHandler;

        // Maintain the connection (not sure this does anything)
        //Debug.WriteLine("{0}: Maintaining connection ...", addrString);
        GattSession s =
            await GattSession.FromDeviceIdAsync(BluetoothDeviceId.FromId(loop.DeviceId));
        //s.MaintainConnection = true;
        
        //Debug.WriteLine("{0}: Getting services ...", addrString);
        GattDeviceServicesResult serviceResult =
            await loop.GetGattServicesAsync(BluetoothCacheMode.Uncached);

        foreach (GattDeviceService service in serviceResult.Services)
        {
            if (service.Uuid.Equals(loopService))
            {
                //Debug.WriteLine("{0}: Finding characteristics ...", addrString);
                GattCharacteristicsResult charResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);

                foreach (GattCharacteristic characteristic in charResult.Characteristics)
                {
                    if (characteristic.Uuid.Equals(loopChar))
                    {
                        Subscribe(characteristic, addr);

                        // Prevent GC of the device and session
                        loops[addr] = loop;
                        sessions[addr] = s;
                        services[addr] = service;
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
                    Debug.WriteLine("Loop battery: {0}%", input[0]);
                    break;
                }

                service.Dispose();
            }
        }
    }

    public void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
    {
        foreach (var p in eventArgs.Advertisement.ServiceUuids)
        {
            //Debug.WriteLine("{0}: {1}", eventArgs.BluetoothAddress.ToString("X"), p);
            if (p == loopService)
            {
                ConnectDevice(eventArgs.BluetoothAddress);
            }
        }
    }


    [MethodImpl(MethodImplOptions.NoOptimization)]
    public async void Watch()
    {
        // Speeds up connection/detection
        BetterScanner.StartScanner(0, 15, 16);

        // JIGGLE THE CORD - we need to restart BLEAdWatcher because it's not good at finding
        // things that didn't exist before it started running (like sleeping rings)
        // https://stackoverflow.com/questions/38596667/bluetoothleadvertisementwatcher-doesnt-work
        while (true)
        {
            // Starting watching for advertisements
            BluetoothLEAdvertisementWatcher w = new BluetoothLEAdvertisementWatcher();
            w.ScanningMode = BluetoothLEScanningMode.Passive;
            w.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(8000);
            w.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(20);
            w.Received += OnAdvertisementReceived;

            // Need to try/catch in case bluetooth isn't enabled (UWP doesn't appear to have great
            // APIs for detecting this
            try
            {
                w.Start();
                await Task.Delay(8000);
                w.Stop();
            }
            catch
            {
                break;
            }
        }
    }

    public LoopListener(Fingers parent)
    {
        fingers = parent;
        Watch();
    }

    public async Task<bool> DisconnectDevice(ulong addr)
    {
        if (characteristics.ContainsKey(addr))
        {
            var result = await characteristics[addr].WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
            if (result != GattCommunicationStatus.Success)
            {
                Debug.WriteLine("Unsubscribe failure", addr.ToString("X"));
            } 
            characteristics[addr].ValueChanged -= (sender, args) => CharHandler(sender, args, addr);
            characteristics[addr] = null;
        }

        if (sessions.ContainsKey(addr))
        {
            sessions[addr].MaintainConnection = false;
            sessions[addr].Dispose();
            sessions[addr] = null;
        }

        if (services.ContainsKey(addr))
        {
            services[addr].Dispose();
            services[addr] = null;
        }

        if (loops.ContainsKey(addr))
        {
            loops[addr].ConnectionStatusChanged -= ConnectionStatusHandler;
            loops[addr].Dispose();
            loops[addr] = null;
        }

        GC.Collect();
        Debug.WriteLine("DisconnectDevice", addr.ToString("X"));
        return true;
    }


    public async Task<bool> Closing()
    {
        // we need to find a way to send a disconnected event
        // to the loops so they can go to sleep; this doesn't
        // actually work, and it's unclear if there is a way 
        // to make it work
        // 
        // See https://stackoverflow.com/questions/47702584/bluetooth-le-device-cannot-disconnect-in-win-10-iot-uwp-application
        // https://github.com/jasongin/noble-uwp/issues/20
        // https://stackoverflow.com/questions/39599252/windows-ble-uwp-disconnect

        List<ulong> addrs = new List<ulong>();
        foreach (var item in loops)
        {
            addrs.Add(item.Key);
        }

        foreach(ulong addr in addrs)
        {
            await DisconnectDevice(addr);
        }

        characteristics.Clear();
        sessions.Clear();
        services.Clear();
        loops.Clear();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        await Task.Delay(500);
        return true;
    }
}