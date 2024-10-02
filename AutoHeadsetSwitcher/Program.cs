using AudioSwitcher.AudioApi.CoreAudio;
using Microsoft.Win32;
using RGB.NET.Core;
using RGB.NET.Devices.Corsair;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoHeadsetSwitcher
{
    public class Program : Form
    {
        private NotifyIcon trayIcon;
        private CoreAudioController controller;
        private CoreAudioDevice speakersDevice;
        private CoreAudioDevice headsetDevice;
        private System.Windows.Forms.Timer manualCheckTimer;
        private bool lastHeadsetStatus = false;
        private bool isFirstRun = true;
        private bool isCorsairInitialized = false;
        private int initializationAttempts = 0;

        [STAThread]
        static void Main()
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            InitializeLogger();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Program());
        }

        private static void InitializeLogger()
        {
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoHeadsetSwitcher", "logs", "log-.txt");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("Application starting up");
        }

        public Program()
        {
            InitializeComponent();
            InitializeTimer();

#if !DEBUG
            AddToStartup();
#endif
        }

        private void InitializeTimer()
        {
            manualCheckTimer = new System.Windows.Forms.Timer()
            {
                Interval = 10000 // Start with a 10-second delay
            };
            manualCheckTimer.Tick += ManualCheckTimer_Tick;
            manualCheckTimer.Start();
            Log.Debug("Timer initialized with 10-second delay");
        }

        private void AddToStartup()
        {
            const string applicationName = "AutoHeadsetSwitcher";

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (key != null)
                    {
                        string executablePath = Process.GetCurrentProcess().MainModule.FileName;
                        object currentValue = key.GetValue(applicationName);

                        if (currentValue == null || currentValue.ToString() != executablePath)
                        {
                            key.SetValue(applicationName, executablePath);
                            Log.Information("Added application to startup");
                        }
                    }
                    else
                    {
                        throw new Exception("Unable to access the Registry key for startup programs.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to add to startup");
                ShowNotification("Startup Error", $"Failed to add to startup: {ex.Message}");
            }
        }

        private void InitializeComponent()
        {
            trayIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true
            };

            trayIcon.ContextMenuStrip.Items.Add("Exit", null, Exit);

            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            FormBorderStyle = FormBorderStyle.None;
            Opacity = 0;
            Size = new System.Drawing.Size(0, 0);
            Log.Debug("UI components initialized");
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        private async void ManualCheckTimer_Tick(object sender, EventArgs e)
        {
            if (controller == null || headsetDevice == null || speakersDevice == null)
            {
                await InitializeAudioDevicesAsync();
                initializationAttempts++;
                Log.Information("Audio devices initialization attempt #{InitializationAttempt}", initializationAttempts);
                return;
            }

            if (!isCorsairInitialized)
            {
                InitializeDeviceProvider();
                return;
            }

            await CheckHeadsetStatusAsync();

            // Reduce the interval after successful initialization
            if (manualCheckTimer.Interval != 2000)
            {
                manualCheckTimer.Interval = 2000;
                Log.Information("Reduced check interval to 2 seconds");
            }
        }

        private async Task InitializeAudioDevicesAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    controller = new CoreAudioController();
                    var devices = controller.GetDevices();
                    speakersDevice = devices.FirstOrDefault(d => d.InterfaceName == "EDIFIER R1280DBs");
                    headsetDevice = devices.FirstOrDefault(d => d.InterfaceName == "CORSAIR HS80 RGB Wireless Gaming Headset");

                    if (speakersDevice == null || headsetDevice == null)
                    {
                        throw new Exception("Failed to initialize audio devices.");
                    }
                });
                Log.Information("Audio devices initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing audio devices");
            }
        }

        private void InitializeDeviceProvider()
        {
            try
            {
                isCorsairInitialized = CorsairDeviceProvider.Instance.Initialize(throwExceptions: true);
                if (isCorsairInitialized)
                {
                    Log.Information("Corsair Device Provider initialized successfully");
                }
                else
                {
                    Log.Warning("Corsair Device Provider initialization returned false");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Corsair Device Provider");
                isCorsairInitialized = false;
            }
        }

        private async Task CheckHeadsetStatusAsync()
        {
            try
            {
                bool currentHeadsetStatus = await IsHeadsetConnectedAsync();

                if (isFirstRun || currentHeadsetStatus != lastHeadsetStatus)
                {
                    lastHeadsetStatus = currentHeadsetStatus;
                    if (currentHeadsetStatus)
                    {
                        await HeadsetConnectedAsync(!isFirstRun);
                    }
                    else
                    {
                        await HeadsetDisconnectedAsync(!isFirstRun);
                    }
                    isFirstRun = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in CheckHeadsetStatusAsync");
                ShowNotification("Error", "An error occurred while checking headset status.");
            }
        }

        private async Task<bool> IsHeadsetConnectedAsync()
        {
            try
            {
                if (!isCorsairInitialized)
                {
                    InitializeDeviceProvider();
                    if (!isCorsairInitialized)
                    {
                        Log.Warning("Skipping headset check due to uninitialized Corsair Device Provider");
                        return false;
                    }
                }

                return await Task.Run(() =>
                {
                    var devices = CorsairDeviceProvider.Instance.LoadCorsairDevices();
                    return devices.Any(d => d.DeviceInfo.DeviceType == RGBDeviceType.Headset);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking headset connection");
                isCorsairInitialized = false;  // Reset the flag to force re-initialization on next attempt
                return false;
            }
        }

        private async Task HeadsetConnectedAsync(bool showNotification)
        {
            Log.Information("Headset connected. Switching to headset audio.");
            await SetDefaultAudioDeviceAsync(headsetDevice);
            if (showNotification)
            {
                ShowNotification("Audio Switched", "Switched to CORSAIR HS80 RGB Wireless Gaming Headset");
            }
        }

        private async Task HeadsetDisconnectedAsync(bool showNotification)
        {
            Log.Information("Headset disconnected. Switching to speakers.");
            await SetDefaultAudioDeviceAsync(speakersDevice);
            if (showNotification)
            {
                ShowNotification("Audio Switched", "Switched to EDIFIER R1280DBs");
            }
        }

        private void ShowNotification(string title, string message)
        {
            trayIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
            Log.Information("Notification shown: {Title} - {Message}", title, message);
        }

        private async Task SetDefaultAudioDeviceAsync(CoreAudioDevice device)
        {
            if (device == null)
            {
                Log.Warning("Attempted to set null device as default");
                return;
            }

            try
            {
                await Task.Run(() => controller.SetDefaultDevice(device));
                Log.Information("Default audio device set to {DeviceName}", device.FullName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error setting default audio device");
            }
        }

        private void Exit(object sender, EventArgs e)
        {
            Log.Information("Application shutting down");
            manualCheckTimer.Stop();
            trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                manualCheckTimer?.Dispose();
                trayIcon?.Dispose();
                controller?.Dispose();
                Log.CloseAndFlush();
            }
            base.Dispose(disposing);
        }
    }
}