using System;
using Microsoft.SPOT;
using System.Net;
using Microsoft.SPOT.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Device.Core;

namespace MQTT
{
	internal class Mqtt:IMqtt
	{
		private readonly ILogger _logger;
		private string _connectionString;
		private string _clientId;
		private string _host;
		private int _port;
		private Socket _socket = null;
		private string _userName="";
		private string _password="";
		private int _keepAlive = 20;
		private bool _isConnected;
		private ushort _messageId = 0;
		private Timer _pingTimer;
		private Thread _listenerThread;
		private bool _listenerStarted = false;

		public Mqtt(string connString, string clientID)
		{
			_logger = new Logger();
			_connectionString = connString;
			_clientId = clientID;
		}
		public Mqtt(string connString, string clientID,ILogger logger)
		{
			_logger = logger;
			_connectionString = connString;
			_clientId = clientID;
		}

		//public int Publish(string topic, MqttPayload payload, QoS qos, bool retained)
		//{
		//	return 0;
		//}

		public int Publish(MqttParcel parcel)
		{
			ushort messageId = MessageId;
			int index = 0;
			int tmp = 0;
			int fixedHeader = 0;
			int varHeader = 0;
			int payload = 0;
			int remainingLength = 0;
			int returnCode = 0;
			byte[] buffer = null;

			// Setup a UTF8 encoder
			UTF8Encoding encoder = new UTF8Encoding();

			// Encode the topic
			byte[] utf8Topic = Encoding.UTF8.GetBytes(parcel.Topic);

			// Some error checking
			// Topic contains wildcards
			if ((parcel.Topic.IndexOf('#') != -1) || (parcel.Topic.IndexOf('+') != -1))
			{
				//return Constants.TOPIC_WILDCARD_ERROR;
			}

			// Topic is too long or short
			if ((utf8Topic.Length > Constants.MAX_TOPIC_LENGTH) || (utf8Topic.Length < Constants.MIN_TOPIC_LENGTH))
			{
				//return Constants.TOPIC_LENGTH_ERROR;
			}

			// Calculate the size of the var header
			varHeader += 2; // Topic Name Length (MSB, LSB)
			varHeader += utf8Topic.Length; // Length of the topic

			// Calculate the size of the fixed header
			fixedHeader++; // byte 1

			// Calculate the payload
			payload = parcel.Payload.ToString().Length;

			// Calculate the remaining size
			remainingLength = varHeader + payload;

			// Check that remaining length will fit into 4 encoded bytes
			if (remainingLength > Constants.MAXLENGTH)
			{
				//return Constants.MESSAGE_LENGTH_ERROR;
			}

			// Add space for each byte we need in the fixed header to store the length
			tmp = remainingLength;
			while (tmp > 0)
			{
				fixedHeader++;
				tmp = tmp / 128;
			};
			// End of Fixed Header

			// Build buffer for message
			buffer = new byte[fixedHeader + varHeader + payload];

			// Start of Fixed header
			// Publish (3.3)
			buffer[index++] = Constants.MQTT_PUBLISH_TYPE;

			// Encode the fixed header remaining length
			// Add remaining length
			index = doRemainingLength(remainingLength, index, buffer);
			// End Fixed Header

			// Start of Variable header
			// Length of topic name
			buffer[index++] = (byte)(utf8Topic.Length / 256); // Length MSB
			buffer[index++] = (byte)(utf8Topic.Length % 256); // Length LSB
			// Topic
			for (var i = 0; i < utf8Topic.Length; i++)
			{
				buffer[index++] = utf8Topic[i];
			}
			// End of variable header

			// Start of Payload
			// Message (Length is accounted for in the fixed header)
			for (var i = 0; i < parcel.Payload.TrimmedBuffer.Length; i++)
			{
				buffer[index++] = (byte)parcel.Payload.TrimmedBuffer[i];
			}
			// End of Payload

			try
			{
				returnCode = _socket.Send(buffer, buffer.Length, 0);
			}
			catch (Exception ex)
			{
				_logger.Error("Error on publish " + ex.Message);
			}

			if (returnCode < buffer.Length)
			{
				//return Constants.CONNECTION_ERROR;
			}

			return messageId;
		}

