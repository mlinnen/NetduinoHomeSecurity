using System;
using Microsoft.SPOT;
using MQTT;
using Device.Core;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using System.Threading;

namespace HomeSecurity.Device.ExternalDoor
{
    public class AlarmController
    {
        private readonly IMqtt _mqttService;
        private readonly ILogger _logger;
        private string _locationCode;
        private string _houseCode;
        private const string _masterBedroomBurglarTopic = "/alarmpanel/masterbedroom/burglar";
        private const string _bedroom1BurglarTopic = "/alarmpanel/bedroom1/burglar";
        private const string _bedroom2BurglarTopic = "/alarmpanel/bedroom2/burglar";
        private const string _firstFloorBurglarTopic = "/alarmpanel/firstfloor/burglar";
        private const string _masterBedroomWindowTopic = "/alarmpanel/masterbedroom/window";
        private const string _bedroom1WindowTopic = "/alarmpanel/bedroom1/window";
        private const string _bedroom2WindowTopic = "/alarmpanel/bedroom2/window";
        private const string _firstFloorWindowTopic = "/alarmpanel/firstfloor/window";
        private const string _firstFloorMotionTopic = "/alarmpanel/firstfloor/motion";
        private const string _frontDoorTopic = "/externaldoor/front/door";
        private const string _sideDoorTopic = "/externaldoor/side/door";
        private const string _backDoorTopic = "/externaldoor/back/door";
        private bool _masterBedroomBurglarAlarmOn = false;
        private bool _bedroom1BurglarAlarmOn = false;
        private bool _bedroom2BurglarAlarmOn = false;
        private bool _firsFloorBurglarAlarmOn = false;
        private static Timer _pingResponseTimer = null;
        private Timer _burglarAlarmTimer = null;
        private OutputPort _burglarAlarmOutput = new OutputPort(Pins.GPIO_PIN_D0, false);
        private OutputPort _masterBedroomWindowOutput = new OutputPort(Pins.GPIO_PIN_D1, false);
        private OutputPort _bedroom1WindowOutput = new OutputPort(Pins.GPIO_PIN_D2, false);
        private OutputPort _bedroom2WindowOutput = new OutputPort(Pins.GPIO_PIN_D3, false);
        private OutputPort _firstFloorWindowOutput = new OutputPort(Pins.GPIO_PIN_D4, false);
        private OutputPort _firstFloorMotionOutput = new OutputPort(Pins.GPIO_PIN_D5, false);
        private OutputPort _frontDoorOutput = new OutputPort(Pins.GPIO_PIN_D6, false);
        private OutputPort _backDoorOutput = new OutputPort(Pins.GPIO_PIN_D7, false);
        private OutputPort _sideDoorOutput = new OutputPort(Pins.GPIO_PIN_D8, false);
        private OutputPort _pingResponseOutput = new OutputPort(Pins.ONBOARD_LED, false);

		#region ctor

		public AlarmController(IMqtt mqttService, ILogger logger,string houseCode, string locationCode)
        {
            _logger = logger;
            _mqttService = mqttService;
			_houseCode = houseCode;
            _locationCode = locationCode;

            // Setup the timer to wait forever
            _burglarAlarmTimer = new Timer(new TimerCallback(OnTimer), this._burglarAlarmOutput, Timeout.Infinite, Timeout.Infinite);

            _pingResponseTimer = new Timer(new TimerCallback(OnPingResponseTimer), this._pingResponseOutput, Timeout.Infinite, Timeout.Infinite);
        }

		#endregion

		#region Public Properties

		public string Topic
		{
			get
			{
				return "/" + _houseCode + "/alarm/" + _locationCode + "/";
			}
		}

		#endregion

		#region Public Methods
		public void Start()
        {
			if (ConnectToBroker()){
				if (Subscribe()){

                    // Send out a ping topic with Hello World as the message and it should come back to this device as a pingresp
                    _mqttService.Publish(new MqttParcel(Topic + "ping","Hello world",QoS.BestEfforts,false));
				}
				else
					_logger.Error("Unable to subscribe to the Broker");
			}
			else
				_logger.Error("Unable to connect to the Broker");
        }
		#endregion

		#region Private methods
		private bool ConnectToBroker()
        {
            bool success = false;
            try
            {
                _mqttService.PublishArrived -= new PublishArrivedDelegate(PublishArrived);
                _mqttService.ConnectionLost -= new ConnectionDelegate(ConnectionLost);
                _mqttService.Connect();
                success = true;
                _mqttService.PublishArrived += new PublishArrivedDelegate(PublishArrived);
                _mqttService.ConnectionLost += new ConnectionDelegate(ConnectionLost);

            }
            catch (Exception ex)
            {
                _logger.Debug("Unable to connect " + ex.Message);
            }

            return success;
        }

        private bool Subscribe()
        {
            bool success = false;
            int messageId = 0;

            try
            {
                Subscription subscription = null;
                subscription = new Subscription(Topic + "pingresp", QoS.BestEfforts);
                messageId = _mqttService.Subscribe(subscription);

                // Subscribe to any alarm panel burglar messages
                string topic = "/" + _houseCode + "/alarmpanel/+/burglar";
                subscription = new Subscription(topic, QoS.BestEfforts);
                messageId = _mqttService.Subscribe(subscription);

                // Subscribe to any alarm panel window messages
                topic = "/" + _houseCode + "/alarmpanel/+/window";
                subscription = new Subscription(topic, QoS.BestEfforts);
                messageId = _mqttService.Subscribe(subscription);

                // Subscribe to any alarm panel motion messages
                topic = "/" + _houseCode + "/alarmpanel/+/motion";
                subscription = new Subscription(topic, QoS.BestEfforts);
                messageId = _mqttService.Subscribe(subscription);

                // Subscribe to any external door messages
                topic = "/" + _houseCode + "/externaldoor/+/door";
                subscription = new Subscription(topic, QoS.BestEfforts);
                messageId = _mqttService.Subscribe(subscription);

                success = true;
            }
            catch (Exception ex)
            {
                _logger.Error("Exception during subscription", ex);
            }
            return success;
        }

