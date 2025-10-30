using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ASRS.ModbusGateway.Models;
using EasyModbus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Subscribing;

namespace ASRS.ModbusGateway
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly PLCSettings _plcSettings;
        private readonly MqttSettings _mqttSettings;

        private ModbusClient _modbusClient;
        private IMqttClient _mqttClient;
        private IMqttClientOptions _mqttOptions;

        public Worker(ILogger<Worker> logger, IOptions<PLCSettings> plcSettings, IOptions<MqttSettings> mqttSettings)
        {
            _logger = logger;
            _plcSettings = plcSettings.Value;
            _mqttSettings = mqttSettings.Value;
        }

        /* protected override async Task ExecuteAsync(CancellationToken stoppingToken)
         {
             await ConnectToMqttAsync(stoppingToken);

             while (!stoppingToken.IsCancellationRequested)
             {
                 try
                 {
                     if (_modbusClient == null || !_modbusClient.Connected)
                     {
                         ConnectToPLC();
                     }

                     if (_modbusClient != null && _modbusClient.Connected)
                     {
                         // Read 10 holding registers from actual PLC
                         int[] values = _modbusClient.ReadHoldingRegisters(0, 10);
                         string message = string.Join(", ", values);
                         _logger.LogInformation($"[{DateTime.Now}] Registers: {message}");
                         await PublishToMqttAsync(message, stoppingToken);
                     }
                     else
                     {
                         // Simulate data when PLC is not connected
                         string simulatedMessage = $"Simulated data at {DateTime.Now:HH:mm:ss.fff}";
                         _logger.LogWarning($"⚠️ PLC not connected. Publishing simulated data: {simulatedMessage}");
                         await PublishToMqttAsync(simulatedMessage, stoppingToken);
                     }


                     await Task.Delay(_plcSettings.PollingIntervalMs, stoppingToken);
                 }
                 catch (Exception ex)
                 {
                     _logger.LogError($"❌ PLC communication error: {ex.Message}");
                     await Task.Delay(_plcSettings.ReconnectDelayMs, stoppingToken);
                 }
             }

             _modbusClient?.Disconnect();
             if (_mqttClient?.IsConnected == true)
                 await _mqttClient.DisconnectAsync();
         }*/

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await ConnectToMqttAsync(stoppingToken);

            Random random = new Random();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_modbusClient == null || !_modbusClient.Connected)
                    {
                        ConnectToPLC();
                    }

                    string message;

                    if (_modbusClient != null && _modbusClient.Connected)
                    {
                        // ✅ Real PLC data
                        int[] values = _modbusClient.ReadHoldingRegisters(0, 10);
                        message = string.Join(", ", values);
                        _logger.LogInformation($"[{DateTime.Now:HH:mm:ss.fff}] Registers: {message}");
                    }
                    else
                    {
                        // 🧪 Simulated numeric data (only numbers)
                        int[] simulatedRegisters = new int[10];
                        for (int i = 0; i < simulatedRegisters.Length; i++)
                        {
                            simulatedRegisters[i] = random.Next(0, 1000); // Random values between 0–999
                        }

                        message = string.Join(", ", simulatedRegisters);
                        _logger.LogWarning($"⚠️ PLC not connected. Publishing simulated registers: {message}");
                    }

                    // ✅ Publish only data string to MQTT
                    await PublishToMqttAsync(message, stoppingToken);

                    await Task.Delay(_plcSettings.PollingIntervalMs, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ PLC communication error: {ex.Message}");
                    await Task.Delay(_plcSettings.ReconnectDelayMs, stoppingToken);
                }
            }

            _modbusClient?.Disconnect();
            if (_mqttClient?.IsConnected == true)
                await _mqttClient.DisconnectAsync();
        }

        // Added method to publish messages to MQTT

        private async Task PublishToMqttAsync(string message, CancellationToken stoppingToken)
        {
            if (_mqttClient?.IsConnected == true)
            {
                var payload = Encoding.UTF8.GetBytes(message);
                var mqttMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(_mqttSettings.TopicPublish)
                    .WithPayload(payload)
                    .WithExactlyOnceQoS()
                    .WithRetainFlag(false)
                    .Build();

                await _mqttClient.PublishAsync(mqttMessage, stoppingToken);
                _logger.LogInformation($"📤 Published to MQTT topic: {_mqttSettings.TopicPublish}");
            }
            else
            {
                _logger.LogWarning("⚠️ MQTT client not connected. Skipping publish.");
            }
        }


        // ==================== PLC Connection ====================
        private void ConnectToPLC()
        {
            try
            {
                _logger.LogInformation($"🔌 Connecting to PLC at {_plcSettings.IpAddress}:{_plcSettings.Port}...");
                _modbusClient = new ModbusClient(_plcSettings.IpAddress, _plcSettings.Port);
                _modbusClient.Connect();
                _logger.LogInformation("✅ Connected to PLC successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"⚠️ Failed to connect to PLC: {ex.Message}");
            }
        }

        // ==================== MQTT Connection ====================
        private async Task ConnectToMqttAsync(CancellationToken stoppingToken)
        {
            try
            {
                var factory = new MqttFactory();
                _mqttClient = factory.CreateMqttClient();

                _mqttOptions = new MqttClientOptionsBuilder()
                    .WithClientId(_mqttSettings.ClientId)
                    .WithTcpServer(_mqttSettings.BrokerAddress, _mqttSettings.Port)
                    .Build();

                // Register event handlers
                _mqttClient.UseApplicationMessageReceivedHandler(async e =>
                {
                    var topic = e.ApplicationMessage.Topic;
                    var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    _logger.LogInformation($"📩 Received MQTT message. Topic: {topic}, Payload: {payload}");

                    await HandleMqttMessageAsync(payload);
                });

                _mqttClient.UseConnectedHandler(async e =>
                {
                    _logger.LogInformation("✅ Connected to MQTT broker successfully.");

                    await _mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(_mqttSettings.TopicSubscribe)
                        .Build());

                    _logger.LogInformation($"📡 Subscribed to topic: {_mqttSettings.TopicSubscribe}");
                });

                _mqttClient.UseDisconnectedHandler(async e =>
                {
                    _logger.LogWarning("⚠️ Disconnected from MQTT broker. Reconnecting in 5 seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(5));

                    try
                    {
                        await _mqttClient.ConnectAsync(_mqttOptions, CancellationToken.None);
                        _logger.LogInformation("🔁 Reconnected to MQTT broker successfully.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Reconnection to MQTT broker failed: {ex.Message}");
                    }
                });

                // Initial connection
                await _mqttClient.ConnectAsync(_mqttOptions, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ MQTT connection error: {ex.Message}");
            }
        }

        // ==================== Handle Incoming MQTT Messages ====================
        private async Task HandleMqttMessageAsync(string payload)
        {
            try
            {
                _logger.LogInformation($"🔧 Handling MQTT message: {payload}");

                if (payload.Contains("READ", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("📥 'READ' command received.");
                    // Example: could re-read PLC registers here
                }
                else if (payload.Contains("WRITE", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("📤 'WRITE' command received.");
                    // Example: send value to PLC here
                }
                else
                {
                    _logger.LogWarning("⚠ Unknown MQTT command.");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error handling MQTT message: {ex.Message}");
            }
        }
    }
}
