using System;
using Microsoft.SPOT;

namespace MQTT
{
	public class MqttPublishMessage
	{
		private string _topic;
		private MqttPayload _payload;
		private bool _retained;
		private QoS _qos;

		public MqttPublishMessage(string topic, MqttPayload payload,bool retained, QoS qos)
		{
			_topic = topic;
			_payload = payload;
			_retained = retained;
			_qos = qos;
		}

		public string Topic
		{
			get { return _topic; }
			set { _topic = value; }
		}
		public MqttPayload Payload
		{
			get { return _payload; }
			set { _payload = value; }
		}
		public bool Retained
		{
			get { return _retained; }
			set { _retained = value; }
		}
		public QoS QualityOfService
		{
			get { return _qos; }
			set { _qos = value; }
		}

	}
}
