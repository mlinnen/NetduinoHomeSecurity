using System;
using System.IO;
using Microsoft.SPOT;
using Microsoft.SPOT.IO;

namespace Device.Core
{
	public class Logger : ILogger
	{
		private string _fileName;
		private LogLevel _logLevel= LogLevel.Error;

		public Logger(string filename)
		{
			_fileName = filename;
		}
		public Logger()
		{
			_fileName = @"\SD\log.txt";
			Initialize();
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
			// TODO process all exceptions to create 1 message
			Write(message + " EX: " + ex.ToString());
		}

		private bool VolumeExist()
		{
			VolumeInfo[] volumes = VolumeInfo.GetVolumes();
			foreach (VolumeInfo volumeInfo in volumes)
			{
				if (volumeInfo.Name.Equals("SD"))
					return true;
			}

			return false;
		}

		private void Write(string message)
		{
			try
			{
				FileStream file = File.Exists(_fileName)
									  ? new FileStream(_fileName, FileMode.Append)
									  : new FileStream(_fileName, FileMode.Create);

				StreamWriter streamWriter = new StreamWriter(file);
				streamWriter.WriteLine(DateTime.Now.ToString() + ": " + message);
				Microsoft.SPOT.Debug.Print(DateTime.Now.ToString() + ": " + message);
				streamWriter.Flush();
				streamWriter.Close();

				file.Close();
			}
			catch (Exception ex)
			{
				Microsoft.SPOT.Debug.Print("Error writing to the log file " + ex.Message);
			}	

		}

		public void Initialize()
		{
			// TODO load up configuration
			_logLevel = LogLevel.Debug;

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
	}
}
