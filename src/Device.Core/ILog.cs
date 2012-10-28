using System;
using Microsoft.SPOT;

namespace Device.Core
{
	public interface ILogger
	{
		void Initialize();
		LogLevel CurrentLogLevel { get; set; }
		void Debug(string message);
		void Info(string message);
		void Error(string message);
		void Error(string message,Exception ex);
	}
}