		public int Subscribe(Subscription[] subscriptions)
		{
			throw new NotImplementedException();
		}

		public int Subscribe(Subscription subscription)
		{
			if (_isConnected)
			{
				ushort messageId = MessageId;
				string[] topics = new string[1];
				int[] qos = new int[1];
				topics[0] = subscription.Topic;
				qos[0] = (int)subscription.QualityOfService;
				SubscribeMQTT(_socket, topics, qos, 1,messageId);
				return messageId;
			}
			else
			{
				throw new MqttNotConnectedException("You must connect to a broker before you can call subscribe");
			}
		}

		public int Subscribe(string topic, QoS qos)
		{
			throw new NotImplementedException();
		}

		public int Unsubscribe(string[] topics)
		{
			throw new NotImplementedException();
		}

		public event PublishArrivedDelegate PublishArrived;

		public void Connect()
		{
			_isConnected = false;

			// Parse the connection string
			if (_connectionString == null || _connectionString.Length == 0)
			{
				throw new InvalidConnectionStringException("The connection string cannot be empty");
			}
			string[] parts = _connectionString.Split(':');
			if (parts.Length!=3)
				throw new InvalidConnectionStringException("The connection string is missing the protocol or the port");

			_host = parts[1].Substring(2);
			_port = int.Parse(parts[2]);

			IPHostEntry entry = Dns.GetHostEntry(_host);

			IPAddress address = entry.AddressList[0];
			IPEndPoint endpoint = new IPEndPoint(address, _port);
			NetworkInterface networkInterface = NetworkInterface.GetAllNetworkInterfaces()[0];

			// Create socket and connect to the broker's IP address and port
			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			try
			{
				_socket.Connect(endpoint);
			}
			catch (Exception ex)
			{
				_logger.Error("Sockect Connect Error:", ex);
			}


			int index = 0;
			int tmp = 0;
			int remainingLength = 0;
			int fixedHeader = 0;
			int varHeader = 0;
			int payload = 0;
			int returnCode = 0;
			bool usingUsername = false;
			bool usingPassword = false;
			byte connectFlags = 0x00;
			byte[] buffer = null;
			byte[] inputBuffer = new byte[1];
			byte firstByte = 0x00;

			UTF8Encoding encoder = new UTF8Encoding();

			byte[] utf8ClientID = Encoding.UTF8.GetBytes(_clientId);
			byte[] utf8Username = Encoding.UTF8.GetBytes(_userName);
			byte[] utf8Password = Encoding.UTF8.GetBytes(_password);

			// Some Error Checking
			// ClientID improperly sized
			if ((utf8ClientID.Length > Constants.MAX_CLIENTID) || (utf8ClientID.Length < Constants.MIN_CLIENTID))
			{
				//return Constants.CLIENTID_LENGTH_ERROR;
			}
			// KeepAlive out of bounds
			if ((_keepAlive > Constants.MAX_KEEPALIVE) || (_keepAlive < Constants.MIN_KEEPALIVE))
			{
				//return Constants.KEEPALIVE_LENGTH_ERROR;
			}
			// Username too long
			if (utf8Username.Length > Constants.MAX_USERNAME)
			{
				//return Constants.USERNAME_LENGTH_ERROR;
			}
			// Password too long
			if (utf8Password.Length > Constants.MAX_PASSWORD)
			{
				//return Constants.PASSWORD_LENGTH_ERROR;
			}

			// Check features being used
			if (!_userName.Equals(""))
				usingUsername = true;
			if (!_password.Equals(""))
				usingPassword = true;

			// Calculate the size of the var header
			varHeader += 2; // Protocol Name Length
			varHeader += 6; // Protocol Name
			varHeader++; // Protocol version
			varHeader++; // Connect Flags
			varHeader += 2; // Keep Alive

			// Calculate the size of the fixed header
			fixedHeader++; // byte 1

			// Calculate the payload
			payload = utf8ClientID.Length + 2;
			if (usingUsername)
			{
				payload += utf8Username.Length + 2;
			}
			if (usingPassword)
			{
				payload += utf8Password.Length + 2;
			}

			// Calculate the remaining size
			remainingLength = varHeader + payload;

			// Check that remaining length will fit into 4 encoded bytes
			if (remainingLength > Constants.MAXLENGTH)
			{
				//return Constants.MESSAGE_LENGTH_ERROR;
			}

			tmp = remainingLength;

			// Add space for each byte we need in the fixed header to store the length
			while (tmp > 0)
			{
				fixedHeader++;
				tmp = tmp / 128;
			};
			// End of Fixed Header

			// Build buffer for message
			buffer = new byte[fixedHeader + varHeader + payload];

			// Fixed Header (2.1)
			buffer[index++] = Constants.MQTT_CONNECT_TYPE;

			// Encode the fixed header remaining length
			// Add remaining length
			index = doRemainingLength(remainingLength, index, buffer);
			// End Fixed Header

			// Connect (3.1)
			// Protocol Name
			buffer[index++] = 0; // String (MQIsdp) Length MSB - always 6 so, zeroed
			buffer[index++] = 6; // Length LSB
			buffer[index++] = (byte)'M'; // M
			buffer[index++] = (byte)'Q'; // Q
			buffer[index++] = (byte)'I'; // I
			buffer[index++] = (byte)'s'; // s
			buffer[index++] = (byte)'d'; // d
			buffer[index++] = (byte)'p'; // p

			// Protocol Version
			buffer[index++] = Constants.MQTTPROTOCOLVERSION;

			// Connect Flags
			//if (cleanSession)
			//	connectFlags |= (byte)Constants.CLEAN_SESSION_FLAG;

			if (usingUsername)
				connectFlags |= (byte)Constants.USING_USERNAME_FLAG;
			if (usingPassword)
				connectFlags |= (byte)Constants.USING_PASSWORD_FLAG;

			// Set the connect flags
			buffer[index++] = connectFlags;

			// Keep alive (defaulted to 20 seconds above)
			buffer[index++] = (byte)(_keepAlive / 256); // Keep Alive MSB
			buffer[index++] = (byte)(_keepAlive % 256); // Keep Alive LSB

			// ClientID
			buffer[index++] = (byte)(utf8ClientID.Length / 256); // Length MSB
			buffer[index++] = (byte)(utf8ClientID.Length % 256); // Length LSB
			for (var i = 0; i < utf8ClientID.Length; i++)
			{
				buffer[index++] = utf8ClientID[i];
			}

			// Username
			if (usingUsername)
			{
				buffer[index++] = (byte)(utf8Username.Length / 256); // Length MSB
				buffer[index++] = (byte)(utf8Username.Length % 256); // Length LSB

				for (var i = 0; i < utf8Username.Length; i++)
				{
					buffer[index++] = utf8Username[i];
				}
			}

			// Password
			if (usingPassword)
			{
				buffer[index++] = (byte)(utf8Password.Length / 256); // Length MSB
				buffer[index++] = (byte)(utf8Password.Length % 256); // Length LSB

				for (var i = 0; i < utf8Password.Length; i++)
				{
					buffer[index++] = utf8Password[i];
				}
			}

			try
			{
				// Send the message
				returnCode = _socket.Send(buffer, index, 0);
			}
			catch (Exception ex)
			{
				_logger.Error("Error on Connect " + ex.Message);
			}

			// The return code should equal our buffer length
			if (returnCode != buffer.Length)
			{
				//return Constants.CONNECTION_ERROR;
			}

			// Get the acknowledgement message
			returnCode = _socket.Receive(inputBuffer, 0);

			if (returnCode < 1)
			{
				//return Constants.CONNECTION_ERROR;
			}

			firstByte = inputBuffer[0];

			// If this is the CONNACK - pass it to the CONNACK handler
			if (((int)firstByte & Constants.MQTT_CONNACK_TYPE) > 0)
			{
				returnCode = handleCONNACK(_socket, firstByte);
				if (returnCode > 0)
				{
					//return Constants.ERROR;
				}
			}

			if (!_listenerStarted)
			{
				// Setup and start a new thread for the listener
				_listenerThread = new Thread(listenerThread);
				_listenerThread.Start();
				_listenerStarted = true;
			}
			else
			{
				if (_pingTimer != null)
				{
					_pingTimer.Dispose();
				}
			}
			
			_pingTimer = new Timer(new TimerCallback(pingIt), null, 1000, 6000);

			_isConnected = true;
		}

