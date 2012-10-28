using System;
using Microsoft.SPOT;

namespace MQTT
{
	public interface IMqtt
	{
		/// <summary>
		/// Publish a message to the MQTT message broker
		/// </summary>
		/// <param name="topic">Destination of message</param>
		/// <param name="payload">Message body</param>
		/// <param name="qos">QoS</param>
		/// <param name="retained">Whether the message is retained by the broker</param>
		/// <returns>Message ID</returns>
		//int Publish(string topic, MqttPayload payload, QoS qos, bool retained);

		/// <summary>
		/// Publish a message to the MQTT message broker
		/// </summary>
		/// <param name="parcel">Parcel containing destination topic, message body, QoS and if the message should be retained</param>
		/// <returns>Message ID</returns>
		int Publish(MqttParcel parcel);

		/// <summary>
		/// Subscribe to many topics
		/// </summary>
		/// <param name="subscriptions">Array of subscription objects</param>
		/// <returns>Message ID</returns>
		int Subscribe(Subscription[] subscriptions);

		/// <summary>
		/// Subscribe to a single topic
		/// </summary>
		/// <param name="subscription">A Subscription object</param>
		/// <returns>Message ID</returns>
		int Subscribe(Subscription subscription);

		/// <summary>
		/// Subscribe to a single topic
		/// </summary>
		/// <param name="topic">Name of topic to subscribe to</param>
		/// <param name="qos">QoS</param>
		/// <returns>Message ID</returns>
		int Subscribe(string topic, QoS qos);

		/// <summary>
		/// Unsubscribe from a topic
		/// </summary>
		/// <param name="topics">Topic Name</param>
		/// <returns>Message ID</returns>
		int Unsubscribe(string[] topics);

		/// <summary>
		/// Fired when the Topic the MQTT client is subscribed to receives a message
		/// </summary>
		event PublishArrivedDelegate PublishArrived;

		    /// <summary>
		/// Connect to the MQTT message broker
		/// </summary>
		void Connect();

		/// <summary>
		/// Disconnect from the MQTT message broker
		/// </summary>
		void Disconnect();

		/// <summary>
		/// Fired when the connection to the broker is lost
		/// </summary>
		event ConnectionDelegate ConnectionLost;

		/// <summary>
		/// Fired when a connection is made with a broker
		/// </summary>
		event ConnectionDelegate Connected;

		/// <summary>
		/// Returns true if the client is connected to a broker, false otherwise
		/// </summary>
		bool IsConnected { get;}

		/// <summary>
		/// Interval (in seconds) in which Client is expected to Ping Broker to keep session alive.
		/// If this interval expires without communication, the broker will assume the client
		/// is disconnected, close the channel, and broker any last will and testament contract.
		/// </summary>
		int KeepAliveInterval { get; set;}

	}
}
