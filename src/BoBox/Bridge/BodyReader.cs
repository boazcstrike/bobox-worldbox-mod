using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace BoBox.Bridge
{
    /// <summary>The client sent less of the body than its Content-Length promised, within the deadline.</summary>
    internal sealed class BodyTimeoutException : Exception
    {
        public BodyTimeoutException(int timeoutMs) : base("request body not received within " + timeoutMs + " ms")
        {
        }
    }

    /// <summary>The client closed or reset the connection while its body was being read.</summary>
    internal sealed class ClientAbortedException : Exception
    {
        public ClientAbortedException(Exception inner) : base("client closed the connection while sending its body", inner)
        {
        }
    }

    /// <summary>Reads a POST body on a listener thread, with a size cap and a deadline for the whole body.</summary>
    internal static class BodyReader
    {
        public const int MaxBodyBytes = 64 * 1024;
        public const int TimeoutMs = 5000;

        private const int ReadActive = 0;
        private const int ReadDone = 1;
        private const int ReadTimedOut = 2;

        /// <summary>
        /// Throws a 411 or 413 <see cref="BridgeException"/> for a missing or oversized Content-Length,
        /// <see cref="BodyTimeoutException"/> when the body is not complete after <see cref="TimeoutMs"/>, and
        /// <see cref="ClientAbortedException"/> when the client resets the connection mid-body.
        /// A body shorter than Content-Length (client closed early) is returned as received.
        /// </summary>
        public static string Read(HttpListenerRequest http, HttpListenerResponse response)
        {
            if (string.IsNullOrEmpty(http.Headers["Content-Length"]) || http.ContentLength64 < 0)
            {
                throw new BridgeException(411, "Content-Length required for POST");
            }

            if (http.ContentLength64 > MaxBodyBytes)
            {
                throw new BridgeException(413, "body too large (max " + MaxBodyBytes + " bytes)");
            }

            byte[] buffer = new byte[http.ContentLength64];
            int total = ReadWithDeadline(http.InputStream, buffer, response);
            using (var reader = new StreamReader(new MemoryStream(buffer, 0, total), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        // A blocking Read on a pool thread. The timer aborts the response at the deadline, which makes the blocked
        // Read throw; that throw is a timeout when the timer fired, a client reset otherwise. The deadline covers
        // the whole body, so a client that trickles bytes cannot keep its slot.
        private static int ReadWithDeadline(Stream input, byte[] buffer, HttpListenerResponse response)
        {
            int state = ReadActive;
            using (new Timer(_ => AbortOnDeadline(response, ref state), null, TimeoutMs, Timeout.Infinite))
            {
                int total = 0;
                try
                {
                    while (total < buffer.Length)
                    {
                        int read = input.Read(buffer, total, buffer.Length - total);
                        if (read == 0)
                        {
                            break;
                        }

                        total += read;
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is HttpListenerException || ex is ObjectDisposedException)
                {
                    throw Volatile.Read(ref state) == ReadTimedOut
                        ? new BodyTimeoutException(TimeoutMs)
                        : (Exception)new ClientAbortedException(ex);
                }

                // The timer may have won the race just as the last byte arrived: the response is already aborted.
                if (Interlocked.CompareExchange(ref state, ReadDone, ReadActive) == ReadTimedOut)
                {
                    throw new BodyTimeoutException(TimeoutMs);
                }

                return total;
            }
        }

        private static void AbortOnDeadline(HttpListenerResponse response, ref int state)
        {
            if (Interlocked.CompareExchange(ref state, ReadTimedOut, ReadActive) != ReadActive)
            {
                return;
            }

            try
            {
                response.Abort();
            }
            catch (Exception ex)
            {
                Log.Warn("could not abort a stalled request: " + ex.Message);
            }
        }
    }
}
