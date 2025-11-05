using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ASRS.ModbusGateway.Registry;
using ASRS.ModbusGateway.Models;
using EasyModbus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
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

        private readonly Random _random = new();

        public Worker(ILogger<Worker> logger, IOptions<PLCSettings> plcSettings, IOptions<MqttSettings> mqttSettings)
        {
            _logger = logger;
            _plcSettings = plcSettings.Value;
            _mqttSettings = mqttSettings.Value;
        }

        // ==========================================================
        // Main Execution Loop
        // ==========================================================
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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

                    // Read or simulate data
                    var message = _modbusClient?.Connected == true
                        ? ReadFromPLC()
                        : "PLC Not Connected";

                    // Publish to MQTT
                    //await PublishToMqttAsync(message, stoppingToken);

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

        // ==========================================================
        // Read from PLC
        // ==========================================================
        private string ReadFromPLC()
        {
            try
            {
                var plcToWmsTags = ModbusTagRegistry.Tags.Where(t => t.Direction == "PLC_TO_WMS").ToList();
                var data = new StringBuilder();

                foreach (var tag in plcToWmsTags)
                {
                    int value = _modbusClient.ReadHoldingRegisters(tag.Register, 1)[0];
                    _logger.LogInformation($"Read {tag.Name} (Reg:{tag.Register}) = {value}");
                    data.Append($"{tag.Name}:{value}, ");
                }

                string payload = data.ToString().TrimEnd(',', ' ');
                _logger.LogInformation($"📡 Read from PLC: {payload}");
                return payload;
            }
            catch (Exception ex)
            {
                _logger.LogError($"⚠ Error reading PLC registers: {ex.Message}");
                return $"ERROR: {ex.Message}";
            }
        }

        // ==========================================================
        // Simulated Data (for testing)
        // ==========================================================
       /* private string SimulateData()
        {
            var simulated = new StringBuilder();

            foreach (var tag in ModbusTagRegistry.Tags.Where(t => t.Direction == "PLC_TO_WMS"))
            {
                int value = _random.Next(0, 1000);
                simulated.Append($"{tag.Name}:{value}, ");
            }

            string message = simulated.ToString().TrimEnd(',', ' ');
            _logger.LogWarning($"⚠️ PLC not connected. Simulated data: {message}");
            return message;
        }*/

        // ==========================================================
        // Publish to MQTT
        // ==========================================================
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

        // ==========================================================
        // Connect to PLC
        // ==========================================================
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

        // ==========================================================
        // Connect to MQTT
        // ==========================================================
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

                _mqttClient.UseConnectedHandler(async e =>
                {
                    _logger.LogInformation("✅ Connected to MQTT broker successfully.");

                    await _mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(_mqttSettings.TopicSubscribe)
                        .Build());

                    _logger.LogInformation($"📡 Subscribed to topic: {_mqttSettings.TopicSubscribe}");
                });

                _mqttClient.UseApplicationMessageReceivedHandler(async e =>
                {
                    string topic = e.ApplicationMessage.Topic;
                    string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    _logger.LogInformation($"📩 Received MQTT message. Topic: {topic}, Payload: {payload}");

                    await HandleMqttMessageAsync(payload);
                });

                _mqttClient.UseDisconnectedHandler(async e =>
                {
                    _logger.LogWarning("⚠️ Disconnected from MQTT broker. Reconnecting in 5 seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

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

                await _mqttClient.ConnectAsync(_mqttOptions, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ MQTT connection error: {ex.Message}");
            }
        }

        // ==========================================================
        // Handle MQTT Messages (WRITE commands from WMS)
        // ==========================================================
        private async Task HandleMqttMessageAsync(string payload)
        {
            try
            {
                _logger.LogInformation($"🔧 Handling MQTT message: {payload}");

                // Expect payload like "Target_X=25, Target_Y=10"
                if (_modbusClient == null || !_modbusClient.Connected)
                {
                    _logger.LogWarning("⚠️ Cannot write to PLC. Client not connected.");
                    return;
                }

                var pairs = payload.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var kv = pair.Split('=');
                    if (kv.Length != 2) continue;

                    string tagName = kv[0].Trim();
                    if (!int.TryParse(kv[1].Trim(), out int value)) continue;

                    var tag = ModbusTagRegistry.Tags.FirstOrDefault(t =>
                        t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase) &&
                        t.Direction == "WMS_TO_PLC");

                    if (tag != null)
                    {
                        _modbusClient.WriteSingleRegister(tag.Register, value);
                        _logger.LogInformation($"✍️ Wrote {value} to {tag.Name} (Reg:{tag.Register})");
                    }
                    else
                    {
                        _logger.LogWarning($"⚠️ Tag {tagName} not found or not writable.");
                    }
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



/*using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using ASRS.ModbusGateway.Registry;
using ASRS.ModbusGateway.Models;
using EasyModbus;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;


namespace ASRS.ModbusGateway
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly PLCSettings _plcSettings;

        private ModbusClient _modbusClient;
        private readonly Random _random = new();

        public Worker(ILogger<Worker> logger, IOptions<PLCSettings> plcSettings)
        {
            _logger = logger;
            _plcSettings = plcSettings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Run SSE HTTP server in background
            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            // --- SSE endpoint for PLC → WMS data ---
            app.MapGet("/stream", async context =>
            {
                context.Response.Headers.Add("Content-Type", "text/event-stream");
                context.Response.Headers.Add("Cache-Control", "no-cache");
                context.Response.Headers.Add("Connection", "keep-alive");

                _logger.LogInformation("🔗 Client connected to SSE stream");

                while (!stoppingToken.IsCancellationRequested && !context.RequestAborted.IsCancellationRequested)
                {
                    try
                    {
                        if (_modbusClient == null || !_modbusClient.Connected)
                            ConnectToPLC();

                        var message = _modbusClient?.Connected == true
                            ? ReadFromPLC()
                            : SimulateData();

                        var sseMessage = $"data: {message}\n\n";
                        await context.Response.WriteAsync(sseMessage);
                        await context.Response.Body.FlushAsync();

                        await Task.Delay(_plcSettings.PollingIntervalMs, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Error streaming data: {ex.Message}");
                        await Task.Delay(_plcSettings.ReconnectDelayMs, stoppingToken);
                    }
                }

                _logger.LogInformation("❌ SSE client disconnected");
            });

            // --- REST endpoint for WMS → PLC write commands ---
            app.MapPost("/write", async context =>
            {
                try
                {
                    using var reader = new System.IO.StreamReader(context.Request.Body);
                    var payload = await reader.ReadToEndAsync();

                    _logger.LogInformation($"📩 Write command received: {payload}");

                    if (_modbusClient == null || !_modbusClient.Connected)
                    {
                        ConnectToPLC();
                        if (_modbusClient == null || !_modbusClient.Connected)
                        {
                            context.Response.StatusCode = 503;
                            await context.Response.WriteAsync("PLC not connected");
                            return;
                        }
                    }

                    var pairs = payload.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pair in pairs)
                    {
                        var kv = pair.Split('=');
                        if (kv.Length != 2) continue;

                        string tagName = kv[0].Trim();
                        if (!int.TryParse(kv[1].Trim(), out int value)) continue;

                        var tag = ModbusTagRegistry.Tags.FirstOrDefault(t =>
                            t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase) &&
                            t.Direction == "WMS_TO_PLC");

                        if (tag != null)
                        {
                            _modbusClient.WriteSingleRegister(tag.Register, value);
                            _logger.LogInformation($"✍️ Wrote {value} to {tag.Name} (Reg:{tag.Register})");
                        }
                        else
                        {
                            _logger.LogWarning($"⚠️ Tag {tagName} not found or not writable.");
                        }
                    }

                    await context.Response.WriteAsync("✅ Write successful");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error in write endpoint: {ex.Message}");
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync($"Error: {ex.Message}");
                }
            });

            _logger.LogInformation("🚀 Starting SSE server on http://localhost:5050 ...");
            app.Urls.Add("http://localhost:5050");
            await app.RunAsync(stoppingToken);

        }

        // ==========================================================
        // PLC Connection
        // ==========================================================
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
                _logger.LogWarning($"⚠️ Failed to connect to PLC: {ex.Message}");
            }
        }

        // ==========================================================
        // Read PLC Data
        // ==========================================================
        private string ReadFromPLC()
        {
            try
            {
                var plcToWmsTags = ModbusTagRegistry.Tags
                    .Where(t => t.Direction == "PLC_TO_WMS").ToList();

                var data = new StringBuilder();
                foreach (var tag in plcToWmsTags)
                {
                    int value = _modbusClient.ReadHoldingRegisters(tag.Register, 1)[0];
                    data.Append($"{tag.Name}:{value}, ");
                }

                string payload = data.ToString().TrimEnd(',', ' ');
                _logger.LogInformation($"📡 Read from PLC: {payload}");
                return payload;
            }
            catch (Exception ex)
            {
                _logger.LogError($"⚠ Error reading PLC registers: {ex.Message}");
                return $"ERROR: {ex.Message}";
            }
        }

        // ==========================================================
        // Simulated Data
        // ==========================================================
        private string SimulateData()
        {
            var simulated = new StringBuilder();

            foreach (var tag in ModbusTagRegistry.Tags.Where(t => t.Direction == "PLC_TO_WMS"))
            {
                int value = _random.Next(0, 1000);
                simulated.Append($"{tag.Name}:{value}, ");
            }

            string message = simulated.ToString().TrimEnd(',', ' ');
            _logger.LogWarning($"⚠️ PLC not connected. Simulated data: {message}");
            return message;
        }
    }
}*/
