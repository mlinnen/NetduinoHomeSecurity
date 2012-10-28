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
        private string _deviceName;
        public ExternalDoorController(IMqtt mqttService, ILogger logger, string deviceName)
        {
            _logger = logger;
            _mqttService = mqttService;
            _deviceName = deviceName;
        }

        public void Start()
        {
            if (ConnectToBroker())
            {
                if (Subscribe())
                {

                }
            }

            // TODO
        }

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
                Subscription subscription = new Subscription("/house/door/" + _deviceName + "/hello", QoS.BestEfforts);
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
            // TODO reset the netduino or reconnect
            _logger.Info("Connection Lost");
        }

        private bool PublishArrived(object sender, PublishArrivedArgs e)
        {
            _logger.Debug("Msg Recvd: " + e.Topic + " " + e.Payload.ToString());

            if (e.Topic.Equals("/house/door/" + _deviceName + "/front/hello"))
            {
                _logger.Info(e.Payload);
            }

            return true;
        }
    }
}
