using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VmCamera;
using VmFunBase;

namespace VisionMaster
{
    public sealed class CameraManagerViewModel : ViewModelBase, IDisposable
    {
        private readonly HikCameraService cameraService;
        private CameraDeviceInfo selectedDevice;
        private BitmapSource previewImage;
        private string statusMessage;
        private bool useSoftwareTrigger;
        private bool isOpened;
        private bool isGrabbing;

        public CameraManagerViewModel()
        {
            cameraService = new HikCameraService();
            cameraService.FrameReceived += CameraServiceOnFrameReceived;

            Devices = new ObservableCollection<CameraDeviceInfo>();
            UseSoftwareTrigger = true;
            StatusMessage = "等待扫描相机。";

            ScanCommand = new RelayCommand(ScanDevices);
            OpenCommand = new RelayCommand(OpenCamera, () => SelectedDevice != null && !IsOpened);
            CloseCommand = new RelayCommand(CloseCamera, () => IsOpened);
            StartGrabCommand = new RelayCommand(StartGrabbing, () => IsOpened && !IsGrabbing);
            StopGrabCommand = new RelayCommand(StopGrabbing, () => IsGrabbing);
            TriggerCommand = new RelayCommand(TriggerSoftware, () => IsOpened && IsGrabbing && UseSoftwareTrigger);
        }

        public ObservableCollection<CameraDeviceInfo> Devices { get; }

        public ICommand ScanCommand { get; }

        public ICommand OpenCommand { get; }

        public ICommand CloseCommand { get; }

        public ICommand StartGrabCommand { get; }

        public ICommand StopGrabCommand { get; }

        public ICommand TriggerCommand { get; }

        public CameraDeviceInfo SelectedDevice
        {
            get { return selectedDevice; }
            set
            {
                if (SetProperty(ref selectedDevice, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public BitmapSource PreviewImage
        {
            get { return previewImage; }
            private set { SetProperty(ref previewImage, value); }
        }

        public string StatusMessage
        {
            get { return statusMessage; }
            private set { SetProperty(ref statusMessage, value); }
        }

        public bool UseSoftwareTrigger
        {
            get { return useSoftwareTrigger; }
            set
            {
                if (SetProperty(ref useSoftwareTrigger, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsOpened
        {
            get { return isOpened; }
            private set
            {
                if (SetProperty(ref isOpened, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsGrabbing
        {
            get { return isGrabbing; }
            private set
            {
                if (SetProperty(ref isGrabbing, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public void Dispose()
        {
            cameraService.FrameReceived -= CameraServiceOnFrameReceived;
            cameraService.Dispose();
        }

        private void ScanDevices()
        {
            try
            {
                Devices.Clear();

                foreach (var device in cameraService.ScanDevices())
                {
                    Devices.Add(device);
                }

                SelectedDevice = Devices.Count > 0 ? Devices[0] : null;
                StatusMessage = string.Format("扫描完成，共发现 {0} 台相机。", Devices.Count);
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }

        private void OpenCamera()
        {
            try
            {
                cameraService.Open(SelectedDevice);
                IsOpened = true;
                StatusMessage = string.Format("已连接相机：{0}", SelectedDevice != null ? SelectedDevice.DisplayName : "未知设备");
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }

        private void CloseCamera()
        {
            try
            {
                cameraService.Close();
                IsOpened = false;
                IsGrabbing = false;
                StatusMessage = "相机已关闭。";
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }

        private void StartGrabbing()
        {
            try
            {
                cameraService.StartGrabbing(UseSoftwareTrigger);
                IsGrabbing = true;
                StatusMessage = UseSoftwareTrigger ? "相机已开始采集，等待软触发。" : "相机已开始连续采集。";
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }

        private void StopGrabbing()
        {
            try
            {
                cameraService.StopGrabbing();
                IsGrabbing = false;
                StatusMessage = "采集已停止。";
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }

        private void TriggerSoftware()
        {
            try
            {
                cameraService.TriggerSoftware();
                StatusMessage = "已发送软触发命令。";
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }

        private void CameraServiceOnFrameReceived(object sender, CameraFrameReceivedEventArgs e)
        {
            var bitmap = e.Bitmap;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                using (bitmap)
                {
                    var image = BitmapSourceHelper.CreateBitmapSource(bitmap);
                    if (image != null && image.CanFreeze)
                    {
                        image.Freeze();
                    }

                    PreviewImage = image;
                    StatusMessage = string.Format("收到图像：{0} x {1}，帧号 {2}", e.Width, e.Height, e.FrameNumber);
                }
            }));
        }
    }
}
