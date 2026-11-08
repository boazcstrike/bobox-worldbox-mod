using System.Threading;

namespace BoBox.Bridge
{
    /// <summary>
    /// A request handed from a listener thread to the Unity main thread.
    /// Exactly one side claims it: the main thread to run it, or the waiter to abandon it after a timeout.
    /// An abandoned request is never run, so the client never sees an error for work that still happened.
    /// </summary>
    internal sealed class PendingRequest
    {
        private const int Waiting = 0;
        private const int Running = 1;
        private const int Abandoned = 2;
        private const int ClaimedWaitFactor = 3;

        private readonly ManualResetEventSlim _done = new ManualResetEventSlim(false);
        private int _state = Waiting;

        public ApiRequest Request { get; }
        public ApiResponse Response { get; private set; }

        public PendingRequest(ApiRequest request)
        {
            Request = request;
        }

        public bool TryClaimForRun()
        {
            return Interlocked.CompareExchange(ref _state, Running, Waiting) == Waiting;
        }

        public void Complete(ApiResponse response)
        {
            Response = response;
            _done.Set();
        }

        /// <summary>True once the main thread claimed the request to run it: after a timeout its outcome is unknown.</summary>
        public bool IsClaimedForRun => Volatile.Read(ref _state) == Running;

        /// <summary>Waits for the main thread. Returns false on timeout: the request is abandoned, or claimed by a handler that never finished.</summary>
        public bool WaitForResponse(int timeoutMs)
        {
            if (_done.Wait(timeoutMs))
            {
                return true;
            }

            if (Interlocked.CompareExchange(ref _state, Abandoned, Waiting) == Waiting)
            {
                return false;
            }

            // Main thread claimed it just before the timeout, so the work may still finish. Wait a bounded
            // time more; a stuck handler gives up here, and its late Complete only sets an unread field.
            return _done.Wait(timeoutMs * ClaimedWaitFactor);
        }
    }
}
