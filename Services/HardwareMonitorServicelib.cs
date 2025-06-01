using LibreHardwareMonitor.Hardware;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading; // Added for SemaphoreSlim
using System.Threading.Tasks;
using System.Windows.Media;

namespace SystemMonitorApp.Services
{
    public class HardwareMonitorServiceLib : IDisposable
    {
        private const double PsuCapacityWatts = 750.0;
        private const string PrimaryNetworkAdapterIdentifier = "Local Area Connection";
        private const string DefaultPingTarget = "8.8.8.8";
        private const int PingTimeoutMs = 1000;
        private const int SpeedTestTimeoutMs = 60000;

        private readonly Computer _computer;
        private readonly Ping _pingSender = new Ping();
        private readonly SemaphoreSlim _pingLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _speedTestLock = new SemaphoreSlim(1, 1);
        private bool _disposed;

        // Sensor properties
        public double CpuTemp { get; private set; }
        public double CpuUsage { get; private set; }
        public double GpuTemp { get; private set; }
        public double GpuUsage { get; private set; }
        public double MemoryUsage { get; private set; }
        public double TotalPowerDraw { get; private set; }
        public double PsuHeadroomPercentage { get; private set; }
        public double HddUsageC { get; private set; }
        public double CpuCoreVoltage { get; private set; }
        public double GpuFanSpeed { get; private set; }
        public double NetworkUploadSpeed { get; private set; }
        public double NetworkDownloadSpeed { get; private set; }
        public double NetworkUtilization { get; private set; }

        // Internet status properties
        public string InternetStatusText { get; private set; } = "Checking...";
        public SolidColorBrush InternetStatusColor { get; private set; } = Brushes.Orange;
        public int LivePing { get; private set; }
        public OoklaSpeedTestResult LastOoklaResult { get; private set; } = new OoklaSpeedTestResult();

        public HardwareMonitorServiceLib()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true,
                IsControllerEnabled = true
            };

