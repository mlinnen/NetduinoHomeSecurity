using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using Device.Core;
using MQTT;

namespace HomeSecurity.Device.ExternalDoor
{
    public class Program
    {
        // BEGIN******* YOU MUST EDIT THE FOLLOWING
        // Change the following line to be your IP of your Netduino Device
        private static string _deviceGateway = "192.168.0.1";
        // Change the following line to set your Unique ID for the MQTT Broker (use your inititals)
        private static string _mqttDeviceId = "mjl";
        // END******* 

        // Networking
        private static string _deviceIP = "192.168.0.2";
        private static string _deviceSubnet = "255.255.255.0";

        // MQTT
        private static string _mqttConnection = "tcp://168.62.49.240:1883";

        private static ILogger _logger;

        public static void Main()
        {
            // Setup the logger
            _logger = new ConsoleLogger();
            _logger.CurrentLogLevel = LogLevel.Debug;

            _logger.Debug("Begin Initializing network");
            Network.InitStaticNetwork(_deviceIP, _deviceSubnet, _deviceGateway);
            _logger.Debug("End Initializing network");

            _logger.Debug("Begin Creating MQTT client");
            IMqtt client = MqttClientFactory.CreateClient(_mqttConnection, _mqttDeviceId, _logger);
            _logger.Debug("End Creating MQTT client");


            ExternalDoorController controller = new ExternalDoorController(client, _logger,"front");
            controller.Start();

            Thread.Sleep(Timeout.Infinite);

        }
    }
}
