using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Devices.Power;
using Windows.Devices.SerialCommunication;
using Windows.Devices.WiFi;
using Windows.Devices.WiFiDirect;
using Windows.Graphics.Imaging;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Profile;

using WinRT;

namespace ConsoleBattery;

public class Program
{
    public static bool BatteryMode { get; private set; } = true;
    public static bool Verbose { get; private set; } = true;
    public static string Title { get; private set; } = "BatteryMonitor";
    public static Windows.System.Power.BatteryStatus LastStatus { get; private set; } = Windows.System.Power.BatteryStatus.NotPresent;
    static ValueStopwatch watch { get; set; }
    static CancellationTokenSource? CurrentTaskCTS { get; set; }
    static IntPtr _consoleHandle = IntPtr.Zero;
    public static IntPtr ConsoleHandle
    {
        get
        {
            if (_consoleHandle == IntPtr.Zero)
            {
                try { _consoleHandle = ConsoleHelper.GetConsoleWindow(); }
                catch (Exception ex) { Debug.WriteLine($"ConsoleHandle: {ex.Message}"); }
            }
            return _consoleHandle;
        }
    }
    static Config? _localConfig;
    public static Config? LocalConfig
    {
        get => _localConfig;
        set => _localConfig = value;
    }

    /// <summary>
    /// Entry point
    /// </summary>
    static void Main(string[] args)
    {
        System.Console.Title = Title;
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;

        CurrentTaskCTS = new CancellationTokenSource();

        Logger.SetLoggerFolderPath(AppDomain.CurrentDomain.BaseDirectory);

        #region [Event Handling]
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            System.Console.Title = $"{Title} - ERROR";
            System.Console.CursorVisible = true;
            Console.WriteLine($"{Environment.NewLine} ⚠️ UNHANDLED EXCEPTION ⚠️ {Environment.NewLine}");
            Console.WriteLine($" 📣 {(e.ExceptionObject as Exception)?.Message}{Environment.NewLine}");
            Logger.Log((e.ExceptionObject as Exception), "UNHANDLED EXCEPTION");
            Environment.Exit(0);
        };
        System.Console.CancelKeyPress += (sender, e) =>
        {
            System.Console.Title = $"{Title} - QUIT";
            System.Console.CursorVisible = true;
            Console.WriteLine($"{Environment.NewLine} ⚠️ USER EXIT ⚠️ {Environment.NewLine}");
            Logger.Log("Ctrl-Break detected from user.", "USER EXIT");
            Environment.Exit(0);
        };
        #endregion

        watch = ValueStopwatch.StartNew();

        #region [Load Config]
        if (ConfigHelper.DoesConfigExist())
        {
            try
            {
                LocalConfig = ConfigHelper.LoadConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($" ⚠️ LoadConfig: {ex.Message}");
            }
        }
        else // create default config if not found
        {
            try
            {
                LocalConfig = new Config
                {
                    firstRun = true,
                    version = $"{Extensions.GetCurrentAssemblyVersion()}",
                    time = DateTime.Now,
                    refresh = 3000, // ms
                    lastRate = 17000, // mW
                };
                ConfigHelper.SaveConfig(LocalConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($" ⚠️ SaveConfig: {ex.Message}");
            }
        }
        #endregion

        if (BatteryMode || (args.Length > 0 && args[0].StartsWith("battery", StringComparison.OrdinalIgnoreCase)))
        {
            #region [May not work with Terminal, but does work with the classic CMD]
            ConsoleHelper.RECT rect = new ConsoleHelper.RECT();
            ConsoleHelper.GetWindowRect(ConsoleHandle, out rect);
            if (rect.Right == 0 || rect.Bottom == 0)
            {
                ConsoleHelper.MoveWindow(ConsoleHandle, 50, 40, 200, 100, true);
                //ConsoleHelper.SetWindowPos(ConsoleHandle, IntPtr.Zero, 50, 40, 200, 100, ConsoleHelper.SWP_NOZORDER | ConsoleHelper.SWP_NOACTIVATE);
            }
            else
            {
                ConsoleHelper.MoveWindow(ConsoleHandle, 50, 40, rect.Right - rect.Left, rect.Bottom - rect.Top, true);
                //ConsoleHelper.SetWindowPos(ConsoleHandle, IntPtr.Zero, 50, 40, rect.Right - rect.Left, rect.Bottom - rect.Top, ConsoleHelper.SWP_NOZORDER | ConsoleHelper.SWP_NOACTIVATE);
            }
            //System.Console.SetWindowSize(300, 100);
            //System.Console.SetBufferSize(300, 100);
            #endregion

            System.Console.Title = Title;
            bool running = true;
            ConsoleKey ck = ConsoleKey.NoName;
            System.Console.CursorVisible = false;

            // Gets a Battery object that represents all battery controllers connected to the device.
            Battery battery = Battery.AggregateBattery;
            ThreadPool.QueueUserWorkItem((object? obj) =>
            {
                while (running)
                {
                    UpdateBattery(battery, LocalConfig != null ? LocalConfig.refresh : 3000);
                }
            });

            ck = System.Console.ReadKey(true).Key;
            while (ck != ConsoleKey.Escape)
            {
                System.Console.Clear();
                Console.WriteLine($"{Environment.NewLine} ══ press <Esc> to exit ══ {Environment.NewLine}");
                ck = System.Console.ReadKey(true).Key;
            }
            System.Console.CursorVisible = true;
            running = false;
            Console.WriteLine($"{Environment.NewLine} ⚠️ Battery Monitor Closing ⚠️  ");

            var elapsed = watch.GetElapsedFriendly();
            Console.WriteLine($"{Environment.NewLine} ⏱️ Elapsed Time : {elapsed}");
            Logger.Log($"⏱️ Application instance ran for {elapsed}");

            Thread.Sleep(1500);
        }

        if (LocalConfig is not null)
        {
            LocalConfig.firstRun = false;
            LocalConfig.time = DateTime.Now;
            LocalConfig.version = $"{Extensions.GetCurrentAssemblyVersion()}";
            _ = ConfigHelper.SaveConfig(LocalConfig);
        }

        Logger.ConfirmLogIsFlushed(2000);
    }

