using System;
using Microsoft.SPOT;
using Device.Core;

namespace MQTT
{
	public class MqttClientFactory
	{
		public static IMqtt CreateClient(string connString, string clientId)
		{
			return new Mqtt(connString, clientId);
		}
		public static IMqtt CreateClient(string connString, string clientId,ILogger logger)
		{
			return new Mqtt(connString, clientId, logger);
		}

	}
}
