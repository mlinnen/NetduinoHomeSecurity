using System;
using Microsoft.SPOT;

namespace MQTT
{
	public class MqttException : Exception
	{
		/// <summary>
		/// Create a new Mqtt Exception
		/// </summary>
		public MqttException() : base() { }

		/// <summary>
		/// Create a new Mqtt exception
		/// </summary>
		/// <param name="message">Error message</param>
		public MqttException(string message) : base(message) { }

		public MqttException(string message, Exception innerException) : base(message, innerException) { }

	}
}
