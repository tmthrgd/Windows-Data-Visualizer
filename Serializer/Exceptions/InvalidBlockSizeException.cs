using System;

namespace Com.Xenthrax.WindowsDataVisualizer.Serializer.Exceptions
{
	public class InvalidBlockSizeException : ArgumentException
	{
		internal InvalidBlockSizeException()
			: base()
		{
		}

		internal InvalidBlockSizeException(string message)
			: base(message)
		{
		}

		internal InvalidBlockSizeException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		internal InvalidBlockSizeException(string message, string paramName)
			: base(message, paramName)
		{
		}

		internal InvalidBlockSizeException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
			: base(info, context)
		{
		}

		internal InvalidBlockSizeException(string message, string paramName, Exception innerException)
			: base(message, paramName, innerException)
		{
		}
	}
}