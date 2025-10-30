using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASRS.ModbusGateway.Models
{
    public class MqttSettings
    {
        public string BrokerAddress { get; set; } = "test.mosquitto.org";
        public int Port { get; set; } = 1883;
        public string ClientId { get; set; } = "ASRS-Modbus-Gateway";
        public string TopicPublish { get; set; } = "asrs/plc/registers";
        public string TopicSubscribe { get; set; } = "asrs/plc/commands";
    }
}