            try
            {
                _computer.Open();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HARDWARE INIT ERROR] {ex.Message}");
            }
        }

        public void UpdateAllSensors()
        {
            TotalPowerDraw = 0;

            foreach (var hardware in _computer.Hardware)
            {
                try
                {
                    hardware.Update();
                    ProcessHardwareSensors(hardware);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SENSOR UPDATE ERROR] {hardware.Name}: {ex.Message}");
                }
            }

            CalculatePowerHeadroom();
            _ = CheckInternetStatusAndPingAsync();
        }

        private void ProcessHardwareSensors(IHardware hardware)
        {
            switch (hardware.HardwareType)
            {
                case HardwareType.Cpu:
                    ProcessCpuSensors(hardware);
                    break;
                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                    ProcessGpuSensors(hardware);
                    break;
                case HardwareType.Memory:
                    ProcessMemorySensors(hardware);
                    break;
                case HardwareType.Storage:
                    ProcessStorageSensors(hardware);
                    break;
                case HardwareType.Network:
                    ProcessNetworkSensors(hardware);
                    break;
            }
        }

        private void ProcessCpuSensors(IHardware hardware)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.Value == null) continue;

                switch (sensor.SensorType)
                {
                    case SensorType.Temperature when sensor.Name.Contains("Core (Tctl/Tdie)") ||
                                                    sensor.Name.Contains("CPU Package"):
                        CpuTemp = sensor.Value.Value;
                        break;
                    case SensorType.Load when sensor.Name == "CPU Total":
                        CpuUsage = sensor.Value.Value;
                        break;
                    case SensorType.Voltage when sensor.Name == "CPU Core":
                        CpuCoreVoltage = sensor.Value.Value;
                        break;
                    case SensorType.Power when sensor.Name.Contains("CPU Package"):
                        TotalPowerDraw += sensor.Value.Value;
                        Debug.WriteLine($"[CPU Power] {sensor.Name}: {sensor.Value.Value}W");
                        break;
                }
            }
        }

        private void ProcessGpuSensors(IHardware hardware)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.Value == null) continue;

                switch (sensor.SensorType)
                {
                    case SensorType.Temperature when sensor.Name.Contains("GPU Core"):
                        GpuTemp = sensor.Value.Value;
                        break;
                    case SensorType.Load when sensor.Name == "GPU Core":
                        GpuUsage = sensor.Value.Value;
                        break;
                    case SensorType.Fan when sensor.Name.Contains("Fan"):
                        GpuFanSpeed = sensor.Value.Value;
                        break;
                    case SensorType.Power when sensor.Name.Contains("GPU Package") ||
                                              sensor.Name.Contains("Total Board Power"):
                        TotalPowerDraw += sensor.Value.Value;
                        Debug.WriteLine($"[GPU Power] {sensor.Name}: {sensor.Value.Value}W");
                        break;
                }
            }
        }

        private void ProcessMemorySensors(IHardware hardware)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Load && sensor.Name == "Memory" && sensor.Value.HasValue)
                {
                    MemoryUsage = sensor.Value.Value;
                    break;
                }
            }
        }

        private void ProcessStorageSensors(IHardware hardware)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Load &&
                    sensor.Name.Contains("Used Space") &&
                    sensor.Identifier.ToString().Contains("/nvme/0/") &&
                    sensor.Value.HasValue)
                {
                    HddUsageC = sensor.Value.Value;
                    break;
                }
            }
        }

        private void ProcessNetworkSensors(IHardware hardware)
        {
            if (!hardware.Name.Contains(PrimaryNetworkAdapterIdentifier))
                return;

            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.Value == null) continue;

                if (sensor.SensorType == SensorType.Throughput)
                {
                    if (sensor.Name.Contains("Upload Speed"))
                        NetworkUploadSpeed = sensor.Value.Value;
                    else if (sensor.Name.Contains("Download Speed"))
                        NetworkDownloadSpeed = sensor.Value.Value;
                }
                else if (sensor.SensorType == SensorType.Load && sensor.Name == "Network Utilization")
                {
                    NetworkUtilization = sensor.Value.Value;
                }
            }
        }

        private void CalculatePowerHeadroom()
        {
            PsuHeadroomPercentage = TotalPowerDraw > 0
                ? Math.Max(0, (1 - (TotalPowerDraw / PsuCapacityWatts)) * 100)
                : 0;
        }

        public async Task CheckInternetStatusAndPingAsync()
        {
            if (!await _pingLock.WaitAsync(0)) return;

            try
            {
                var reply = await _pingSender.SendPingAsync(DefaultPingTarget, PingTimeoutMs);
                UpdateInternetStatus(reply);
            }
            catch (PingException ex)
            {
                SetOfflineStatus();
                Debug.WriteLine($"[PING ERROR] {ex.Message}");
            }
            finally
            {
                _pingLock.Release();
            }
        }

        private void UpdateInternetStatus(PingReply reply)
        {
            if (reply.Status == IPStatus.Success)
            {
                LivePing = (int)reply.RoundtripTime;
                InternetStatusText = "Online";
                InternetStatusColor = Brushes.LightGreen;
            }
            else
            {
                SetOfflineStatus();
            }
        }

        private void SetOfflineStatus()
        {
            LivePing = 0;
            InternetStatusText = "Offline";
            InternetStatusColor = Brushes.Red;
        }

        public async Task<OoklaSpeedTestResult> MeasureOoklaInternetSpeedAsync()
        {
            if (!await _speedTestLock.WaitAsync(0))
                return LastOoklaResult;

            try
            {
                var speedtestPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "speedtest.exe"
                );

                if (!File.Exists(speedtestPath))
                {
                    Debug.WriteLine($"[SPEEDTEST] Executable not found at {speedtestPath}");
                    return new OoklaSpeedTestResult();
                }

                return await RunSpeedTestProcess(speedtestPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SPEEDTEST ERROR] {ex.Message}");
                return new OoklaSpeedTestResult();
            }
            finally
            {
                _speedTestLock.Release();
            }
        }

        private async Task<OoklaSpeedTestResult> RunSpeedTestProcess(string speedtestPath)
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = speedtestPath,
                Arguments = "--format=json --accept-license --accept-gdpr",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(TimeSpan.FromMilliseconds(SpeedTestTimeoutMs));

            return process.ExitCode == 0
                ? ParseSpeedTestResult(output)
                : HandleSpeedTestError(process, output);
        }

        private OoklaSpeedTestResult ParseSpeedTestResult(string jsonOutput)
        {
            if (string.IsNullOrWhiteSpace(jsonOutput))
            {
                Debug.WriteLine("[SPEEDTEST] Empty output received");
                return new OoklaSpeedTestResult();
            }

            try
            {
                using var jsonDoc = JsonDocument.Parse(jsonOutput);
                var root = jsonDoc.RootElement;

                var result = new OoklaSpeedTestResult
                {
                    DownloadSpeed = GetBandwidthValue(root, "download") / 1_000_000,
                    UploadSpeed = GetBandwidthValue(root, "upload") / 1_000_000,
                    Ping = GetPingValue(root, "ping"),
                    Jitter = GetJitterValue(root, "ping")
                };

                LastOoklaResult = result;
                Debug.WriteLine($"[SPEEDTEST] Result: {result.DownloadSpeed:F2}↓/{result.UploadSpeed:F2}↑ Mbps");
                return result;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[SPEEDTEST JSON ERROR] {ex.Message}\nOutput: {jsonOutput}");
                return new OoklaSpeedTestResult();
            }
        }

        private double GetBandwidthValue(JsonElement root, string property)
        {
            return root.TryGetProperty(property, out var element) &&
                   element.TryGetProperty("bandwidth", out var bandwidth)
                ? bandwidth.GetDouble()
                : 0;
        }

        private double GetPingValue(JsonElement root, string property)
        {
            return root.TryGetProperty(property, out var element) &&
                   element.TryGetProperty("latency", out var latency)
                ? latency.GetDouble()
                : 0;
        }

        private double GetJitterValue(JsonElement root, string property)
        {
            return root.TryGetProperty(property, out var element) &&
                   element.TryGetProperty("jitter", out var jitter)
                ? jitter.GetDouble()
                : 0;
        }

        private OoklaSpeedTestResult HandleSpeedTestError(Process process, string output)
        {
            var error = process.StandardError.ReadToEnd();
            Debug.WriteLine($"[SPEEDTEST ERROR] Exit code: {process.ExitCode}\nError: {error}\nOutput: {output}");
            return new OoklaSpeedTestResult();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _computer.Close();
                _pingSender.Dispose();
                _pingLock.Dispose();
                _speedTestLock.Dispose();
            }

            _disposed = true;
        }

        public class OoklaSpeedTestResult
        {
            public double DownloadSpeed { get; set; }
            public double UploadSpeed { get; set; }
            public double Ping { get; set; }
            public double Jitter { get; set; }
        }
    }
}