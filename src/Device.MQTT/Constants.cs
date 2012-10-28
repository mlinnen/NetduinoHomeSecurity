using System;
using Microsoft.SPOT;

namespace MQTT
{
	static class Constants
	{

		// These have been scaled down to the hardware
		// Maximum values are commented out - you can 
		// adjust, but keep in mind the limits of the hardware

		public const int MQTTPROTOCOLVERSION = 3;
		//public const int MAXLENGTH = 268435455; // 256MB
		public const int MAXLENGTH = 10240; // 10K
		public const int MAX_CLIENTID = 23;
		public const int MIN_CLIENTID = 1;
		public const int MAX_KEEPALIVE = 65535;
		public const int MIN_KEEPALIVE = 0;
		//public const int MAX_USERNAME = 65535;
		//public const int MAX_PASSWORD = 65535;
		public const int MAX_USERNAME = 12;
		public const int MAX_PASSWORD = 12;
		//public const int MAX_TOPIC_LENGTH = 32767;
		public const int MAX_TOPIC_LENGTH = 256;
		public const int MIN_TOPIC_LENGTH = 1;
		public const int MAX_MESSAGEID = 65535;

		// Error Codes
		public const int CLIENTID_LENGTH_ERROR = 1;
		public const int KEEPALIVE_LENGTH_ERROR = 1;
		public const int MESSAGE_LENGTH_ERROR = 1;
		public const int TOPIC_LENGTH_ERROR = 1;
		public const int TOPIC_WILDCARD_ERROR = 1;
		public const int USERNAME_LENGTH_ERROR = 1;
		public const int PASSWORD_LENGTH_ERROR = 1;
		public const int CONNECTION_ERROR = 1;
		public const int ERROR = 1;
		public const int SUCCESS = 0;

		public const int CONNECTION_OK = 0;

		public const int CONNACK_LENGTH = 4;
		public const int PINGRESP_LENGTH = 2;

		public const byte MQTT_CONN_OK = 0x00;  // Connection Accepted
		public const byte MQTT_CONN_BAD_PROTOCOL_VERSION = 0x01;  // Connection Refused: unacceptable protocol version
		public const byte MQTT_CONN_BAD_IDENTIFIER = 0x02;  // Connection Refused: identifier rejected
		public const byte MQTT_CONN_SERVER_UNAVAILABLE = 0x03;  // Connection Refused: server unavailable
		public const byte MQTT_CONN_BAD_AUTH = 0x04;  //  Connection Refused: bad user name or password
		public const byte MQTT_CONN_NOT_AUTH = 0x05;  //  Connection Refused: not authorized

		// Message types
		public const byte MQTT_CONNECT_TYPE = 0x10;
		public const byte MQTT_CONNACK_TYPE = 0x20;
		public const byte MQTT_PUBLISH_TYPE = 0x30;
		public const byte MQTT_PING_REQ_TYPE = 0xc0;
		public const byte MQTT_PING_RESP_TYPE = 0xd0;
		public const byte MQTT_DISCONNECT_TYPE = 0xe0;
		public const byte MQTT_SUBSCRIBE_TYPE = 0x82;
		public const byte MQTT_UNSUBSCRIBE_TYPE = 0xa2;

		// Flags
		public const int CLEAN_SESSION_FLAG = 0x02;
		public const int USING_USERNAME_FLAG = 0x80;
		public const int USING_PASSWORD_FLAG = 0x40;
		public const int CONTINUATION_BIT = 0x80;
	}
}
