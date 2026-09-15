using System.Threading;

namespace AuroraClock
{
    /// <summary>Ensures only one copy of the widget suite runs at a time.</summary>
    internal static class SingleInstance
    {
        private const string MutexName = "AuroraWorldClock.SingleInstance.7C1F";
        private static Mutex? _mutex;

        public static bool TryAcquire()
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            return createdNew;
        }

        public static void Release()
        {
            try { _mutex?.ReleaseMutex(); } catch { /* ignored */ }
            _mutex?.Dispose();
            _mutex = null;
        }
    }
}
