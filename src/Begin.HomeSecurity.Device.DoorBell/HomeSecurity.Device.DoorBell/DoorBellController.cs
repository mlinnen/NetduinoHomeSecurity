using System;
using Microsoft.SPOT;
using MQTT;
using Device.Core;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using System.Threading;

namespace HomeSecurity.Device.DoorBell
{
	public class DoorBellController
	{
        private readonly IMqtt _mqttService;
        private readonly ILogger _logger;
        private string _locationCode;
        private string _houseCode;

        #region ctor

        public DoorBellController(IMqtt mqttService, ILogger logger, string houseCode, string locationCode)
        {
            _logger = logger;
            _mqttService = mqttService;
			_houseCode = houseCode;
            _locationCode = locationCode;

        }

		#endregion

		#region Public Properties

		public string Topic
		{
			get
			{
				return "/" + _houseCode + "/doorbell/" + _locationCode + "/";
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
                    // TODO Send out a ping topic with Hello World as the message and it should come back to this device as a pingresp
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
                // TODO Add your subscriptions

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

            return true;
		}

		#endregion
	}
}
