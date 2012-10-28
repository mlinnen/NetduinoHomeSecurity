using System;
using Microsoft.SPOT;

namespace MQTT
{
	public class InvalidConnectionStringException:Exception
	{
		public InvalidConnectionStringException(string message) : base(message)
        {

        }

		public InvalidConnectionStringException()
        {

        }

	}
}
