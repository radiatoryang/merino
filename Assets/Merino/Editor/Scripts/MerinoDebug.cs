using UnityEngine;

namespace Merino
{
	internal enum LoggingLevel
	{
		None = 0,
		Error = 1,
		Warning = 2,
		Info = 3, // Verbose, but not as verbose
		Verbose = 4
	}

	internal static class MerinoDebug 
	{
		internal static void Log(LoggingLevel level, object message)
		{
			//should we actually log this message?
			if (MerinoPrefs.loggingLevel < level)
				return;

			message = "[Merino] " + message; 
			
			switch (level)
			{
				case LoggingLevel.Error:
					Debug.LogError(message);
					break;
				case LoggingLevel.Warning:
					Debug.LogWarning(message);
					break;
				default: //info and verbose
					Debug.Log(message);
					break;
			}
		}

		internal static void LogFormat(LoggingLevel level, string format, params object[] args)
		{
			//should we actually log this message?
			if (MerinoPrefs.loggingLevel < level)
				return;

			format = "[Merino] " + format;
			
			switch (level)
			{
				case LoggingLevel.Error:
					Debug.LogErrorFormat(format, args);
					break;
				case LoggingLevel.Warning:
					Debug.LogWarningFormat(format, args);
					break;
				default: //info and verbose
					Debug.LogFormat(format, args);
					break;
			}
		}

		internal static void LogFormat(LoggingLevel level, Object context, string format, params object[] args)
		{
			//should we actually log this message?
			if (MerinoPrefs.loggingLevel < level)
				return;

			format = "[Merino] " + format;
			
			switch (level)
			{
				case LoggingLevel.Error:
					Debug.LogErrorFormat(context, format, args);
					break;
				case LoggingLevel.Warning:
					Debug.LogWarningFormat(context, format, args);
					break;
				default: //info and verbose
					Debug.LogFormat(context, format, args);
					break;
			}
		}
	}
}
