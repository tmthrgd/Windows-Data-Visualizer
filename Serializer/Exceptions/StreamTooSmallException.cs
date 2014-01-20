using System;

namespace Com.Xenthrax.WindowsDataVisualizer.Serializer.Exceptions
{
	public class StreamTooSmallException : ArgumentException
	{
		internal StreamTooSmallException()
			: base()
		{
		}

		internal StreamTooSmallException(string message)
			: base(message)
		{
		}

		internal StreamTooSmallException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		internal StreamTooSmallException(string message, string paramName)
			: base(message, paramName)
		{
		}

		internal StreamTooSmallException(string message, string paramName, Exception innerException)
			: base(message, paramName, innerException)
		{
		}
		
		internal StreamTooSmallException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
			: base(info, context)
		{
		}

	}
}