		public void Disconnect()
		{
			throw new NotImplementedException();
		}

		public event ConnectionDelegate ConnectionLost;

		public event ConnectionDelegate Connected;

		public bool IsConnected
		{
			get { return _isConnected; }
		}

		public int KeepAliveInterval
		{
			get
			{
				return _keepAlive;
			}
			set
			{
				_keepAlive=value;
			}
		}

		public int doRemainingLength(int remainingLength, int index, byte[] buffer)
		{
			int digit = 0;
			do
			{
				digit = remainingLength % 128;
				remainingLength /= 128;
				if (remainingLength > 0)
				{
					digit = digit | Constants.CONTINUATION_BIT;
				}
				buffer[index++] = (byte)digit;
			} while (remainingLength > 0);
			return index;
		}

		// Connect acknowledgement - returns 3 more bytes, byte 3 
		// should be 0 for success
		private int handleCONNACK(Socket mySocket, byte firstByte)
		{
			int returnCode = 0;
			byte[] buffer = new byte[3];
			returnCode = mySocket.Receive(buffer, 0);
			if ((buffer[0] != 2) || (buffer[2] > 0) || (returnCode != 3))
				return Constants.ERROR;
			return Constants.SUCCESS;
		}

		private ushort MessageId
		{
			get { return _messageId++; }
		}

