using System;
using Microsoft.SPOT;
using MQTT;
using Device.Core;
using System.Threading;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using Toolbox.NETMF.Hardware;

namespace HomeSecurity.Device.ExternalDoor
{
	public class ExternalDoorController
	{
		private readonly IMqtt _mqttService;
		private readonly ILogger _logger;
		private string _deviceCode;
		private string _houseCode;
		private static Timer _pingResponseTimer = null;
        private static Timer _invalidCodeFlashLEDTimer = null;
        private static int _currentFlashCount = 2;
		private OutputPort _pingResponseOutput = new OutputPort(Pins.ONBOARD_LED, false);
		private OutputPort _doorLockedLED = new OutputPort(Pins.GPIO_PIN_D0, false);
        private OutputPort _invalidCodeLED = new OutputPort(Pins.GPIO_PIN_D1, false);
        private AutoRepeatInputPort _door = new AutoRepeatInputPort(Pins.GPIO_PIN_D2, Port.ResistorMode.PullUp, false);
        private AutoRepeatInputPort _keyboard0Key = new AutoRepeatInputPort(Pins.GPIO_PIN_D3, Port.ResistorMode.PullUp, false);
        private AutoRepeatInputPort _keyboard1Key = new AutoRepeatInputPort(Pins.GPIO_PIN_D4, Port.ResistorMode.PullUp, false);
        private AutoRepeatInputPort _keyboardEnterKey = new AutoRepeatInputPort(Pins.GPIO_PIN_D5, Port.ResistorMode.PullUp, false);
        private AutoRepeatInputPort _doorBell = new AutoRepeatInputPort(Pins.GPIO_PIN_D6, Port.ResistorMode.PullUp, false);
        private string _keyboardInput = "";

		#region ctor

		public ExternalDoorController(IMqtt mqttService, ILogger logger,string houseCode, string deviceCode)
		{
			_logger = logger;
			_mqttService = mqttService;
			_houseCode = houseCode;
			_deviceCode = deviceCode;

			// Setup the timer to wait forever
			_pingResponseTimer = new Timer(new TimerCallback(OnPingResponseTimer), this._pingResponseOutput, Timeout.Infinite, Timeout.Infinite);

            // Setup the timer that flashes the LED when the code is invalid
            _invalidCodeFlashLEDTimer = new Timer(new TimerCallback(OnFlashLEDTimer), this._invalidCodeLED, Timeout.Infinite, Timeout.Infinite);

            // Setup the interrupt handlers that detect when a door is opened or closed
            _door.StateChanged += new AutoRepeatEventHandler(_door_StateChanged);
            
            // Setup the interrupt handlers that detect when a keypad key is depressed
            _keyboard0Key.StateChanged += new AutoRepeatEventHandler(_keyboard0Key_StateChanged);
            _keyboard1Key.StateChanged += new AutoRepeatEventHandler(_keyboard1Key_StateChanged);
            _keyboardEnterKey.StateChanged += new AutoRepeatEventHandler(_keyboardEnterKey_StateChanged);

            // Setup the interrupt handlers that detect when a doorbell is pressed
            _doorBell.StateChanged += new AutoRepeatEventHandler(_doorBell_StateChanged);
		}

		#endregion

		#region Public Properties

		public string Topic
		{
			get
			{
				return "/" + _houseCode + "/externaldoor/" + _deviceCode + "/";
			}
		}

		#endregion

