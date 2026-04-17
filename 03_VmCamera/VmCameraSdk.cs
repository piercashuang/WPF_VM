using System;
using MvCameraControl;

namespace VmCamera
{
    public static class VmCameraSdk
    {
        private static readonly object SyncRoot = new object();
        private static bool initialized;

        public static void EnsureInitialized()
        {
            lock (SyncRoot)
            {
                if (initialized)
                {
                    return;
                }

                SDKSystem.Initialize();
                initialized = true;
            }
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                if (!initialized)
                {
                    return;
                }

                SDKSystem.Finalize();
                initialized = false;
            }
        }
    }
}