		// Subscribe to a topic 
		private int SubscribeMQTT(Socket mySocket, String[] topic, int[] QoS, int topics, ushort messageId)
		{
			int index = 0;
			int index2 = 0;
			int messageIndex = 0;
			int messageID = 0;
			int tmp = 0;
			int fixedHeader = 0;
			int varHeader = 0;
			int payloadLength = 0;
			int remainingLength = 0;
			int returnCode = 0;
			byte[] buffer = null;
			byte[][] utf8Topics = null;

			UTF8Encoding encoder = new UTF8Encoding();

			utf8Topics = new byte[topics][];

			while (index < topics)
			{
				utf8Topics[index] = new byte[Encoding.UTF8.GetBytes(topic[index]).Length];
				utf8Topics[index] = Encoding.UTF8.GetBytes(topic[index]);
				if ((utf8Topics[index].Length > Constants.MAX_TOPIC_LENGTH) || (utf8Topics[index].Length < Constants.MIN_TOPIC_LENGTH))
				{
					return Constants.TOPIC_LENGTH_ERROR;
				}
				else
				{
					payloadLength += 2; // Size (LSB + MSB)
					payloadLength += utf8Topics[index].Length;  // Length of topic
					payloadLength++; // QoS Requested
					index++;
				}
			}

			// Calculate the size of the fixed header
			fixedHeader++; // byte 1

			// Calculate the size of the var header
			varHeader += 2; // Message ID is 2 bytes

			// Calculate the remaining size
			remainingLength = varHeader + payloadLength;

			// Check that remaining encoded length will fit into 4 encoded bytes
			if (remainingLength > Constants.MAXLENGTH)
				return Constants.MESSAGE_LENGTH_ERROR;

			// Add space for each byte we need in the fixed header to store the length
			tmp = remainingLength;
			while (tmp > 0)
			{
				fixedHeader++;
				tmp = tmp / 128;
			};

			// Build buffer for message
			buffer = new byte[fixedHeader + varHeader + payloadLength];

			// Start of Fixed header
			// Publish (3.3)
			buffer[messageIndex++] = Constants.MQTT_SUBSCRIBE_TYPE;

			// Add remaining length
			messageIndex = doRemainingLength(remainingLength, messageIndex, buffer);
			// End Fixed Header

			// Start of Variable header
			// Message ID
			//messageID = rand.Next(Constants.MAX_MESSAGEID);
			_logger.Debug("SUBSCRIBE: Message ID: " + messageId); 
			buffer[messageIndex++] = (byte)(messageId / 256); // Length MSB
			buffer[messageIndex++] = (byte)(messageId % 256); // Length LSB
			// End of variable header

			// Start of Payload
			index = 0;
			while (index < topics)
			{
				// Length of Topic
				buffer[messageIndex++] = (byte)(utf8Topics[index].Length / 256); // Length MSB 
				buffer[messageIndex++] = (byte)(utf8Topics[index].Length % 256); // Length LSB 

				index2 = 0;
				while (index2 < utf8Topics[index].Length)
				{
					buffer[messageIndex++] = utf8Topics[index][index2];
					index2++;
				}
				buffer[messageIndex++] = (byte)(QoS[index]);
				index++;
			}
			// End of Payload

			try
			{
				returnCode = mySocket.Send(buffer, buffer.Length, 0);
			}
			catch (Exception ex)
			{
				_logger.Error("Error on Subscribe " + ex.Message); 
			}

			if (returnCode < buffer.Length)
				return Constants.CONNECTION_ERROR;

			return Constants.SUCCESS;
		}

