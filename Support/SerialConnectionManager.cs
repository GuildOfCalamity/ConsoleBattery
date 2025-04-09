using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;
using Windows.Storage.Streams;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ConsoleBattery;

public class SerialConnectionManager
{
    SerialDevice? _serialDevice;
    DataReader? _dataReader;
    DataWriter? _dataWriter;
    CancellationTokenSource? _readCts;
    public event TypedEventHandler<SerialDevice, ErrorReceivedEventArgs>? ErrorReceived;

    public bool IsConnected => _serialDevice != null;

    public async Task<bool> ConnectAnyAsync(uint baudRate = 9600)
    {
        string selector = SerialDevice.GetDeviceSelector();
        var devices = await DeviceInformation.FindAllAsync(selector);

        if (devices.Count == 0)
            return false;

        foreach(var device in devices)
        {
            if (!device.Id.StartsWith("\\\\?\\BTHENUM", StringComparison.OrdinalIgnoreCase))
            {
                _serialDevice = await SerialDevice.FromIdAsync(device.Id);
                if (_serialDevice != null)
                {
                    Console.WriteLine($" 🔔 Serial connection will use '{_serialDevice.PortName}' ");
                    break;
                }
            }
        }

        if (_serialDevice == null)
        {
            Console.WriteLine($" ⚠️ A proper serial connection wasn't found. ");
            return false;
        }

        _serialDevice.BaudRate = baudRate;
        _serialDevice.Parity = SerialParity.None;
        _serialDevice.StopBits = SerialStopBitCount.One;
        _serialDevice.DataBits = 8;
        _serialDevice.Handshake = SerialHandshake.None;
        _serialDevice.ReadTimeout = TimeSpan.FromMilliseconds(3000);
        _serialDevice.WriteTimeout = TimeSpan.FromMilliseconds(3000);
        _serialDevice.ErrorReceived += OnErrorReceived;

        _dataReader = new DataReader(_serialDevice.InputStream);
        _dataReader.InputStreamOptions = InputStreamOptions.Partial;

        _dataWriter = new DataWriter(_serialDevice.OutputStream);

        _readCts = new CancellationTokenSource();
        StartReadLoop(_readCts.Token);

        return true;
    }

    public async Task<bool> ConnectAsync(string deviceSelectorSubstring, uint baudRate = 9600)
    {
        string selector = SerialDevice.GetDeviceSelector(deviceSelectorSubstring);
        var devices = await DeviceInformation.FindAllAsync(selector);

        if (devices.Count == 0)
            return false;

        _serialDevice = await SerialDevice.FromIdAsync(devices[0].Id);
        if (_serialDevice == null)
            return false;

        _serialDevice.BaudRate = baudRate;
        _serialDevice.Parity = SerialParity.None;
        _serialDevice.StopBits = SerialStopBitCount.One;
        _serialDevice.DataBits = 8;
        _serialDevice.Handshake = SerialHandshake.None;
        _serialDevice.ReadTimeout = TimeSpan.FromMilliseconds(3000);
        _serialDevice.WriteTimeout = TimeSpan.FromMilliseconds(3000);
        _serialDevice.ErrorReceived += OnErrorReceived;

        _dataReader = new DataReader(_serialDevice.InputStream);
        _dataReader.InputStreamOptions = InputStreamOptions.Partial;

        _dataWriter = new DataWriter(_serialDevice.OutputStream);

        _readCts = new CancellationTokenSource();
        StartReadLoop(_readCts.Token);

        return true;
    }

    async void StartReadLoop(CancellationToken token)
    {
        try
        {
            if (_serialDevice != null)
                Console.WriteLine($" 🔔 Listening on '{_serialDevice.PortName}' ");

            while (!token.IsCancellationRequested)
            {
                uint bytesToRead = await _dataReader?.LoadAsync(1024)?.AsTask(token);

                if (bytesToRead > 0)
                {
                    var received = _dataReader?.ReadString(bytesToRead);
                    Console.WriteLine($" ✅ Received: {received}");
                }
            }
        }
        catch (TaskCanceledException) { /* graceful exit */ }
        catch (Exception ex)
        {
            Console.WriteLine($" ⚠️ Read error: {ex}");
        }
    }

    public async Task SendAsync(string data)
    {
        if (_dataWriter == null) return;

        try
        {
            Console.WriteLine($" 🔔 Sending '{data}' ");

            _dataWriter.WriteString(data);
            await _dataWriter.StoreAsync();
            //await _dataWriter.FlushAsync(); // This throws an exception
        }
        catch (Exception ex)
        {
            Console.WriteLine($" ⚠️ Send error: {ex}");
        }
    }

    public void Dispose()
    {
        Disconnect();
        _readCts?.Dispose();
    }

    public void Disconnect()
    {
        Console.WriteLine($" 🔔 Disconnecting ");

        if (_serialDevice != null)
            _serialDevice.ErrorReceived -= OnErrorReceived;

        _readCts?.Cancel();
        _dataReader?.DetachStream();
        _dataWriter?.DetachStream();

        _dataReader?.Dispose();
        _dataWriter?.Dispose();
        _serialDevice?.Dispose();

        _dataReader = null;
        _dataWriter = null;
        _serialDevice = null;
    }

    void OnErrorReceived(SerialDevice sender, ErrorReceivedEventArgs args)
    {
        var error = args.Error;

        System.Diagnostics.Debug.WriteLine($"[WARNING] Serial error: {error}");

        // Optional: Raise external event
        ErrorReceived?.Invoke(sender, args);

        // Optional: Act on specific error
        switch (error)
        {
            case SerialError.Frame:
            case SerialError.ReceiveParity:
            case SerialError.BufferOverrun:
            case SerialError.ReceiveFull:
            case SerialError.TransmitFull:
                Logger.Log($"Serial error detected: {error}");
                break;
        }
    }

    // Static method to get list of serial devices
    public static async Task<DeviceInformationCollection> GetAvailablePortsAsync()
    {
        string aqs = SerialDevice.GetDeviceSelector();
        return await DeviceInformation.FindAllAsync(aqs);
    }
}
