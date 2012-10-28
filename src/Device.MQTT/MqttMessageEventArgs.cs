using System;
using Microsoft.SPOT;

namespace MQTT
{
	public class MqttMessageEventArgs
	{
		/// <summary>
		/// Gets or sets the topic the message was published to.
		/// </summary>
		public string Topic
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the message that was published.
		/// </summary>
		public string Message
		{
			get;
			set;
		}

		/// <summary>
		/// Creates a new instance of the MqttMessageEventArgs class.
		/// </summary>
		/// <param name="topic">
		/// A <see cref="System.String"/> that represents the topic the message was published to.
		/// </param>
		/// <param name="message">
		/// A <see cref="MqttMessage"/> that represents the message that was published.
		/// </param>
		public MqttMessageEventArgs(string topic, string message)
		{
			Message = message;
			Topic = topic;
		}
	}
}