		private void pingIt(object o)
		{
			if (_socket!=null)
				PingMQTT(_socket);
		}

		// Ping the MQTT broker - used to extend keep alive
		private int PingMQTT(Socket mySocket)
		{

			int index = 0;
			int returnCode = 0;
			byte[] buffer = new byte[2];

			buffer[index++] = Constants.MQTT_PING_REQ_TYPE;
			buffer[index++] = 0x00;

			try
			{
				// Send the ping
				returnCode = mySocket.Send(buffer, index, 0);
			}
			catch (Exception ex)
			{
				_logger.Error("Error on Ping " + ex.Message); 
			}

			// The return code should equal our buffer length
			if (returnCode != buffer.Length)
			{
				return Constants.CONNECTION_ERROR;
			}
			return Constants.SUCCESS;
		}

		// Listen for data on the socket - call appropriate handlers based on first byte
		private int listen(Socket mySocket)
		{
			int returnCode = 0;
			byte first = 0x00;
			byte[] buffer = new byte[1];
			int retries = 0;
			while (true)
			{
				try
				{
					returnCode = mySocket.Receive(buffer, 0);
					if (returnCode > 0)
					{
						if (retries > 3)
							_logger.Info("Reconnect was successful");
						retries = 0;
						first = buffer[0];
						switch (first >> 4)
						{
							case 0:  // Reserved
								_logger.Debug("First Reserved Message received");
								returnCode = Constants.ERROR;
								break;
							case 1:  // Connect (Broker Only)
								_logger.Debug("CONNECT Message received");
								returnCode = Constants.ERROR;
								break;
							case 2:  // CONNACK
								_logger.Debug("CONNACK Message received");
								returnCode = handleCONNACK(mySocket, first);
								break;
							case 3:  // PUBLISH
								_logger.Debug("PUBLISH Message received");
								returnCode = handlePUBLISH(mySocket, first);
								break;
							case 4:  // PUBACK (QoS > 0 - did it anyway)
								_logger.Debug("PUBACK Message received");
								returnCode = handlePUBACK(mySocket, first);
								break;
							case 5:  // PUBREC (QoS 2)
								_logger.Debug("PUBREC Message received");
								returnCode = Constants.ERROR;
								break;
							case 6:  // PUBREL (QoS 2)
								_logger.Debug("PUBREL Message received");
								returnCode = Constants.ERROR;
								break;
							case 7:  // PUBCOMP (QoS 2)
								_logger.Debug("PUBCOMP Message received");
								returnCode = Constants.ERROR;
								break;
							case 8:  // SUBSCRIBE (Broker only)
								_logger.Debug("SUBSCRIBE Message received");
								returnCode = Constants.ERROR;
								break;
							case 9:  // SUBACK 
								_logger.Debug("SUBACK Message received");
								returnCode = handleSUBACK(mySocket, first);
								break;
							case 10:  // UNSUBSCRIBE (Broker Only)
								_logger.Debug("UNSUBSCRIBE Message received");
								returnCode = Constants.ERROR;
								break;
							case 11:  // UNSUBACK
								_logger.Debug("UNSUBACK Message received");
								returnCode = handleUNSUBACK(mySocket, first);
								break;
							case 12:  // PINGREQ (Technically a Broker Deal - but we're doing it anyway)
								_logger.Debug("PINGREQ Message received");
								returnCode = handlePINGREQ(mySocket, first);
								break;
							case 13:  // PINGRESP
								//_logger.Debug("PINGRESP Message received");
								returnCode = handlePINGRESP(mySocket, first);
								break;
							case 14:  // DISCONNECT (Broker Only)
								_logger.Debug("DISCONNECT Message received");
								returnCode = Constants.ERROR;
								break;
							case 15:  // Reserved
								_logger.Debug("Last Reserved Message received");
								returnCode = Constants.ERROR;
								break;
							default:  // Default action
								_logger.Debug("Unknown Message received");
								returnCode = Constants.ERROR;
								break;
						}
						if (returnCode != Constants.SUCCESS)
						{
							_logger.Error("An error occurred in message processing");
						}
					}
				}
				catch (Exception ex)
				{
					_logger.Error("Error in the listener",ex);
					Thread.Sleep(250);
					retries = retries + 1;
					if (retries > 3)
					{
						_logger.Info("More than 3 retries so going to attempt a reconnect in 30 sec");
						_pingTimer.Dispose();
						Thread.Sleep(30000);
						_logger.Info("Attempting a reconnect");
						Connect();
					}
				}
			}
		}

