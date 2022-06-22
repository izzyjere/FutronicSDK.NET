
using System;
using System.Drawing;
using System.Threading;

namespace FutronicSDK.NET
{
    public class FingerprintDevice : IDisposable
    {
        private const int FingerPresenseCheckIntervalInMs = 50;
        private const int FingerDetectionContrastThreshold = 800;
        private const int NDose = 4;

        private readonly IntPtr handle;
        private readonly Timer fingerDetectionTimer;

        private readonly object ledStatusWritingLock = new object();

        public event EventHandler FingerDetected;
        public event EventHandler FingerReleased;

        public FingerprintDevice(IntPtr handle)
        {
            this.handle = handle;
            fingerDetectionTimer = new Timer(FingerDetectionCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public bool GreenLed
        {
            get => GetLedState().GreenIsOn;
            set => SetGreenLed(value);
        }

        public bool RedLed
        {
            get => GetLedState().RedIsOn;
            set => SetRedLed(value);
        }

        public bool IsFingerPresent { get; private set; }

        public void StartFingerDetection()
        {
            fingerDetectionTimer.Change(0, FingerPresenseCheckIntervalInMs);
        }
        public void StopFingerDetection()
        {
            fingerDetectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void FingerDetectionCallback(object state)
        {
            var result = LibScanApi.ftrScanIsFingerPresent(handle, out var pFrameParameters);

            if (!result)
            {
                // There could be an error
                // var error = LibScanApi.GetLastError();
                return;
            }

            var lastFingerDetectedResult = pFrameParameters.nContrastOnDose2 > FingerDetectionContrastThreshold;

            if (lastFingerDetectedResult && !IsFingerPresent)
            {
                IsFingerPresent = true;
                OnFingerDetected();
            }

            if (!lastFingerDetectedResult && IsFingerPresent)
            {
                IsFingerPresent = false;
                OnFingerReleased();
            }
        }

        public void Dispose()
        {
            fingerDetectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
            fingerDetectionTimer.Dispose();

            LibScanApi.ftrScanCloseDevice(handle);
        }

        protected virtual void OnFingerDetected()
        {
            FingerDetected?.Invoke(this, EventArgs.Empty);
        }

        public LedState GetLedState()
        {
            LibScanApi.ftrScanGetDiodesStatus(handle, out bool greenIsOn, out bool redIsOn);

            return new LedState()
            {
                GreenIsOn = greenIsOn,
                RedIsOn = redIsOn,
            };
        }

        public void SwitchLedState(bool green, bool red)
        {
            LibScanApi.ftrScanSetDiodesStatus(handle, (byte)(green ? 255 : 0), (byte)(red ? 255 : 0));
        }

        public Bitmap ReadFingerprint()
        {
            var t = new LibScanApi.FTRSCAN_IMAGE_SIZE();
            LibScanApi.ftrScanGetImageSize(handle, out t);

            byte[] arr = new byte[t.nImageSize];
            LibScanApi.ftrScanGetImage(handle, NDose, arr);

            var width = t.nWidth;
            var height = t.nHeight;

            var image = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte a = (byte)(0xFF - arr[y * width + x]);

                    image.SetPixel(x, y, Color.FromArgb(a, a, a));
                }
            }

            return image;
        }

        protected void SetGreenLed(bool state)
        {
            lock (ledStatusWritingLock)
            {
                LibScanApi.ftrScanGetDiodesStatus(handle, out bool greenIsOn, out bool redIsOn);

                greenIsOn = state;

                LibScanApi.ftrScanSetDiodesStatus(handle, (byte)(greenIsOn ? 255 : 0), (byte)(redIsOn ? 255 : 0));
            }
        }

        protected void SetRedLed(bool state)
        {
            lock (ledStatusWritingLock)
            {
                LibScanApi.ftrScanGetDiodesStatus(handle, out bool greenIsOn, out bool redIsOn);

                redIsOn = state;

                LibScanApi.ftrScanSetDiodesStatus(handle, (byte)(greenIsOn ? 255 : 0), (byte)(redIsOn ? 255 : 0));
            }
        }

        protected virtual void OnFingerReleased()
        {
            FingerReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    public class LedState
    {
        public bool GreenIsOn { get; set; }

        public bool RedIsOn { get; set; }
    }
}