		#region Public Methods
		public void Start()
		{
			if (ConnectToBroker())
			{
				if (Subscribe())
				{
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
				Subscription subscription = new Subscription(Topic + "pingresp", QoS.BestEfforts);
				messageId = _mqttService.Subscribe(subscription);
				success = true;

                subscription = new Subscription(Topic + "lock", QoS.BestEfforts);
                messageId = _mqttService.Subscribe(subscription);

                subscription = new Subscription(Topic + "codevalid", QoS.BestEfforts);
                messageId = _mqttService.Subscribe(subscription);

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
				_pingResponseOutput.Write(true);
				_pingResponseTimer.Change(3000, 3000);
				return true;
			}

            CheckForLockUnlockMessages(e);

            CheckForCodeValidMessages(e);

			return true;
		}

        public void CheckForLockUnlockMessages(PublishArrivedArgs e)
        {
            if (e.Topic.Equals(Topic + "setlock"))
            {
                if (e.Payload.ToString().Equals("locked") || e.Payload.ToString().Equals("lock"))
                {
                    _doorLockedLED.Write(true);
                    _mqttService.Publish(new MqttParcel(Topic + "lock", "locked", QoS.BestEfforts, false));
                }
                else if (e.Payload.ToString().Equals("unlock"))
                {
                    _doorLockedLED.Write(false);
                    _mqttService.Publish(new MqttParcel(Topic + "lock", "unlocked", QoS.BestEfforts, false));
                }
            }
        }

        public void CheckForCodeValidMessages(PublishArrivedArgs e)
        {
            if (e.Topic.Equals(Topic + "codevalid"))
            {
                if (e.Payload.ToString().Equals("true"))
                {
                    _invalidCodeFlashLEDTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    _invalidCodeLED.Write(false);
                }
                else if (e.Payload.ToString().Equals("false"))
                {
                    // Since the code was invalid we need to flash the LED on/off twice
                    _currentFlashCount = 4;
                    _invalidCodeFlashLEDTimer.Change(0, 1000);
                }
            }
        }

		private static void OnPingResponseTimer(object state)
		{
			_pingResponseTimer.Change(Timeout.Infinite, Timeout.Infinite);
			OutputPort output = (OutputPort)state;
			bool isOn = output.Read();
			output.Write(!isOn);
		}

        private static void OnFlashLEDTimer(object state)
        {
            _currentFlashCount--;
            if (_currentFlashCount == 0)
            {
                _invalidCodeFlashLEDTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            OutputPort output = (OutputPort)state;
            bool isOn = output.Read();
            output.Write(!isOn);
        }

        void _door_StateChanged(object sender, AutoRepeatEventArgs e)
        {
            switch (e.State)
            {
                case AutoRepeatInputPort.AutoRepeatState.Press:
                    _logger.Debug("opened");
                    _mqttService.Publish(new MqttParcel(Topic + "door", "opened", QoS.BestEfforts, false));
                    break;
                case AutoRepeatInputPort.AutoRepeatState.Release:
                    _logger.Debug("closed");
                    _mqttService.Publish(new MqttParcel(Topic + "door", "closed", QoS.BestEfforts, false));
                    break;
            }
        }

        void _keyboardEnterKey_StateChanged(object sender, AutoRepeatEventArgs e)
        {
            switch (e.State)
            {
                case AutoRepeatInputPort.AutoRepeatState.Press:
                    _logger.Debug("Enter Key Pressed");
                    if (_keyboardInput != "")
                    {
                        _mqttService.Publish(new MqttParcel(Topic + "code", _keyboardInput, QoS.BestEfforts, false));
                        _keyboardInput = "";
                    }
                    break;
                case AutoRepeatInputPort.AutoRepeatState.Release:
                    _logger.Debug("Enter Key Released");
                    break;
            }
        }

        void _keyboard1Key_StateChanged(object sender, AutoRepeatEventArgs e)
        {
            switch (e.State)
            {
                case AutoRepeatInputPort.AutoRepeatState.Press:
                    _logger.Debug("1 Key Pressed");
                    _keyboardInput = _keyboardInput + "1";
                    break;
                case AutoRepeatInputPort.AutoRepeatState.Release:
                    _logger.Debug("1 Key Released");
                    break;
            }
        }

        void _keyboard0Key_StateChanged(object sender, AutoRepeatEventArgs e)
        {
            switch (e.State)
            {
                case AutoRepeatInputPort.AutoRepeatState.Press:
                    _logger.Debug("0 Key Pressed");
                    _keyboardInput = _keyboardInput + "0";
                    break;
                case AutoRepeatInputPort.AutoRepeatState.Release:
                    _logger.Debug("0 Key Released");
                    break;
            }
        }

        void _doorBell_StateChanged(object sender, AutoRepeatEventArgs e)
        {
            switch (e.State)
            {
                case AutoRepeatInputPort.AutoRepeatState.Press:
                    _logger.Debug("Doorbell Pushed");
                    _mqttService.Publish(new MqttParcel(Topic + "doorbell", "pushed", QoS.BestEfforts, false));
                    break;
            }
        }
		#endregion
	}
}
