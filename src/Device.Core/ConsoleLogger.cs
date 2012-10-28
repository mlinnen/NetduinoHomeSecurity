using System;
using Microsoft.SPOT;

namespace Device.Core
{
    public class ConsoleLogger:ILogger
    {
        private LogLevel _logLevel = LogLevel.Error;

        public void Initialize()
        {
        }

        public LogLevel CurrentLogLevel
        {
            get
            {
                return _logLevel;
            }
            set
            {
                _logLevel = value;
            }
        }

        public void Debug(string message)
        {
            if (_logLevel == LogLevel.Debug)
            {
                Write(message);
            }
        }

        public void Info(string message)
        {
            if (_logLevel == LogLevel.Error)
                return;
            Write(message);
        }

        public void Error(string message)
        {
            Write(message);
        }

        public void Error(string message, Exception ex)
        {
            Write(message + " EX: " + ex.ToString());
        }

        private void Write(string message)
        {
            try
            {
                Microsoft.SPOT.Debug.Print(message);
            }
            catch (Exception ex)
            {

            }

        }
    }
}
