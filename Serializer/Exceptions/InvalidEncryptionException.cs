using System;

namespace Com.Xenthrax.WindowsDataVisualizer.Serializer.Exceptions
{
	public class InvalidEncryptionException : NotSupportedException
	{
		internal InvalidEncryptionException()
			: base()
		{
		}

		internal InvalidEncryptionException(string message)
			: base(message)
		{
		}

		internal InvalidEncryptionException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		internal InvalidEncryptionException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
			: base(info, context)
		{
		}
	}
}