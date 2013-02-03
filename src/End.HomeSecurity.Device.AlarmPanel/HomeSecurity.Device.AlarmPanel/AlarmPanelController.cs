using System;
using Microsoft.SPOT;
using MQTT;
using Device.Core;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using System.Threading;
using Toolbox.NETMF.Hardware;

namespace HomeSecurity.Device.AlarmPanel
{
    public class AlarmPanelController
    {
        private readonly IMqtt _mqttService;
        private readonly ILogger _logger;
        private string _locationCode;
        private string _houseCode;
		private OutputPort _awayModeLED = new OutputPort(Pins.GPIO_PIN_D0, false);
		private OutputPort _sleepModeLED = new OutputPort(Pins.GPIO_PIN_D1, false);
		private OutputPort _invalidCodeLED = new OutputPort(Pins.GPIO_PIN_D2, false);
		private AutoRepeatInputPort _windowCircuit = new AutoRepeatInputPort(Pins.GPIO_PIN_D3, Port.ResistorMode.PullUp, false);
		private AutoRepeatInputPort _keyboard0Key = new AutoRepeatInputPort(Pins.GPIO_PIN_D4, Port.ResistorMode.PullUp, false);
		private AutoRepeatInputPort _keyboard1Key = new AutoRepeatInputPort(Pins.GPIO_PIN_D5, Port.ResistorMode.PullUp, false);
		private AutoRepeatInputPort _keyboardEnterKey = new AutoRepeatInputPort(Pins.GPIO_PIN_D6, Port.ResistorMode.PullUp, false);
		private AutoRepeatInputPort _motionCircuit = new AutoRepeatInputPort(Pins.GPIO_PIN_D7, Port.ResistorMode.PullUp, false);
        private AutoRepeatInputPort _sleepMode = new AutoRepeatInputPort(Pins.GPIO_PIN_D8, Port.ResistorMode.PullUp, false);
        private AutoRepeatInputPort _awayMode = new AutoRepeatInputPort(Pins.GPIO_PIN_D9, Port.ResistorMode.PullUp, false);
        private OutputPort _pingResponseOutput = new OutputPort(Pins.ONBOARD_LED, false);
		private static Timer _pingResponseTimer = null;
		private static Timer _invalidCodeFlashLEDTimer = null;
		private static int _currentFlashCount = 2;
		private string _keyboardInput = "";

		#region ctor

		public AlarmPanelController(IMqtt mqttService, ILogger logger,string houseCode, string locationCode)
        {
            _logger = logger;
            _mqttService = mqttService;
			_houseCode = houseCode;
            _locationCode = locationCode;

			// Setup the timer that turns off the onboard led after a length of time
			_pingResponseTimer = new Timer(new TimerCallback(OnPingResponseTimer), this._pingResponseOutput, Timeout.Infinite, Timeout.Infinite);

			// Setup the timer that flashes the LED when the code is invalid
			_invalidCodeFlashLEDTimer = new Timer(new TimerCallback(OnFlashLEDTimer), this._invalidCodeLED, Timeout.Infinite, Timeout.Infinite);

			// Setup the interrupt handler to detect when the windows opened or closed
			_windowCircuit.StateChanged += new AutoRepeatEventHandler(_windowCircuit_StateChanged);

			// Setup the interrupt handler to detect when the motion detector opened or closed
			_motionCircuit.StateChanged += new AutoRepeatEventHandler(_motionCircuit_StateChanged);

			// Setup the interrupt handlers that detect when a keypad key is depressed
			_keyboard0Key.StateChanged += new AutoRepeatEventHandler(_keyboard0Key_StateChanged);
			_keyboard1Key.StateChanged += new AutoRepeatEventHandler(_keyboard1Key_StateChanged);
			_keyboardEnterKey.StateChanged += new AutoRepeatEventHandler(_keyboardEnterKey_StateChanged);

            // Setup the interrupt handlers that detect when the sleep button is pressed
            _sleepMode.StateChanged += new AutoRepeatEventHandler(_sleepMode_StateChanged);

            // Setup the interrupt handlers that detect when the alarm button is pressed
            _awayMode.StateChanged += new AutoRepeatEventHandler(_awayMode_StateChanged);
        }

		#endregion

		#region Public Properties

		public string Topic
		{
			get
			{
				return "/" + _houseCode + "/alarmpanel/" + _locationCode + "/";
			}
		}

		#endregion