		// Ping Request 
		private int handlePINGREQ(Socket mySocket, byte firstByte)
		{
			int returnCode = 0;
			byte[] buffer = new byte[1];
			returnCode = mySocket.Receive(buffer, 0);
			if ((returnCode != 1) || (buffer[0] != 0))
				return Constants.ERROR;
			returnCode = sendPINGRESP(mySocket);
			if (returnCode != 0)
				return Constants.ERROR;
			return Constants.SUCCESS;
		}

		// Respond to a PINGRESP
		private int sendPINGRESP(Socket mySocket)
		{
			int index = 0;
			int returnCode = 0;
			byte[] buffer = new byte[2];

			buffer[index++] = Constants.MQTT_PING_RESP_TYPE;
			buffer[index++] = 0x00;

			try
			{
				// Send the ping
				returnCode = mySocket.Send(buffer, index, 0);
			}
			catch (Exception ex)
			{
				_logger.Error("Error on PING Resp",ex);
			}
			// The return code should equal our buffer length
			if (returnCode != buffer.Length)
			{
				return Constants.CONNECTION_ERROR;
			}
			return Constants.SUCCESS;
		}

		// Ping response - this should be a total of 2 bytes - that's pretty much 
		// all I'm looking for.
		private int handlePINGRESP(Socket mySocket, byte firstByte)
		{
			int returnCode = 0;
			byte[] buffer = new byte[1];
			try
			{
				returnCode = mySocket.Receive(buffer, 0);
			}
			catch (Exception ex)
			{
				_logger.Error("Error on handle PING Resp",ex);
			}

			if ((buffer[0] != 0) || (returnCode != 1))
			{
				return Constants.ERROR;
			}
			return Constants.SUCCESS;
		}

		// We're not doing QoS 1 yet, so this is just here for flushing 
		// and to notice if we are getting this message for some reason
		private int handlePUBACK(Socket mySocket, byte firstByte)
		{
			int returnCode = 0;
			int messageID = 0;
			byte[] buffer = new byte[3];
			returnCode = mySocket.Receive(buffer, 0);
			if ((buffer[0] != 2) || (returnCode != 3))
				return Constants.ERROR;
			messageID += buffer[1] * 256;
			messageID += buffer[2];
			_logger.Debug("PUBACK: Message ID: " + messageID);
			return Constants.SUCCESS;
		}

