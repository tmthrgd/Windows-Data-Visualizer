using System;

namespace Com.Xenthrax.WindowsDataVisualizer.Serializer.Exceptions
{
	public class FileFormatNotSupportedException : NotSupportedException
	{
		internal FileFormatNotSupportedException()
			: base()
		{
		}

		internal FileFormatNotSupportedException(string message)
			: base(message)
		{
		}

		internal FileFormatNotSupportedException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		internal FileFormatNotSupportedException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
			: base(info, context)
		{
		}
	}
}