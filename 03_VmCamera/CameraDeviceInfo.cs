namespace VmCamera
{
    public sealed class CameraDeviceInfo
    {
        public string UniqueKey { get; set; }

        public string DisplayName { get; set; }

        public string SerialNumber { get; set; }

        public string UserDefinedName { get; set; }

        public string ManufacturerName { get; set; }

        public string ModelName { get; set; }

        public string TransportLayer { get; set; }

        public bool IsAccessible { get; set; }
    }

    public sealed class CameraFrameReceivedEventArgs : System.EventArgs
    {
        public CameraFrameReceivedEventArgs(System.Drawing.Bitmap bitmap, long frameNumber, int width, int height)
        {
            Bitmap = bitmap;
            FrameNumber = frameNumber;
            Width = width;
            Height = height;
        }

        public System.Drawing.Bitmap Bitmap { get; private set; }

        public long FrameNumber { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }
    }
}
