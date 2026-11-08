using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace BoBox.Bridge
{
    /// <summary>
    /// Loopback-only HTTP server. Listener threads parse requests and queue them;
    /// <see cref="ProcessPending"/> runs them on the Unity main thread and wakes the waiting thread.
    /// </summary>
    internal sealed class BridgeServer
    {
        // HttpListener prefix syntax: https://learn.microsoft.com/dotnet/api/system.net.httplistener
        // Bound to 127.0.0.1 only; never "+" or "*", which would expose the API on the network.
        private const string LoopbackHost = "127.0.0.1";
        private const int MainThreadTimeoutMs = 10000;
        private const int MaxRunsPerFrame = 8;
        private const int MaxInFlightRequests = 16;
        private const string AbandonedTimeoutMessage = "game main thread did not respond in time";
        private const string ClaimedTimeoutMessage = "claimed by main thread, outcome unknown, do not retry";
        private static readonly string FallbackErrorJson = "{\"error\":\"" + ApiResponse.InternalErrorMessage + "\"}";

        private readonly ConcurrentQueue<PendingRequest> _queue = new ConcurrentQueue<PendingRequest>();
        private readonly int _port;
        private readonly Func<ApiRequest, ApiResponse> _handler;
        private readonly SemaphoreSlim _inFlight = new SemaphoreSlim(MaxInFlightRequests, MaxInFlightRequests);
        private HttpListener _listener;
        private Thread _acceptThread;

        public BridgeServer(int port, Func<ApiRequest, ApiResponse> handler)
        {
            _port = port;
            _handler = handler;
        }

        public void Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://" + LoopbackHost + ":" + _port + "/");
            _listener.Start();
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "BoBox-accept" };
            _acceptThread.Start();
            Log.Info("listening on http://" + LoopbackHost + ":" + _port + "/");
        }

        public void Stop()
        {
            HttpListener listener = _listener;
            _listener = null;
            if (listener == null)
            {
                return;
            }

            try
            {
                listener.Close();
            }
            catch (Exception ex)
            {
                Log.Warn("error while stopping listener: " + ex.Message);
            }

            Log.Info("stopped");
        }

        /// <summary>Call from MonoBehaviour.Update only.</summary>
        public void ProcessPending()
        {
            for (int i = 0; i < MaxRunsPerFrame && _queue.TryDequeue(out PendingRequest pending); i++)
            {
                if (!pending.TryClaimForRun())
                {
                    continue;
                }

                pending.Complete(Run(pending.Request));
            }
        }

        private ApiResponse Run(ApiRequest request)
        {
            try
            {
                return _handler(request);
            }
            catch (Exception ex)
            {
                return ApiResponse.FromException(ex, request.Method + " " + request.Path + " failed");
            }
        }

        private void AcceptLoop()
        {
            HttpListener listener = _listener;
            var backoff = new AcceptBackoff();
            while (listener != null && listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                    backoff.Reset();
                }
                catch (Exception) when (!listener.IsListening)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (backoff.ShouldWarn())
                    {
                        Log.Warn("accept failed, retrying with back-off (logged at most every 30 s): " + ex.Message);
                    }

                    Thread.Sleep(backoff.NextDelayMs());
                    continue;
                }

                // Stalled clients hold a thread-pool thread, which the game's own Parallel.For also needs.
                if (!_inFlight.Wait(0))
                {
                    Write(context, new ApiResponse(503, new { error = "too many requests in flight, try again" }));
                    continue;
                }

                ThreadPool.QueueUserWorkItem(_ => Serve(context));
            }
        }

        private void Serve(HttpListenerContext context)
        {
            try
            {
                ApiResponse response;
                try
                {
                    response = Dispatch(context);
                }
                catch (Exception ex) when (ex is BodyTimeoutException || ex is ClientAbortedException)
                {
                    Abort(context, ex);
                    return;
                }
                catch (Exception ex)
                {
                    response = ApiResponse.FromException(ex, "request failed");
                }

                Write(context, response);
            }
            finally
            {
                _inFlight.Release();
            }
        }

        private ApiResponse Dispatch(HttpListenerContext context)
        {
            HttpListenerRequest http = context.Request;
            RejectBrowserRequests(http);

            string path = http.Url.AbsolutePath;
            if (path.Length > 1)
            {
                path = path.TrimEnd('/');
            }

            string body = http.HttpMethod == "POST" ? BodyReader.Read(http, context.Response) : string.Empty;
            var pending = new PendingRequest(new ApiRequest(http.HttpMethod, path, body));
            _queue.Enqueue(pending);

            if (!pending.WaitForResponse(MainThreadTimeoutMs))
            {
                throw new BridgeException(504, pending.IsClaimedForRun ? ClaimedTimeoutMessage : AbandonedTimeoutMessage);
            }

            return pending.Response;
        }

        // A web page can reach 127.0.0.1 too (CSRF, DNS rebinding). Scripts send no Origin header
        // and a known Host, so refuse anything else.
        private void RejectBrowserRequests(HttpListenerRequest http)
        {
            if (!string.IsNullOrEmpty(http.Headers["Origin"]))
            {
                throw new BridgeException(403, "browser-origin requests are not allowed");
            }

            string host = http.Headers["Host"];
            bool hostOk = string.Equals(host, LoopbackHost + ":" + _port, StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "localhost:" + _port, StringComparison.OrdinalIgnoreCase);
            if (!hostOk)
            {
                throw new BridgeException(403, "unexpected Host header");
            }
        }

        // A client that stalled or reset mid-body gets no reply: dropping the connection frees its thread-pool thread.
        private static void Abort(HttpListenerContext context, Exception ex)
        {
            Log.Warn(ex.Message + ", connection dropped");
            try
            {
                context.Response.Abort();
            }
            catch (Exception abortEx)
            {
                Log.Warn("could not abort response: " + abortEx.Message);
            }
        }

        private static void Write(HttpListenerContext context, ApiResponse response)
        {
            try
            {
                int status = response.Status;
                if (!TrySerialize(response.Body, out string json))
                {
                    status = 500;
                    json = FallbackErrorJson;
                }

                byte[] bytes = Encoding.UTF8.GetBytes(json);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                Log.Warn("could not write response: " + ex.Message);
            }
            finally
            {
                try
                {
                    context.Response.Close();
                }
                catch (Exception)
                {
                    // Client already disconnected; nothing left to release.
                }
            }
        }

        private static bool TrySerialize(object body, out string json)
        {
            try
            {
                json = JsonConvert.SerializeObject(body);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("could not serialize response body", ex);
                json = null;
                return false;
            }
        }
    }
}
