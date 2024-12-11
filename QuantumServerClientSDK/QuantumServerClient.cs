using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;


namespace QuantumServerClient
{
     public struct QuantumGameServerConfig{
        public string Url;
        public string Port;
        public string AuthenticationToken;
        public string ServerID;
        public string ServerSecret;
        public int MessageDelay;
        public JsonSerializerSettings SerializationSettings;
    }

    public class QuantumEvent
    {
        public string type;
        public uint id;
        public GenericMessage message;
    }
     
    public class GenericMessage
    {
        public string type { get; set; }
        public object data { get; set; }
    }
    
    public class QuantumServerClient
    {
        private SocketIOClient.SocketIO _client;
        private QuantumGameServerConfig _config;
        public Action OnConnected;
        public int test = 0;
        public Action<string, SocketIOResponse> OnAnyMessage;
        public Action<string, SocketIOResponse> OnGenericMessage;
        private PingQueue _pingQueue;
        private uint _currentEventId;
        Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        public Action<string> Log;
        
        public QuantumServerClient(QuantumGameServerConfig config)
        {
            //var uri = new Uri($"{config.Url}:{config.Port}/gateway");
            _config = config;
            _client = new SocketIOClient.SocketIO(_config.Url);
            _client.JsonSerializer = new NewtonsoftJsonSerializer(_config.SerializationSettings);
            _client.On("generic-message", OnGenericMessageCallback);
            _client.OnAny(OnAnyMessageCallback);
            _client.OnConnected += (sender, args) =>
            {
                OnConnected?.Invoke();
            };
            _pingQueue = new PingQueue(100);
            _currentEventId = 0;
        }

        private async void OnGenericMessageCallback(SocketIOResponse response)
        {
            await Task.Delay(Math.Max(_config.MessageDelay - Ping, 0));
            OnGenericMessage?.Invoke("generic-message", response);
        }

        private async void OnAnyMessageCallback(string name, SocketIOResponse response)
        {
            await Task.Delay(Math.Max(_config.MessageDelay - Ping, 0));
            OnAnyMessage?.Invoke(name, response);
        }

        public int Ping => _pingQueue.GetQueue().Sum() / Math.Max(_pingQueue.Count, 1);

        public async void Connect(object Auth)
        {
            _client.Options.Auth = Auth;
            await _client.ConnectAsync();

            while (_client.Connected)
            {
                await CheckPing();
                await Task.Delay(200);
            }
        }

        public void Disconnect()
        {
            _client.DisconnectAsync();
        }

        public async Task SendMessage(GenericMessage message)
        {
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
            await _client.EmitAsync(qEvent.type, qEvent);
        }
        
        private async Task CheckPing()
        {
            stopwatch.Start();
            await _client.EmitAsync("game-server-status", response =>
            {
                stopwatch.Stop();
                _pingQueue.Enqueue((int)stopwatch.ElapsedMilliseconds);
                stopwatch.Reset();
            });
        }
    }
}