using System;

namespace Com.Xenthrax.WindowsDataVisualizer.Serializer.Exceptions
{
	public class InvalidKeySizeException : ArgumentException
	{
		internal InvalidKeySizeException()
			: base()
		{
		}

		internal InvalidKeySizeException(string message)
			: base(message)
		{
		}

		internal InvalidKeySizeException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		internal InvalidKeySizeException(string message, string paramName)
			: base(message, paramName)
		{
		}

		internal InvalidKeySizeException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
			: base(info, context)
		{
		}

		internal InvalidKeySizeException(string message, string paramName, Exception innerException)
			: base(message, paramName, innerException)
		{
		}
	}
}