    static void UpdateBattery(Battery battery, int msDelay)
    {
        if (battery is null)
            return;

        try
        {
            Console.SetCursorPosition(0, 0);
            Console.WriteLine($" 📝 Battery Report {DateTime.Now.ToLongTimeString()}   ");
            Console.WriteLine($"════════════════════════════════════   ");
            
            BatteryReport batteryReport = battery.GetReport();

            // If the battery status has changed then bring our window to the foreground.
            if (LastStatus != batteryReport.Status)
            {
                if (ConsoleHandle != IntPtr.Zero)
                    ConsoleHelper.SetForegroundWindow(ConsoleHandle);
                
                LastStatus = batteryReport.Status;
            }
            
            Console.WriteLine($" Status............: {LastStatus}         ");
            Console.WriteLine($" ChargeRate........: {Extensions.FormatMilliwatts(batteryReport.ChargeRateInMilliwatts)}        ");
            Console.WriteLine($" DesignCapacity....: {Extensions.FormatMilliwatts(batteryReport.DesignCapacityInMilliwattHours)}h      ");
            Console.WriteLine($" FullChargeCapacity: {Extensions.FormatMilliwatts(batteryReport.FullChargeCapacityInMilliwattHours)}h        ");
            Console.WriteLine($" RemainingCapacity.: {Extensions.FormatMilliwatts(batteryReport.RemainingCapacityInMilliwattHours)}h        ");

            if (batteryReport.ChargeRateInMilliwatts != null && batteryReport.ChargeRateInMilliwatts < 0)
            {
                Console.WriteLine($" TimeRemaining.....: {Extensions.MilliwattHoursToMinutes(batteryReport.RemainingCapacityInMilliwattHours, batteryReport.ChargeRateInMilliwatts)}         ");
                if (LocalConfig != null && batteryReport.ChargeRateInMilliwatts != null)
                    LocalConfig.lastRate = Math.Abs((int)batteryReport.ChargeRateInMilliwatts);
            }
            else if (LocalConfig != null && LocalConfig.lastRate > 0) // show a time based on the last power drain
            {
                Console.WriteLine($" EstimatedRemaining: {Extensions.MilliwattHoursToMinutes(batteryReport.RemainingCapacityInMilliwattHours, LocalConfig.lastRate)}         ");
            }
            else // empty line
            {
                Console.WriteLine(new string(' ', 50));
            }

            DrawBattery(batteryReport.RemainingCapacityInMilliwattHours, batteryReport.FullChargeCapacityInMilliwattHours);
            
            Thread.Sleep(msDelay);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CheckBattery: {ex.Message}");
            Logger.Log(ex);
        }
        finally
        {
            Console.WriteLine();
        }
    }