		#region Public Methods
		public void Start()
        {
            _logger.Debug("Attempting to connect to Broker");
            if (ConnectToBroker())
            {
                _logger.Debug("Attempting to subscribe to Broker");
                if (Subscribe())
                {
					// TODO add the logic to handle the I/O

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
				Subscription subscription = null; ;
				subscription = new Subscription(Topic + "pingresp", QoS.BestEfforts);
				messageId = _mqttService.Subscribe(subscription);

				// Subscribe to any alarm state changes (away, sleep, off)
				subscription = new Subscription(Topic + "setalarmstate", QoS.BestEfforts);
				messageId = _mqttService.Subscribe(subscription);

				// Subscribe to messages that indicate the code was valid or invalid
				subscription = new Subscription(Topic + "codevalid", QoS.BestEfforts);
				messageId = _mqttService.Subscribe(subscription);

				// Subscribe to messages that indicate the alarm state change was valid or invalid
				subscription = new Subscription(Topic + "alarmstatevalid", QoS.BestEfforts);
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

			CheckForAlarmStateMessages(e);

			CheckForCodeValidMessages(e);

			CheckForAlarmStateValidMessages(e);

            return true;
		}

		public void CheckForAlarmStateMessages(PublishArrivedArgs e)
		{
			if (e.Topic.Equals(Topic + "setalarmstate"))
			{
				if (e.Payload.ToString().Equals("away"))
				{
					_sleepModeLED.Write(false);
					_awayModeLED.Write(true);
				}
				else if (e.Payload.ToString().Equals("sleep"))
				{
					_sleepModeLED.Write(true);
					_awayModeLED.Write(false);
				}
				else if (e.Payload.ToString().Equals("off"))
				{
					_sleepModeLED.Write(false);
					_awayModeLED.Write(false);
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

		public void CheckForAlarmStateValidMessages(PublishArrivedArgs e)
		{
			if (e.Topic.Equals(Topic + "alarmstatevalid"))
			{
				if (e.Payload.ToString().Equals("true"))
				{
					_invalidCodeFlashLEDTimer.Change(Timeout.Infinite, Timeout.Infinite);
					_invalidCodeLED.Write(false);
				}
				else if (e.Payload.ToString().Equals("false"))
				{
					// Since the alarm state was invalid we need to flash the LED on/off twice
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

		void _windowCircuit_StateChanged(object sender, AutoRepeatEventArgs e)
		{
			switch (e.State)
			{
				case AutoRepeatInputPort.AutoRepeatState.Press:
					// The circuit opened meaning a window was opened
					_logger.Debug("Window Circuit Opened");
					_mqttService.Publish(new MqttParcel(Topic + "window", "opened", QoS.BestEfforts, false));
					break;
				case AutoRepeatInputPort.AutoRepeatState.Release:
					// The circuit closed meaning all windows were closed
					_logger.Debug("Window Circuit Closed");
					_mqttService.Publish(new MqttParcel(Topic + "window", "closed", QoS.BestEfforts, false));
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

		void _motionCircuit_StateChanged(object sender, AutoRepeatEventArgs e)
		{
			switch (e.State)
			{
				case AutoRepeatInputPort.AutoRepeatState.Press:
					// The circuit opened meaning a motion detector fired
					_logger.Debug("Motion Circuit Opened");
					_mqttService.Publish(new MqttParcel(Topic + "motion", "opened", QoS.BestEfforts, false));
					break;
				case AutoRepeatInputPort.AutoRepeatState.Release:
					// The circuit closed meaning all motion detectors are not firing
					_logger.Debug("Motion Circuit Closed");
					_mqttService.Publish(new MqttParcel(Topic + "motion", "closed", QoS.BestEfforts, false));
					break;
			}
		}

        void _awayMode_StateChanged(object sender, AutoRepeatEventArgs e)
        {
            switch (e.State)
            {
                case AutoRepeatInputPort.AutoRepeatState.Press:
                    // The away security mode button was pressed
                    _logger.Debug("Away button pressed");
                    _mqttService.Publish(new MqttParcel(Topic + "alarmstate", "away", QoS.BestEfforts, false));
                    break;
            }
        }

        void _sleepMode_StateChanged(object sender, AutoRepeatEventArgs e)
        {
            switch (e.State)
            {
                case AutoRepeatInputPort.AutoRepeatState.Press:
                    // The sleep security mode button was pressed
                    _logger.Debug("Sleep button pressed");
                    _mqttService.Publish(new MqttParcel(Topic + "alarmstate", "sleep", QoS.BestEfforts, false));
                    break;
            }
        }


		#endregion
	}
}
