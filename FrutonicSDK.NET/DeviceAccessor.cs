
using System;

namespace FutronicSDK.NET
{
    public class DeviceAccessor
    {
        public FingerprintDevice AccessFingerprintDevice()
        {
            var handle = LibScanApi.ftrScanOpenDevice();

            if (handle != IntPtr.Zero)
            {
                return new FingerprintDevice(handle);
            }
            return default;             
        }
    }
}