    /// <summary>
    /// e.g. DrawBattery(50, 100, 8, 24, true, true, '#'); ░─▒─▓
    /// </summary>
    static void DrawBattery(int? currentValue, int? maxValue, int yPos = 8, int length = 35, bool showPercent = true, bool useOutline = true, char drawChar = '░')
    {
        if (currentValue == null || maxValue == null)
            return;

        int barCount = 3;

        if (useOutline)
            barCount = 5;

        Console.SetCursorPosition(0, yPos);

        // Calculate the percentage of charge
        int percentage = (int)Math.Round(((double)currentValue.Value / maxValue.Value) * 100);

        // Calculate the number of battery characters to draw
        int barLength = (int)Math.Round(((double)percentage / 100) * length);

        // Draw the meter bars
        for (int b = 0; b < barCount; b++)
        {
            #region [left end cap]
            if (b == 0)
                System.Console.Write(" ┌");
            else if (b == 1 && barCount == 3)
                System.Console.Write(" │");
            else if (b == 1 && barCount == 2)
                System.Console.Write(" └");
            else if (b == 1 && barCount == 4)
                System.Console.Write(" │");
            else if (b == 1 && barCount == 5)
                System.Console.Write(" │");
            else if (b == 2 && barCount == 3)
                System.Console.Write(" └");
            else if (b == 2 && barCount == 4)
                System.Console.Write(" │");
            else if (b == 2 && barCount == 5)
                System.Console.Write(" │");
            else if (b == 3 && barCount == 4)
                System.Console.Write(" └");
            else if (b == 3 && barCount == 5)
                System.Console.Write(" │");
            else if (b == 4 && barCount == 5)
                System.Console.Write(" └");
            #endregion

            #region [outline & fill]
            if (useOutline && (b == 0 || b == barCount - 1))
            {
                for (int l = 0; l < length; l++)
                {
                    System.Console.Write("─");
                }
            }
            else
            {
                for (int i = 0; i < barLength; i++)
                {
                    System.Console.Write(drawChar);
                }
                for (int i = barLength; i < length; i++)
                {
                    System.Console.Write(" ");
                }
            }
            #endregion

            #region [right end cap]
            if (b == 0)
                System.Console.Write("┐ ");
            else if (b == 1 && barCount == 3)
                System.Console.Write("│ ");
            else if (b == 1 && barCount == 2)
                System.Console.Write("┘ ");
            else if (b == 1 && barCount == 4)
                System.Console.Write("│ ");
            else if (b == 1 && barCount == 5)
                System.Console.Write("│ ");
            else if (b == 2 && barCount == 3)
                System.Console.Write("┘ ");
            else if (b == 2 && barCount == 4)
                System.Console.Write("│ ");
            else if (b == 2 && barCount == 5)
                System.Console.Write("│ ");
            else if (b == 3 && barCount == 4)
                System.Console.Write("┘ ");
            else if (b == 3 && barCount == 5)
                System.Console.Write("│ ");
            else if (b == 4 && barCount == 5)
                System.Console.Write("┘ ");
            #endregion

            System.Console.WriteLine();
        }

        if (showPercent)
            System.Console.WriteLine($"{Environment.NewLine}  ⚡ Charge: {percentage}%   ");
    }

    #region [UWP Experiments]
    static async void ClipboardOnContentChanged(object? sender, object arg)
    {
        try
        {
            Debug.WriteLine($"[INFO] Clipboard arg type: '{arg?.GetType()}'");
            var dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                try
                {
                    var text = await dataPackageView.GetTextAsync();
                    Console.WriteLine($"Clipboard Text: {text}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error retrieving Text format from Clipboard: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Text format is not available in clipboard");
            }

            if (dataPackageView.Contains(StandardDataFormats.Html))
            {
                DisplayResourceMapAsync(dataPackageView);

                string htmlFormat = null;
                try
                {
                    htmlFormat = await dataPackageView.GetHtmlFormatAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error retrieving HTML format from Clipboard: {ex.Message}");
                }

                if (htmlFormat != null)
                {
                    string htmlFragment = HtmlFormatHelper.GetStaticFragment(htmlFormat);
                    Console.WriteLine("HTML:<br/ > " + htmlFragment);
                }
            }
            else
            {
                Console.WriteLine("HTML:<br/ > HTML format is not available in clipboard");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WARNING] ClipboardOnContentChanged: {ex.Message}");
            Logger.Log(ex);
        }
    }

    // Note: this sample is not trying to resolve and render the HTML using resource map.
    // Please refer to the Clipboard JavaScript sample for an example of how to use resource map
    // for local images display within an HTML format. This sample will only demonstrate how to
    // get a resource map object and extract its key values
    static async void DisplayResourceMapAsync(DataPackageView dataPackageView)
    {
        IReadOnlyDictionary<string, RandomAccessStreamReference>? resMap = null;

        try
        {
            resMap = await dataPackageView.GetResourceMapAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving Resource map from Clipboard: {ex.Message}");
            Logger.Log(ex);
        }

        Console.WriteLine("Resource map: ");
        if (resMap != null)
        {
            if (resMap.Count > 0)
            {
                foreach (var item in resMap)
                {
                    Console.WriteLine($"\t Key: {item.Key}");
                }
            }
            else
            {
                Console.WriteLine("Resource map is empty");
            }
        }
    }

