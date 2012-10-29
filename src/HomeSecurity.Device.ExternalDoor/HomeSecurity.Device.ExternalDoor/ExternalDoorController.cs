using System;
using Microsoft.SPOT;
using MQTT;
using Device.Core;

namespace HomeSecurity.Device.ExternalDoor
{
    public class ExternalDoorController
    {
        private readonly IMqtt _mqttService;
        private readonly ILogger _logger;
        private string _deviceCode;
        private string _houseCode;

		#region ctor

		public ExternalDoorController(IMqtt mqttService, ILogger logger,string houseCode, string deviceCode)
        {
            _logger = logger;
            _mqttService = mqttService;
			_houseCode = houseCode;
            _deviceCode = deviceCode;
        }

		#endregion

		#region Public Properties

		public string Topic
		{
			get
			{
				return "/" + _houseCode + "/door/" + _deviceCode + "/";
			}
		}

		#endregion

		#region Public Methods
		public void Start()
        {
			if (ConnectToBroker()){
				if (Subscribe()){
					// TODO add the logic to handle the I/O
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
				Subscription subscription = new Subscription(Topic + "hello", QoS.BestEfforts);
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

            if (e.Topic.Equals(Topic + "hello"))
            {
                _logger.Info(e.Payload);
				return true;
            }

			// TODO test for more subscriptions arriving and execute on them

            return true;
		}

		#endregion
	}
}
