using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jint.DebugAgent.Domains;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jint.DebugAgent
{
    /// <summary>
    /// Implements the messaging for the chrome debug protocol. The commands are implemented (by domain) in the ...Domain classes
    /// </summary>
    internal class ChromeDebugProtocolServer
    {
        private readonly IProtocolServerOwner owner;
        private readonly DomainBase[] domains;
        private int count;
        private CancellationToken cancellationToken;
        private WebSocket webSocket;
        private readonly object webSocketLock = new object();
        private readonly object sendLock = new object();


        /// <summary/>
        public ChromeDebugProtocolServer(IProtocolServerOwner owner, params DomainBase[] domains)
        {
            this.owner = owner;
            this.domains = domains;
        }

        /// <summary>
        /// Starts the server. This method only returns when the cancellation token is set to cancelled
        /// </summary>
        public async Task Start(CancellationToken cancellationToken, params string[] listenerPrefix)
        {
            this.cancellationToken = cancellationToken;
            HttpListener Listener = new HttpListener();
            Array.ForEach(listenerPrefix, _ => Listener.Prefixes.Add(_));
            Listener.Start();
            Debug.WriteLine("Listening...");

            while (this.cancellationToken.IsCancellationRequested == false)
            {
                HttpListenerContext ListenerContext = await Listener.GetContextAsync();
                if (ListenerContext.Request.IsWebSocketRequest)
                {
                    ProcessRequest(ListenerContext);
                }
                else
                {
                    ListenerContext.Response.StatusCode = 400;
                    ListenerContext.Response.Close();
                }
            }
        }

        /// <summary>
        /// Sends debug protocol messages throught the websocket
        /// </summary>
         public async void Transmit(string domain, string method, JObject parameter)
        {
            await this.SendMessage(new JObject(
                new JProperty("method", string.Join(".", domain, method)),
                new JProperty("params", parameter)
                ));
        }

        /// <summary>
        /// Processes a debug method request by forwarding ti to the corresponding domain implementation
        /// </summary>
        private async void ProcessRequest(HttpListenerContext listenerContext)
        {

            WebSocketContext WebSocketContext;
            try
            {
                WebSocketContext = await listenerContext.AcceptWebSocketAsync(null);
                Interlocked.Increment(ref count);
                Debug.WriteLine("Processed: {0}", count);
            }
            catch (Exception Exception)
            {
                listenerContext.Response.StatusCode = 500;
                listenerContext.Response.Close();
                Debug.WriteLine("Exception: {0}", Exception);
                return;
            }
            lock (this.webSocketLock)
            {
                this.webSocket = WebSocketContext.WebSocket;
            }
            this.owner.NotifyConnected();
            try
            {
                byte[] ReceiveBuffer = new byte[1024];

                while (this.webSocket?.State == WebSocketState.Open && cancellationToken.IsCancellationRequested == false)
                {
                    WebSocketReceiveResult ReceiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(ReceiveBuffer), this.cancellationToken);

                    if (ReceiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", this.cancellationToken);
                    }
                    else if (ReceiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        //Revceive message, decode it and forward it to the correspoinding domain implementation. If the method exists, send back 
                        //the result value or only an acknowledge of the mesage in case no return value is specified
                        string MessageText = Encoding.UTF8.GetString(ReceiveBuffer, 0, ReceiveResult.Count);
                        Debug.WriteLine($">> {MessageText}");

                        JObject Message = JsonConvert.DeserializeObject<JObject>(MessageText);
                        int MessageId = Message["id"].Value<int>();
                        string[] Method = Message["method"].Value<string>().Split('.');
                        JObject Parameter = Message["params"]?.Value<JObject>();
                        try
                        {
                            JObject Result = await this.ProcessMessageAsync(Method[0], Method[1], Parameter);
                            if (Result != null && this.webSocket != null)
                            {
                                JProperty IdProperty = new JProperty("id", MessageId);
                                JProperty ResultProperty = Result.HasValues ? new JProperty("result", Result) : null;
                                JObject Response = ResultProperty != null
                                    ? new JObject(IdProperty, ResultProperty)
                                    : new JObject(IdProperty);
                                try
                                {
                                    Monitor.Enter(this.sendLock);
                                    await SendMessage(Response);
                                }
                                finally
                                {
                                    Monitor.Exit(this.sendLock);
                                }
                            }
                            else
                            {
                                //Ignore error or null results
                            }
                        }
                        catch
                        {
                            //Ignore
                        }
                    }
                }
            }
            catch (Exception Exception)
            {
                Debug.WriteLine("Exception: {0}", Exception);
            }
            finally
            {
                this.owner.NotifyDisconnected();
                lock (this.webSocketLock)
                {
                    // Clean up by disposing the WebSocket once it is closed/aborted.
                    this.webSocket?.Dispose();
                    this.webSocket = null;
                }
            }
        }

        private async Task SendMessage(JObject message)
        {
            try
            {
                Monitor.Enter(this.sendLock);
                string ResponseText = JsonConvert.SerializeObject(message);
                Debug.WriteLine($"<< {ResponseText}");
                await this.webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(ResponseText)), WebSocketMessageType.Text, true, this.cancellationToken);
            }
            catch
            {
                Debug.WriteLine("<< SEND FAILED");
                //Ignore. Send was not successful
            }
            finally
            {
                Monitor.Exit(this.sendLock);
            }
        }

        private async Task<JObject> ProcessMessageAsync(string domain, string method, JObject parameter)
        {
            DomainBase Domain = this.domains.FirstOrDefault(_ => _.Name == domain);
            if (Domain != null)
            {
                return await Domain.ProcessMessageAsync(method, parameter);
            }
            else
            {
                //Domain not supported; ignore
                return null;
            }
        }
    }
}