		// Messages from the broker come back to us as publish messages
		private int handlePUBLISH(Socket mySocket, byte firstByte)
		{
			int remainingLength = 0;
			int messageID = 0;
			int topicLength = 0;
			int topicIndex = 0;
			int payloadIndex = 0;
			int index = 0;
			byte[] buffer = null;
			byte[] topic = null;
			byte[] payload = null;
			int QoS = 0x00;
			String topicString = null;
			String payloadString = null;

			remainingLength = undoRemainingLength(mySocket);
			buffer = new byte[remainingLength];
			if ((mySocket.Receive(buffer, 0) != remainingLength) || remainingLength < 5)
				return Constants.ERROR;
			topicLength += buffer[index++] * 256;
			topicLength += buffer[index++];
			topic = new byte[topicLength];
			while (topicIndex < topicLength)
			{
				topic[topicIndex++] = buffer[index++];
			}
			QoS = firstByte & 0x06;
			if (QoS > 0)
			{
				messageID += buffer[index++] * 256;
				messageID += buffer[index++];
				_logger.Debug("PUBLISH: Message ID: " + messageID);
			}
			topicString = new String(Encoding.UTF8.GetChars(topic));
			_logger.Debug("PUBLISH: Topic: " + topicString);
			payload = new byte[remainingLength - index];
			while (index < remainingLength)
			{
				payload[payloadIndex++] = buffer[index++];
			}

			_logger.Debug("PUBLISH: Payload Length: " + payload.Length);

			// This doesn't work if the payload isn't UTF8
			payloadString = new String(Encoding.UTF8.GetChars(payload));
			_logger.Debug("PUBLISH: Payload: " + payloadString);

			MqttPublishMessage msg = new MqttPublishMessage(topicString, new MqttPayload(payloadString), false, (QoS)QoS);
			OnPublishArrived(msg);

			return Constants.SUCCESS;
		}

		private int handleSUBACK(Socket mySocket, byte firstByte)
		{
			int remainingLength = 0;
			int messageID = 0;
			int index = 0;
			int QoSIndex = 0;
			int[] QoS = null;
			byte[] buffer = null;
			remainingLength = undoRemainingLength(mySocket);
			buffer = new byte[remainingLength];
			if ((mySocket.Receive(buffer, 0) != remainingLength) || remainingLength < 3)
				return Constants.ERROR;
			messageID += buffer[index++] * 256;
			messageID += buffer[index++];
			_logger.Debug("SUBACK: Message ID: " + messageID);
			do
			{
				QoS = new int[remainingLength - 2];
				QoS[QoSIndex++] = buffer[index++];
				_logger.Debug("SUBACK: QoS Granted: " + QoS[QoSIndex - 1]);
			} while (index < remainingLength);
			return Constants.SUCCESS;
		}

		// Extract the remaining length field from the fixed header
		private int undoRemainingLength(Socket mySocket)
		{
			int multiplier = 1;
			int count = 0;
			int digit = 0;
			int remainingLength = 0;
			byte[] nextByte = new byte[1];
			do
			{
				if (mySocket.Receive(nextByte, 0) == 1)
				{
					digit = (byte)nextByte[0];
					remainingLength += ((digit & 0x7F) * multiplier);
					multiplier *= 128;
				}
				count++;
			} while (((digit & 0x80) != 0) && count < 4);
			return remainingLength;
		}

		private int handleUNSUBACK(Socket mySocket, byte firstByte)
		{
			int returnCode = 0;
			int messageID = 0;
			byte[] buffer = new byte[3];
			returnCode = mySocket.Receive(buffer, 0);
			if ((buffer[0] != 2) || (returnCode != 3))
				return Constants.ERROR;
			messageID += buffer[1] * 256;
			messageID += buffer[2];
			_logger.Debug("UNSUBACK: Message ID: " + messageID);
			return Constants.SUCCESS;
		}

		// The thread that listens for inbound messages
		private void listenerThread()
		{
			listen(_socket);
		}

		protected void OnPublishArrived(MqttPublishMessage m)
		{
			bool accepted = false;

			if (PublishArrived != null)
			{
				PublishArrivedArgs e = new PublishArrivedArgs(m.Topic, m.Payload, m.Retained, m.QualityOfService);
				try
				{
					accepted |= PublishArrived(this, e);
				}
				catch (Exception ex)
				{
					_logger.Error("Uncaught exception from user delegate: " + ex.ToString());
				}
			}

		}
	}
}
