using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;
using SystemMonitorApp.Services;

namespace SystemMonitorApp.ViewModels
{
    public class SystemMonitorViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly HardwareMonitorServiceLib _monitorService;
        private readonly DispatcherTimer _updateTimer;

        public SystemMonitorViewModel()
        {
            _monitorService = new HardwareMonitorServiceLib();

            // Setup update timer
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += UpdateSensors;
            _updateTimer.Start();
        }

        private void UpdateSensors(object sender, EventArgs e)
        {
            _monitorService.UpdateAllSensors();
            OnPropertyChanged(nameof(CpuUsage));
            OnPropertyChanged(nameof(CpuTemp));
            OnPropertyChanged(nameof(GpuUsage));
            OnPropertyChanged(nameof(GpuTemp));
            OnPropertyChanged(nameof(MemoryUsage));
            OnPropertyChanged(nameof(TotalPowerDraw));
            OnPropertyChanged(nameof(PsuHeadroomPercentage));
            OnPropertyChanged(nameof(HddUsageC));
            OnPropertyChanged(nameof(CpuCoreVoltage));
            OnPropertyChanged(nameof(GpuFanSpeed));
            OnPropertyChanged(nameof(NetworkUploadSpeed));
            OnPropertyChanged(nameof(NetworkDownloadSpeed));
            OnPropertyChanged(nameof(NetworkUtilization));
            OnPropertyChanged(nameof(InternetStatusText));
            OnPropertyChanged(nameof(InternetStatusColor));
            OnPropertyChanged(nameof(LivePing));
        }

        // Sensor properties
        public double CpuUsage => _monitorService.CpuUsage;
        public double CpuTemp => _monitorService.CpuTemp;
        public double GpuUsage => _monitorService.GpuUsage;
        public double GpuTemp => _monitorService.GpuTemp;
        public double MemoryUsage => _monitorService.MemoryUsage;
        public double TotalPowerDraw => _monitorService.TotalPowerDraw;
        public double PsuHeadroomPercentage => _monitorService.PsuHeadroomPercentage;
        public double HddUsageC => _monitorService.HddUsageC;
        public double CpuCoreVoltage => _monitorService.CpuCoreVoltage;
        public double GpuFanSpeed => _monitorService.GpuFanSpeed;
        public double NetworkUploadSpeed => _monitorService.NetworkUploadSpeed;
        public double NetworkDownloadSpeed => _monitorService.NetworkDownloadSpeed;
        public double NetworkUtilization => _monitorService.NetworkUtilization;

        // Internet status properties
        public string InternetStatusText => _monitorService.InternetStatusText;
        public SolidColorBrush InternetStatusColor => _monitorService.InternetStatusColor;
        public int LivePing => _monitorService.LivePing;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            _updateTimer.Stop();
            _updateTimer.Tick -= UpdateSensors;
            _monitorService.Dispose();
        }
    }
}