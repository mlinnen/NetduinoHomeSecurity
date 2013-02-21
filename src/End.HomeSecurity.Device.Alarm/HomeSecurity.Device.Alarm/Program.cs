using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using Device.Core;
using MQTT;

namespace HomeSecurity.Device.Alarm
{
	public class Program
	{
		// BEGIN******* YOU MUST EDIT THE FOLLOWING
        // Change this if you need a different gateway
        private static string _deviceGateway = "192.168.1.1";
		// Change the following line to set your Unique ID for the MQTT Broker (use your initials)
		private static string _mqttDeviceId = "mjl70";
		// Change the IP of your device (this would be provided to you at the event)
		private static string _deviceIP = "192.168.1.4";
		// END******* 

		// Networking
		private static string _deviceSubnet = "255.255.255.0";

		// MQTT Message Broker endpoint
        private static string _mqttConnection = "tcp://168.62.48.21:1883";

		private static ILogger _logger;

		public static void Main()
		{
			// Setup the logger
			_logger = new ConsoleLogger();
			_logger.CurrentLogLevel = LogLevel.Debug;

            // Delay 5 seconds to give the board a chance to be interupted by the IDE
            Thread.Sleep(5000);

            // Begin Initializing network
			Network.InitStaticNetwork(_deviceIP, _deviceSubnet, _deviceGateway);

			// Begin Creating MQTT client
			IMqtt client = MqttClientFactory.CreateClient(_mqttConnection, _mqttDeviceId, _logger);

			// Begin doing some sucurty related stuff
			AlarmController controller = new AlarmController(client, _logger,"house1","firstfloor");
			controller.Start();

			Thread.Sleep(Timeout.Infinite);

		}
	}
}