    /// <summary>
    /// You can use the <see cref="StorageFile.CreateStreamedFileAsync(string, StreamedFileDataRequestedHandler, IRandomAccessStreamReference)"/>
    /// method to create a virtual StorageFile. You give it a name, a delegate, and an optional thumbnail. 
    /// When the virtual StorageFile is first accessed, your delegate will be invoked, and its job is to
    /// fill the provided output stream with data.
    /// </summary>
    public static async Task<StorageFile?> ConvertFileAsync(StorageFile originalFile)
    {
        byte[] data = Encoding.UTF8.GetBytes("example file contents");
        IBuffer buffer = data.AsBuffer();

        try
        {
            var thumb = await GetFileThumbnailStreamReferenceAsync(originalFile);

            // The temp file will normally appear here "C:\Users\AccountName\AppData\Local\Temp\sample.txt"
            return await StorageFile.CreateStreamedFileAsync("sample.txt",
               async (request) =>
               {
                   using (request)
                   {
                       await request.WriteAsync(buffer);
                   }
               }, thumb);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WARNING] ConvertFileAsync: {ex.Message}");
            Logger.Log(ex);
        }
        return null;
    }

    /// <summary>
    /// Gets <see cref="IRandomAccessStreamReference"/> for a file thumbnail. Can be used in tandem with 
    /// <see cref="StorageFile.CreateStreamedFileAsync(string, StreamedFileDataRequestedHandler, IRandomAccessStreamReference)"/>.
    /// </summary>
    public static async Task<IRandomAccessStreamReference?> GetFileThumbnailStreamReferenceAsync(StorageFile file)
    {
        if (file != null)
        {
            try
            {   // Get the thumbnail
                var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem);
                if (thumbnail != null)
                {
                    Debug.WriteLine($"[WARNING] Thumbnail size is {thumbnail.Size.ToFileSize()}");
                    // Create IRandomAccessStreamReference from the thumbnail
                    return RandomAccessStreamReference.CreateFromFile(file);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WARNING] GetFileThumbnailStreamReferenceAsync: {ex.Message}");
                Logger.Log(ex);
            }
        }
        return null; // Return null if no file or thumbnail is found
    }

    public static async void CreateStreamedFile()
    {
        // Create a streamed file.
        StorageFile file = await StorageFile.CreateStreamedFileAsync("file.txt", StreamedFileWriter, null);

        // Prepare to copy the file (don't use "ApplicationData.Current.LocalFolder" with console app)
        StorageFolder localFolder = await StorageFolder.GetFolderFromPathAsync(AppContext.BaseDirectory);
        string newName = "copied_file.txt";

        // Copy the streamed file. At this point, the data is streamed into the source file.
        await file.CopyAsync(localFolder, newName, NameCollisionOption.ReplaceExisting);
    }

    public static async void StreamedFileWriter(StreamedFileDataRequest request)
    {
        try
        {
            using (var stream = request.AsStreamForWrite())
            using (var streamWriter = new StreamWriter(stream))
            {
                for (int l = 0; l < 50; l++)
                {
                    await streamWriter.WriteLineAsync($"Data line #{l}.");
                }
            }
            request.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WARNING] StreamedFileWriter: {ex.Message}");
            Logger.Log(ex);
            request.FailAndClose(StreamedFileFailureMode.Incomplete);
        }
    }

    static void ListNetworks()
    {
        const double MBPS = 1000 * 1000;
        const double GB = 1024 * 1024 * 1024;

        foreach (var host in NetworkInformation.GetHostNames())
        {
            Console.WriteLine($"Host Display Name: {host.DisplayName}");
        }

        try
        {
            ConnectionProfile profile = NetworkInformation.GetInternetConnectionProfile();
            NetworkAdapter internetAdapter = profile.NetworkAdapter;

            PrintInterfaceType(internetAdapter.IanaInterfaceType);
            Console.WriteLine($" InboundMaxBps: {internetAdapter.InboundMaxBitsPerSecond.ToFileSize()}ps");
            Console.WriteLine($"OutboundMaxBps: {internetAdapter.OutboundMaxBitsPerSecond.ToFileSize()}ps");

            IReadOnlyList<HostName> hostNames = NetworkInformation.GetHostNames();
            HostName? connectedHost = hostNames?.Where
                (h => h.IPInformation != null
                && h.IPInformation.NetworkAdapter != null
                && h.IPInformation.NetworkAdapter.NetworkAdapterId == internetAdapter.NetworkAdapterId)
                .FirstOrDefault();

            if (connectedHost != null)
            {
                Console.WriteLine($"{connectedHost.CanonicalName} (Type={connectedHost.Type})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetInternetConnectionProfile: {ex.Message}");
            Logger.Log(ex);
        }
    }

    static void PrintInterfaceType(uint IanaInterfaceType)
    {
        switch (IanaInterfaceType)
        {
            case 1: Console.WriteLine($"📡 InterfaceType: Some other type of network interface.");
                break;
            case 6: Console.WriteLine($"📡 InterfaceType: An Ethernet network interface.");
                break;
            case 9: Console.WriteLine($"📡 InterfaceType: A token ring network interface.");
                break;
            case 23: Console.WriteLine($"📡 InterfaceType: A PPP network interface.");
                break;
            case 24: Console.WriteLine($"📡 InterfaceType: A software loop-back network interface.");
                break;
            case 37: Console.WriteLine($"📡 InterfaceType: An ATM network interface.");
                break;
            case 71: Console.WriteLine($"📡 InterfaceType: An IEEE 802.11 wireless network interface.");
                break;
            case 131: Console.WriteLine($"📡 InterfaceType: A tunnel type encapsulation network interface.");
                break;
            case 144: Console.WriteLine($"📡 InterfaceType: An IEEE 1394 (Firewire) high performance serial bus network interface.");
                break;
            default: Console.WriteLine($"📡 InterfaceType: UNKNOWN");
                break;
        }
    }

    static async void CheckUSB()
    {
        try
        {
            Console.WriteLine($" ⚙️ USB Report {Environment.NewLine}––––––––––––––––––––––––––––––––––");

            // Assuming you want to find USB mass storage devices
            Guid massStorage = new Guid("36fc9e60-c465-11cf-8056-444553540000");

            // SuperMutt's Interface class {875D47FC-D331-4663-B339-624001A2DC5E}
            Guid superMutt = new Guid("875D47FC-D331-4663-B339-624001A2DC5E");

            var aqs = Windows.Devices.Usb.UsbDevice.GetDeviceSelector(massStorage);
            if (string.IsNullOrEmpty(aqs))
            {
                Console.WriteLine("⚠️ No USB device selector available.");
                return;
            }

            var dispResults = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(aqs);
            foreach (var device in dispResults)
            {
                foreach (var p in device.Properties)
                {
                    Console.WriteLine($"\t USB.Key: {p.Key}   USB.Value: {p.Value}");
                }

                DeviceAccessStatus access = DeviceAccessInformation.CreateFromId(device.Id).CurrentStatus;
                if (access == DeviceAccessStatus.DeniedByUser)
                {
                    Console.WriteLine("\t This app does not have access to connect to the USB device");
                }
                else
                {
                    Console.WriteLine($"\t ⚙️ USB DeviceAccessStatus: {access}");
                }
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CheckUSB: {ex.Message}");
            Logger.Log(ex);
        }
        finally
        {
            Console.WriteLine();
        }
    }

    static async void CheckSerial()
    {
        try
        {
            Console.WriteLine($" ⚙️ Serial Report {Environment.NewLine}––––––––––––––––––––––––––––––––––");
            
            var aqs = Windows.Devices.SerialCommunication.SerialDevice.GetDeviceSelector();
            if (string.IsNullOrEmpty(aqs))
            {
                Console.WriteLine("⚠️ No serial device selector available.");
                return;
            }

            var dispResults = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(aqs);
            foreach (var device in dispResults)
            {
                foreach (var p in device.Properties)
                {
                    Console.WriteLine($"\t Serial.Key: {p.Key}   Serial.Value: {p.Value}");
                }

                DeviceAccessStatus access = DeviceAccessInformation.CreateFromId(device.Id).CurrentStatus;
                if (access == DeviceAccessStatus.DeniedByUser)
                {
                    Console.WriteLine("\t This app does not have access to connect to the serial device");
                }
                else
                {
                    Console.WriteLine($"\t ⚙️ Serial DeviceAccessStatus: {access}");
                }
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CheckSerial: {ex.Message}");
            Logger.Log(ex);
        }
        finally
        {
            Console.WriteLine();
        }
    }

    static void CheckEasClientInfo()
    {
        try
        {
            Console.WriteLine($" EAS Client Device Info {Environment.NewLine}––––––––––––––––––––––––––––––––––");
            EasClientDeviceInformation deviceInfo = new EasClientDeviceInformation();
            Console.WriteLine($"OperatingSystem: {deviceInfo.OperatingSystem}");
            Console.WriteLine($"Machine........: {deviceInfo.FriendlyName}");
            Console.WriteLine($"Make/Model.....: {deviceInfo.SystemManufacturer}/{deviceInfo.SystemProductName}");
            Console.WriteLine($"SKU............: {deviceInfo.SystemSku}");

            string familyVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong v = ulong.Parse(familyVersion);
            ulong v1 = (v & 0xFFFF000000000000L) >> 48;
            ulong v2 = (v & 0x0000FFFF00000000L) >> 32;
            ulong v3 = (v & 0x00000000FFFF0000L) >> 16;
            ulong v4 = (v & 0x000000000000FFFFL);
            Console.WriteLine($"OS Version.....: {v1}.{v2}.{v3}.{v4} ({AnalyticsInfo.VersionInfo.DeviceFamily})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CheckEasClientInfo: {ex.Message}");
            Logger.Log(ex);
        }
        finally
        {
            Console.WriteLine();
        }
    }

    static async void CheckDisplay()
    {
        try
        {
            Console.WriteLine($" 🖥️ Display Report {Environment.NewLine}––––––––––––––––––––––––––––––––––");
            var dispResults = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(Windows.Devices.Display.DisplayMonitor.GetDeviceSelector());
            foreach (var device in dispResults)
            {
                foreach (var p in device.Properties)
                {
                    Console.WriteLine($"\t Display.Key: {p.Key}   Display.Value: {p.Value}");
                }
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CheckDisplay: {ex.Message}");
            Logger.Log(ex);
        }
        finally
        {
            Console.WriteLine();
        }
    }

    static async void CheckHumanInterfaceDevice()
    {
        try
        {
            var sel = Windows.Devices.HumanInterfaceDevice.HidDevice.GetDeviceSelector((ushort)1, (ushort)0);
            var hidResults = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(sel);
            
            if (hidResults != null && hidResults.Count == 0)
            {
                Console.WriteLine("⚠️ No HID device selector available.");
                return;
            }

            foreach (var device in hidResults)
            {
                foreach (var p in device.Properties)
                {
                    Console.WriteLine($"\t HID.Key: {p.Key}   HID.Value: {p.Value}");
                }

                DeviceAccessStatus access = DeviceAccessInformation.CreateFromId(device.Id).CurrentStatus;
                if (access == DeviceAccessStatus.DeniedByUser)
                {
                    Console.WriteLine("\t This app does not have access to connect to the HID device");
                }
                else
                {
                    Console.WriteLine($"\t 📡 HID DeviceAccessStatus: {access}");
                }
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CheckHumanInterfaceDevice: {ex.Message}");
            Logger.Log(ex);
        }
        finally
        {
            Console.WriteLine();
        }
    }

    static async void CheckWiFi()
    {
        try
        {
            var WiFiDirectPairedOnly = WiFiDirectDevice.GetDeviceSelector();
            var WiFiDirect = WiFiDirectDevice.GetDeviceSelector(WiFiDirectDeviceSelectorType.AssociationEndpoint);
            
            Console.WriteLine($" 📡 WiFi Report {Environment.NewLine}––––––––––––––––––––––––––––––––––");
            var wifiResults = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(WiFiDirectPairedOnly);
            foreach (var device in wifiResults)
            {
                foreach (var p in device.Properties)
                {
                    Console.WriteLine($"\t WiFi.Key: {p.Key}   WiFi.Value: {p.Value}");
                }

                DeviceAccessStatus access = DeviceAccessInformation.CreateFromId(device.Id).CurrentStatus;
                if (access == DeviceAccessStatus.DeniedByUser)
                {
                    Console.WriteLine("\t This app does not have access to connect to the WiFi device");
                }
                else
                {
                    Console.WriteLine($"\t 📡 WiFi DeviceAccessStatus: {access}");
                }
                Console.WriteLine();
            }

            IReadOnlyList<WiFiAdapter> wiFiAdapterList;

            await Task.Run(async () =>
            {
                WiFiAccessStatus accessStatus = await WiFiAdapter.RequestAccessAsync();
                if (accessStatus != WiFiAccessStatus.Allowed)
                {
                    Console.WriteLine("🚨 WiFi access denied.");
                }
                else
                {
                    wiFiAdapterList = await WiFiAdapter.FindAllAdaptersAsync();
                    Console.WriteLine($"There are {wiFiAdapterList.Count} WiFi Adapter(s)");
                    if (wiFiAdapterList.Count > 0)
                    {
                        foreach (var wfa in wiFiAdapterList)
                        {
                            PrintInterfaceType(wfa.NetworkAdapter.IanaInterfaceType);
                        }
                    }
                    else
                    {
                        Console.WriteLine("No WiFi adapters detected on this machine.");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CheckWiFi: {ex.Message}");
            Logger.Log(ex);
        }
        finally
        {
            Console.WriteLine();
        }
    }

    public static Windows.Devices.Enumeration.DeviceClass DeviceClassSelector { get; set; } = Windows.Devices.Enumeration.DeviceClass.All;
    public static Windows.Devices.Enumeration.DeviceInformationKind DeviceKind { get; set; } = Windows.Devices.Enumeration.DeviceInformationKind.Unknown;

    static async void CheckBluetooth()
    {
        try
        {
            // Currently Bluetooth APIs don't provide a selector to get ALL devices that are both paired and non-paired.
            // Typically you wouldn't need this for common scenarios, but it's convenient to demonstrate the various sample scenarios.
            
            var BluetoothSelector = "System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\"";
            DeviceKind = DeviceInformationKind.AssociationEndpoint;

            var BluetoothUnpairedOnly = BluetoothDevice.GetDeviceSelectorFromPairingState(false);
            var BluetoothPairedOnly = BluetoothDevice.GetDeviceSelectorFromPairingState(true);

          
            var BluetoothLE = "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\"";
            DeviceKind = DeviceInformationKind.AssociationEndpoint;

            var BluetoothLEUnpairedOnly = BluetoothLEDevice.GetDeviceSelectorFromPairingState(false);
            var BluetoothLEPairedOnly = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);



            Console.WriteLine($" 📡 Bluetooth Report {Environment.NewLine}––––––––––––––––––––––––––––––––––");
            var btResults = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(Windows.Devices.Bluetooth.BluetoothAdapter.GetDeviceSelector());
            foreach (var device in btResults)
            {
                foreach (var p in device.Properties)
                {
                    Console.WriteLine($"\t BT.Key: {p.Key}   BT.Value: {p.Value}");
                }
                Console.WriteLine();
            }

            //string BluetoothDeviceAQS = "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\"";
            //var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(BluetoothDeviceAQS);
            //foreach (var device in devices) { }

            Console.WriteLine($" Additional Bluetooth Data {Environment.NewLine}––––––––––––––––––––––––––––––––––");
            var bluetooth = await Windows.Devices.Bluetooth.BluetoothAdapter.GetDefaultAsync();
            if (bluetooth == null)
            {
                Console.WriteLine(" 📡 Bluetooth is not available ");
                return;
            }
            
            // Throws an exception
            //BluetoothDeviceId? btdi = BluetoothDeviceId.FromId(bluetooth.DeviceId);
            

            //if (bluetooth.IsPeripheralRoleSupported) {
            Console.WriteLine($" 📡 Bluetooth is turned ON ");
            Console.WriteLine($" DeviceID.....................: {bluetooth.DeviceId} ");
            Console.WriteLine($" BluetoothAddress.............: {bluetooth.BluetoothAddress} ");
            Console.WriteLine($" CentralRoleSupported.........: {bluetooth.IsCentralRoleSupported} ");
            Console.WriteLine($" AdvertisementOffloadSupported: {bluetooth.IsAdvertisementOffloadSupported} ");
            Console.WriteLine($" ExtendedAdvertisingSupported.: {bluetooth.IsExtendedAdvertisingSupported} ");
            Console.WriteLine($" ClassicTransportSupported....: {bluetooth.IsClassicSupported} ");
            Console.WriteLine($" ClassicSecureSupported.......: {bluetooth.AreClassicSecureConnectionsSupported} ");
            Console.WriteLine($" LowEnergyTransportSupported..: {bluetooth.IsLowEnergySupported} ");
            Console.WriteLine($" PeripheralRoleSupported......: {bluetooth.IsPeripheralRoleSupported} ");
            
            var aqs = BluetoothDevice.GetDeviceSelectorFromBluetoothAddress(bluetooth.BluetoothAddress);
            Console.WriteLine($" AdvancedQuerySyntax..........: {aqs} ");
            //} else { Console.WriteLine(" 📡 Bluetooth is turned OFF "); }

            DeviceAccessStatus accessStatus = DeviceAccessInformation.CreateFromId(bluetooth.DeviceId).CurrentStatus;
            if (accessStatus == DeviceAccessStatus.DeniedByUser)
            {
                Console.WriteLine("\t This app does not have access to connect to the remote device (please grant access in Settings > Privacy > Other Devices");
                return;
            }
            else
            {
                Console.WriteLine($"\t 📡 Bluetooth DeviceAccessStatus: {accessStatus}");
                //await ConnectToBluetoothDeviceAsync(bluetooth.DeviceId);
            }

            // If not, try to get the Bluetooth device from ID
            try
            {
                BluetoothDevice btdi = await BluetoothDevice.FromIdAsync(bluetooth.DeviceId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BluetoothDevice.FromIdAsync: {ex.Message}");
                Logger.Log(ex);
                return;
            }

            //await FindBluetoothDevicesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CheckBluetooth: {ex.Message}");
            Logger.Log(ex);
        }
        finally
        {
            Console.WriteLine();
        }
    }

    static async void PairDeviceAsync(DeviceInformation device)
    {
        try
        {
            DevicePairingResult PairResult = await device.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ConfirmPinMatch, DevicePairingProtectionLevel.EncryptionAndAuthentication);
            if (PairResult.Status == DevicePairingResultStatus.Paired)
            {
                Console.WriteLine($" 📡 Paired with device '{device.Name}' ");
            }
            else
            {
                Console.WriteLine($" 📡 Pairing failed with '{device.Name}' (status: {PairResult.Status})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PairDeviceAsync: {ex.Message}");
            Logger.Log(ex);
        }
    }

    static async void UnPairDeviceAsync(DeviceInformation device)
    {
        try
        {
            DeviceUnpairingResult UnPairResult = await device.Pairing.UnpairAsync();
            if (UnPairResult.Status == DeviceUnpairingResultStatus.Unpaired || UnPairResult.Status == DeviceUnpairingResultStatus.AlreadyUnpaired)
            {
                Console.WriteLine($" 📡 UnPaired with the device '{device.Name}' ");
            }
            else
            {
                Console.WriteLine($" 📡 UnPairing failed with '{device.Name}' ");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UnPairDeviceAsync: {ex.Message}");
            Logger.Log(ex);
        }
    }

    static List<BTDevice> _nearPeers = new List<BTDevice>();
    static async Task SearchForPairedDevicesAsync(RfcommServiceId? serviceId)
    {
        if (serviceId is null)
        {
            //serviceId = RfcommServiceId.ObexObjectPush;
            serviceId = RfcommServiceId.SerialPort; // or RfcommServiceId.GenericFileTransfer
        }

        try
        {
            DeviceInformationCollection devicesInfo = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort));
            if (((IReadOnlyCollection<DeviceInformation>)devicesInfo).Count.Equals(0))
            {
                Console.WriteLine(" SearchForDeviceFailure: NearbyDevicesNotFound ");
                Console.WriteLine(" Try a different RfcommServiceId? ");
                return;
            }

            foreach (DeviceInformation deviceInfo in (IEnumerable<DeviceInformation>)devicesInfo)
            {
                try
                {
                    RfcommDeviceService btDevice = await RfcommDeviceService.FromIdAsync(deviceInfo.Id);
                    Console.WriteLine($" Adding '{deviceInfo.Name}' ");
                    _nearPeers.Add(new BTDevice(btDevice.ConnectionHostName, btDevice.ConnectionServiceName, btDevice.MaxProtectionLevel, deviceInfo.Name));
                }
                catch (Exception) { }
            }

            if (_nearPeers.Count.Equals(0))
            {
                Console.WriteLine(" SearchForDeviceFailureReasons: NearbyDevicesNotFound ");
            }
            else
            {
                Console.WriteLine($" SearchForDevicesSucceeded: {_nearPeers.Count} ");
            }
        }
        catch (Exception ex)
        {
            if (ex is NullReferenceException)
            {
                Console.WriteLine($" WARNING: CapabilityNotDefined ");
            }
            else
            {
                Console.WriteLine($" WARNING: NearbyDevicesNotFound ");
            }
        }
    }

    static async Task<bool> IsCompatibleVersion(RfcommDeviceService service)
    {
        IBuffer versionAttribute = (await service.GetSdpRawAttributesAsync(BluetoothCacheMode.Uncached))[768u];
        DataReader reader = DataReader.FromBuffer(versionAttribute);
        byte attributeType = reader.ReadByte();
        if (attributeType == 10)
        {
            uint num = reader.ReadUInt32();
            return num >= 200;
        }
        return false;
    }

    /// <summary>
    /// The "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}" is the Bluetooth protocol UUID.
    /// "DeviceInformationKind.AssociationEndpoint" targets paired/unpaired Bluetooth devices.
    /// </summary>
    public static async Task FindBluetoothDevicesAsync()
    {
        /*
         [May need to add to "Package.appxmanifest"]
         <Capabilities>
             <DeviceCapability Name="bluetooth" />
             <DeviceCapability Name="internetClient" />
             <DeviceCapability Name="privateNetworkClientServer" />
         </Capabilities>
         */

        // AQS filter string for Bluetooth devices
        string aqsFilter = "System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\"";

        // Additional device selector flags
        Windows.Devices.Enumeration.DeviceWatcher watcher = Windows.Devices.Enumeration.DeviceInformation
            .CreateWatcher(aqsFilter, null, DeviceInformationKind.AssociationEndpoint);

        watcher.Added += async (s, deviceInfo) =>
        {
            Console.WriteLine($" 📡 Found Bluetooth device: {deviceInfo.Name}, ID: {deviceInfo.Id}");
        };

        watcher.Start();
        Console.WriteLine($" [watching for Bluetooth devices] ");
    }

    public static async Task ConnectToBluetoothDeviceAsync(string deviceId)
    {
        // Connect to the BluetoothLEDevice
        var bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(deviceId);
        if (bluetoothLeDevice == null)
        {
            Console.WriteLine($"Unable to connect to device '{deviceId}'.");
            return;
        }

        Console.WriteLine($"Connected to: {bluetoothLeDevice.Name}");

        // Get GATT services
        var gattServicesResult = await bluetoothLeDevice.GetGattServicesAsync();
        if (gattServicesResult.Status == GattCommunicationStatus.Success)
        {
            foreach (var service in gattServicesResult.Services)
            {
                Console.WriteLine($"Service: {service.Uuid}");

                // Get characteristics
                var characteristicsResult = await service.GetCharacteristicsAsync();
                if (characteristicsResult.Status == GattCommunicationStatus.Success)
                {
                    foreach (var characteristic in characteristicsResult.Characteristics)
                    {
                        Console.WriteLine($"Characteristic: {characteristic.Uuid}");

                        // OPTIONAL: Read a characteristic
                        var readResult = await characteristic.ReadValueAsync();
                        if (readResult.Status == GattCommunicationStatus.Success)
                        {
                            var reader = Windows.Storage.Streams.DataReader.FromBuffer(readResult.Value);
                            byte[] data = new byte[readResult.Value.Length];
                            reader.ReadBytes(data);
                            Console.WriteLine($"Data: {BitConverter.ToString(data)}");
                        }
                    }
                }
            }
        }
    }

    #endregion
}
