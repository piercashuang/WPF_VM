using MvCameraControl;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VmCamera
{
    public sealed class HikCameraService : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly DeviceTLayerType enumTLayerType =
            DeviceTLayerType.MvGigEDevice |
            DeviceTLayerType.MvUsbDevice |
            DeviceTLayerType.MvGenTLGigEDevice |
            DeviceTLayerType.MvGenTLCXPDevice |
            DeviceTLayerType.MvGenTLCameraLinkDevice |
            DeviceTLayerType.MvGenTLXoFDevice;

        private List<IDeviceInfo> lastScanDevices = new List<IDeviceInfo>();
        private IDevice device;
        private CancellationTokenSource grabTokenSource;
        private Task grabTask;
        private bool isGrabbing;

        public HikCameraService()
        {
            VmCameraSdk.EnsureInitialized();
        }

        public event EventHandler<CameraFrameReceivedEventArgs> FrameReceived;

        public bool IsOpened
        {
            get { return device != null && device.IsConnected; }
        }

        public bool IsGrabbing
        {
            get { return isGrabbing; }
        }

        public IReadOnlyList<CameraDeviceInfo> ScanDevices()
        {
            List<IDeviceInfo> devices;
            var result = DeviceEnumerator.EnumDevices(enumTLayerType, out devices);

            if (result != MvError.MV_OK)
            {
                throw new InvalidOperationException(string.Format("枚举相机失败，错误码：0x{0:X8}", result));
            }

            lastScanDevices = devices ?? new List<IDeviceInfo>();

            return lastScanDevices.Select((info, index) =>
            {
                var accessible = DeviceEnumerator.IsDeviceAccessible(info, DeviceAccessMode.AccessControl);
                var displayName = !string.IsNullOrWhiteSpace(info.UserDefinedName)
                    ? string.Format("{0} ({1})", info.UserDefinedName, info.SerialNumber)
                    : string.Format("{0} {1} ({2})", info.ManufacturerName, info.ModelName, info.SerialNumber);

                return new CameraDeviceInfo
                {
                    UniqueKey = BuildUniqueKey(info, index),
                    DisplayName = displayName,
                    SerialNumber = info.SerialNumber,
                    UserDefinedName = info.UserDefinedName,
                    ManufacturerName = info.ManufacturerName,
                    ModelName = info.ModelName,
                    TransportLayer = info.TLayerType.ToString(),
                    IsAccessible = accessible
                };
            }).ToList();
        }

        public void Open(CameraDeviceInfo camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            lock (syncRoot)
            {
                Close();

                var deviceInfo = FindDeviceInfo(camera.UniqueKey);
                device = DeviceFactory.CreateDevice(deviceInfo);

                var result = device.Open();
                if (result != MvError.MV_OK)
                {
                    device.Dispose();
                    device = null;
                    throw new InvalidOperationException(string.Format("打开相机失败，错误码：0x{0:X8}", result));
                }

                ConfigurePacketSizeIfNeeded();
                device.Parameters.SetEnumValueByString("AcquisitionMode", "Continuous");
                device.Parameters.SetEnumValueByString("TriggerMode", "Off");
                device.StreamGrabber.SetImageNodeNum(4);
            }
        }

        public void Close()
        {
            StopGrabbing();

            lock (syncRoot)
            {
                if (device != null)
                {
                    try
                    {
                        device.Close();
                    }
                    finally
                    {
                        device.Dispose();
                        device = null;
                    }
                }
            }
        }

        public void StartGrabbing(bool useSoftwareTrigger)
        {
            lock (syncRoot)
            {
                EnsureDeviceOpened();

                if (isGrabbing)
                {
                    return;
                }

                device.Parameters.SetEnumValueByString("TriggerMode", useSoftwareTrigger ? "On" : "Off");

                if (useSoftwareTrigger)
                {
                    device.Parameters.SetEnumValueByString("TriggerSource", "Software");
                }

                var result = device.StreamGrabber.StartGrabbing();
                if (result != MvError.MV_OK)
                {
                    throw new InvalidOperationException(string.Format("开始取流失败，错误码：0x{0:X8}", result));
                }

                grabTokenSource = new CancellationTokenSource();
                isGrabbing = true;
                grabTask = Task.Run(() => GrabLoop(grabTokenSource.Token), grabTokenSource.Token);
            }
        }

        public void StopGrabbing()
        {
            CancellationTokenSource tokenSource = null;
            Task runningTask = null;

            lock (syncRoot)
            {
                if (!isGrabbing)
                {
                    return;
                }

                tokenSource = grabTokenSource;
                runningTask = grabTask;
                grabTokenSource = null;
                grabTask = null;
                isGrabbing = false;
            }

            if (tokenSource != null)
            {
                tokenSource.Cancel();
            }

            if (runningTask != null)
            {
                try
                {
                    runningTask.Wait(1000);
                }
                catch (AggregateException)
                {
                }
            }

            lock (syncRoot)
            {
                if (device != null)
                {
                    device.StreamGrabber.StopGrabbing();
                }
            }

            if (tokenSource != null)
            {
                tokenSource.Dispose();
            }
        }

        public void TriggerSoftware()
        {
            lock (syncRoot)
            {
                EnsureDeviceOpened();

                var result = device.Parameters.SetCommandValue("TriggerSoftware");
                if (result != MvError.MV_OK)
                {
                    throw new InvalidOperationException(string.Format("软触发失败，错误码：0x{0:X8}", result));
                }
            }
        }

        public void Dispose()
        {
            Close();
        }

        private void GrabLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                IFrameOut frameOut = null;

                try
                {
                    int result;

                    lock (syncRoot)
                    {
                        if (device == null)
                        {
                            return;
                        }

                        result = device.StreamGrabber.GetImageBuffer(500, out frameOut);
                    }

                    if (result == MvError.MV_OK && frameOut != null)
                    {
                        using (var bitmap = frameOut.Image.ToBitmap())
                        {
                            var clone = new Bitmap(bitmap);
                            RaiseFrameReceived(clone, frameOut.FrameNum, bitmap.Width, bitmap.Height);
                        }
                    }
                    else
                    {
                        Thread.Sleep(5);
                    }
                }
                finally
                {
                    if (frameOut != null)
                    {
                        lock (syncRoot)
                        {
                            if (device != null)
                            {
                                device.StreamGrabber.FreeImageBuffer(frameOut);
                            }
                        }
                    }
                }
            }
        }

        private void RaiseFrameReceived(Bitmap bitmap, long frameNumber, int width, int height)
        {
            var handler = FrameReceived;
            if (handler != null)
            {
                handler(this, new CameraFrameReceivedEventArgs(bitmap, frameNumber, width, height));
            }
            else
            {
                bitmap.Dispose();
            }
        }

        private void EnsureDeviceOpened()
        {
            if (device == null || !device.IsConnected)
            {
                throw new InvalidOperationException("当前没有已打开的相机。");
            }
        }

        private IDeviceInfo FindDeviceInfo(string uniqueKey)
        {
            for (int i = 0; i < lastScanDevices.Count; i++)
            {
                if (BuildUniqueKey(lastScanDevices[i], i) == uniqueKey)
                {
                    return lastScanDevices[i];
                }
            }

            throw new InvalidOperationException("所选相机不在当前扫描结果中，请重新扫描。");
        }

        private void ConfigurePacketSizeIfNeeded()
        {
            var gigEDevice = device as IGigEDevice;
            if (gigEDevice == null)
            {
                return;
            }

            int packetSize;
            var result = gigEDevice.GetOptimalPacketSize(out packetSize);
            if (result == MvError.MV_OK && packetSize > 0)
            {
                device.Parameters.SetIntValue("GevSCPSPacketSize", packetSize);
            }
        }

        private static string BuildUniqueKey(IDeviceInfo info, int index)
        {
            if (!string.IsNullOrWhiteSpace(info.SerialNumber))
            {
                return info.SerialNumber;
            }

            return string.Format("{0}-{1}-{2}-{3}", info.TLayerType, info.ManufacturerName, info.ModelName, index);
        }
    }
}