        private void ConnectionLost(object sender, EventArgs e)
        {
            _logger.Info("Connection Lost");
        }

        private bool PublishArrived(object sender, PublishArrivedArgs e)
        {
			_logger.Info("Msg Recvd: " + e.Topic + " " + e.Payload.ToString());

            if (e.Topic.Equals(Topic + "pingresp"))
            {
                _logger.Info(e.Payload);
                _pingResponseOutput.Write(true);
                _pingResponseTimer.Change(3000, 3000);
				return true;
            }

            CheckForBurglarMessages(e);

            CheckForWindowMessages(e);

            CheckForDoorMessages(e);

            CheckForMotionMessages(e);

            return true;
		}

        public void CheckForBurglarMessages(PublishArrivedArgs e)
        {
            if (e.Topic.Equals("/" + _houseCode + _masterBedroomBurglarTopic))
            {
                _masterBedroomBurglarAlarmOn = ParseBurglarMessageValue(e.Payload);

                SetBurglarAlarmOutput();
            }

            if (e.Topic.Equals("/" + _houseCode + _bedroom1BurglarTopic))
            {
                _bedroom1BurglarAlarmOn = ParseBurglarMessageValue(e.Payload);

                SetBurglarAlarmOutput();
            }

            if (e.Topic.Equals("/" + _houseCode + _bedroom2BurglarTopic))
            {
                _bedroom2BurglarAlarmOn = ParseBurglarMessageValue(e.Payload);

                SetBurglarAlarmOutput();
            }

            if (e.Topic.Equals("/" + _houseCode + _firstFloorBurglarTopic))
            {
                _firsFloorBurglarAlarmOn = ParseBurglarMessageValue(e.Payload);

                SetBurglarAlarmOutput();
            }
        }

        public void CheckForWindowMessages(PublishArrivedArgs e)
        {
            if (e.Topic.Equals("/" + _houseCode + _masterBedroomWindowTopic))
            {
                _masterBedroomWindowOutput.Write(ParseSensorMessageValue(e.Payload));
            }

            if (e.Topic.Equals("/" + _houseCode + _bedroom1WindowTopic))
            {
                _bedroom1WindowOutput.Write(ParseSensorMessageValue(e.Payload));
            }

            if (e.Topic.Equals("/" + _houseCode + _bedroom2WindowTopic))
            {
                _bedroom2WindowOutput.Write(ParseSensorMessageValue(e.Payload));
            }

            if (e.Topic.Equals("/" + _houseCode + _firstFloorWindowTopic))
            {
                _firstFloorWindowOutput.Write(ParseSensorMessageValue(e.Payload));
            }
        }

        public void CheckForDoorMessages(PublishArrivedArgs e)
        {
            if (e.Topic.Equals("/" + _houseCode + _frontDoorTopic))
            {
                _frontDoorOutput.Write(ParseSensorMessageValue(e.Payload));
            }

            if (e.Topic.Equals("/" + _houseCode + _backDoorTopic))
            {
                _backDoorOutput.Write(ParseSensorMessageValue(e.Payload));
            }

            if (e.Topic.Equals("/" + _houseCode + _sideDoorTopic))
            {
                _sideDoorOutput.Write(ParseSensorMessageValue(e.Payload));
            }

        }

        public void CheckForMotionMessages(PublishArrivedArgs e)
        {
            if (e.Topic.Equals("/" + _houseCode + _firstFloorMotionTopic))
            {
                _firstFloorMotionOutput.Write(ParseSensorMessageValue(e.Payload));
            }
        }

        public void SetBurglarAlarmOutput()
        {
            if (_masterBedroomBurglarAlarmOn || _bedroom1BurglarAlarmOn || _bedroom2BurglarAlarmOn || _firsFloorBurglarAlarmOn)
            {
                _burglarAlarmTimer.Change(0, 2000);
            }
            else
            {
                _burglarAlarmTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _burglarAlarmOutput.Write(false);
            }
        }

        private bool ParseBurglarMessageValue(MqttPayload payload)
        {
            bool returnValue = false;

            if (payload.ToString().Equals("on"))
            {
                returnValue = true;
            }
            else if (payload.ToString().Equals("off"))
            {
                returnValue = false;
            }
            return returnValue;
        }

        private bool ParseSensorMessageValue(MqttPayload payload)
        {
            bool returnValue = false;

            if (payload.ToString().Equals("opened"))
            {
                returnValue = true;
            }
            else if (payload.ToString().Equals("closed"))
            {
                returnValue = false;
            }
            return returnValue;
        }

        private static void OnTimer(object state)
        {
            OutputPort output = (OutputPort)state;
            bool isOn = output.Read();
            output.Write(!isOn);
        }

        private static void OnPingResponseTimer(object state)
        {
            _pingResponseTimer.Change(Timeout.Infinite, Timeout.Infinite);
            OutputPort output = (OutputPort)state;
            bool isOn = output.Read();
            output.Write(!isOn);
        }
        
        #endregion
	}
}
