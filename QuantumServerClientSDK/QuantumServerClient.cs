using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;

namespace Quantum
{
     public struct ClientConfig
     {
        public string Url { get; set; }
        public int MessageDelay { get; set; }
        public JsonSerializerSettings SerializationSettings { get; set; }
        public bool EnableLogs { get; set; }
    }

    public class QuantumEvent
    {
        public string type { get; set; }
        public uint id { get; set; }
        public GenericMessage message { get; set; }
    }
     
    public class GenericMessage
    {
        public string type { get; set; }
        public object data { get; set; }
        
        public T GetData<T>()
        {
            return JsonConvert.DeserializeObject<T>(data.ToString());
        }
    }
    
    public class Client
    {
        private SocketIO _socketIO;
        private ClientConfig _config;
        private PingQueue _pingQueue;
        private uint _currentEventId;
        private Stopwatch stopwatch = new Stopwatch();
        
        public int Ping { get; set; }
        
        public Action OnConnected {get; set;}
        public Action OnReconnected {get; set;}
        public Action OnDisconnected {get; set;}
        public Action<QuantumEvent> OnAnyEvent {get; set;}
        public Action<GenericMessage> OnGenericMessage {get; set;}
        public Action<string> Log {get; set;}
        
        public Client(ClientConfig config)
        {
            _config = config;
            _socketIO = new SocketIO(_config.Url);
            
            _socketIO.JsonSerializer = new NewtonsoftJsonSerializer(_config.SerializationSettings);
            
            _socketIO.OnConnected += OnSocketIoOnConnected;
            _socketIO.OnDisconnected += SocketIOOnDisconnected;
            _socketIO.OnReconnected += SocketIOOnReconnected;
            _socketIO.OnError += SocketIOOnError;
            
            _socketIO.On("generic-message", OnGenericMessageCallback);
            _socketIO.OnAny(OnAnyEventCallback);
            
            _pingQueue = new PingQueue(100);
            _currentEventId = 0;
        }
        

        private void OnSocketIoOnConnected(object sender, EventArgs args)
        {
            DebugLog("SocketIO: OnConnected");
            OnConnected?.Invoke();
        }

        private void SocketIOOnReconnected(object sender, int e)
        {
            DebugLog("SocketIO: OnReconnected");
            OnReconnected?.Invoke();
        }

        private void SocketIOOnDisconnected(object sender, string e)
        {
            DebugLog("SocketIO: OnDisconnected");
            OnDisconnected?.Invoke();
        }

        private void SocketIOOnError(object sender, string e)
        {
            DebugLog(e);
        }
        
        private async void OnGenericMessageCallback(SocketIOResponse response)
        {
            DebugLog("Quantum: OnGenericMessage");
            var qEvent = response.GetValue<QuantumEvent>();
            await Task.Delay(Math.Max(_config.MessageDelay - Ping, 0));
            OnGenericMessage?.Invoke(qEvent.message);
        }

        private async void OnAnyEventCallback(string name, SocketIOResponse response)
        {
            DebugLog("Quantum: OnAnyMessage");
            var qEvent = response.GetValue<QuantumEvent>();
            await Task.Delay(Math.Max(_config.MessageDelay - Ping, 0));
            OnAnyEvent?.Invoke(qEvent);
        }

        public async void Connect(object Auth)
        {
            _socketIO.Options.Auth = Auth;
            await _socketIO.ConnectAsync();

            while (_socketIO.Connected)
            {
                await CheckPing();
                Ping = _pingQueue.GetQueue().Sum() / Math.Max(_pingQueue.Count, 1);
                await Task.Delay(200);
            }
        }

        public async Task Disconnect()
        {
            await _socketIO.DisconnectAsync();
        }

        public async Task SendMessage(GenericMessage message)
        {
            DebugLog("Quantum: SendMessage");
            var qEvent = new QuantumEvent
            {
                type = "generic-message",
                id = _currentEventId++,
                message = message
            };
            await SendEvent(qEvent);
        }

        private async Task SendEvent(QuantumEvent qEvent)
        {
            DebugLog("Quantum: SendEvent");
            await _socketIO.EmitAsync(qEvent.type, qEvent);
        }
        
        private async Task CheckPing()
        {
            stopwatch.Start();
            await _socketIO.EmitAsync("game-server-status", response =>
            {
                stopwatch.Stop();
                _pingQueue.Enqueue((int)stopwatch.ElapsedMilliseconds);
                stopwatch.Reset();
            });
        }
        
        private void DebugLog(string text)
        {
            if (_config.EnableLogs)
            {
                Log?.Invoke($"[Log] {text}]");
            }
        }
    }
}