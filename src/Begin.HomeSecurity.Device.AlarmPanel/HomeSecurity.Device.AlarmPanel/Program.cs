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

namespace HomeSecurity.Device.AlarmPanel
{
	public class Program
	{
		// BEGIN******* YOU MUST EDIT THE FOLLOWING
		// Change the following line to set your Unique ID for the MQTT Broker (use your initials)
		private static string _mqttDeviceId = "mjl5";
		// Change the location code of the device (firstfloor, masterbedroom, bedroom1 or bedroom2)
		private static string _locationCode = "firstfloor";
		// END******* 

		// MQTT Message Broker endpoint
        private static string _mqttConnection = "tcp://168.62.48.21:1883";

		private static ILogger _logger;

		public static void Main()
		{
			// Setup the logger
			_logger = new ConsoleLogger();
			_logger.CurrentLogLevel = LogLevel.Debug;

            // Delay 5 seconds to give the board a chance to be interrupted by the IDE
            Thread.Sleep(5000);

            // Begin Initializing network
			Network.InitDhcpNetwork();

			// Begin Creating MQTT client
			IMqtt client = MqttClientFactory.CreateClient(_mqttConnection, _mqttDeviceId, _logger);

			// Begin doing some sucurty related stuff
			AlarmPanelController controller = new AlarmPanelController(client, _logger, "house1", _locationCode);
			controller.Start();

			Thread.Sleep(Timeout.Infinite);

		}
	}
}
