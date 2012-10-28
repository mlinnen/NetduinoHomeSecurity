using System;
using Microsoft.SPOT;

namespace MQTT
{
	public class MqttNotConnectedException : MqttException
	{
		public MqttNotConnectedException() : base() { }

		public MqttNotConnectedException(string message) : base(message) { }

		public MqttNotConnectedException(string message, Exception innerException) : base(message, innerException) { }

	}
}
