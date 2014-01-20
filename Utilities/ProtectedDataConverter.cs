using System;
using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Com.Xenthrax.RegistrySettings;

namespace Com.Xenthrax.WindowsDataVisualizer.Utilities
{
	public class ProtectedDataConverter : TypeConverter
	{
		public ProtectedDataConverter()
		{
			this.OptionalEntropy = null;
			this.Scope = DataProtectionScope.CurrentUser;
		}

		public ProtectedDataConverter(byte[] optionalEntropy)
		{
			this.OptionalEntropy = optionalEntropy;
			this.Scope = DataProtectionScope.CurrentUser;
		}

		public ProtectedDataConverter(DataProtectionScope scope)
		{
			this.OptionalEntropy = null;
			this.Scope = scope;
		}

		public ProtectedDataConverter(byte[] optionalEntropy, DataProtectionScope scope)
		{
			this.OptionalEntropy = optionalEntropy;
			this.Scope = scope;
		}

		public byte[] OptionalEntropy { get; set; }
		public DataProtectionScope Scope { get; set; }

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(byte[]);
		}

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(byte[]);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value == null)
				return null;

			if (!(value is byte[]))
				throw new ArgumentException("value");

			byte[] result = ProtectedData.Unprotect((byte[])value, this.OptionalEntropy, this.Scope);

			if (context is RegistrySettingsProviderContext)
			{
				RegistrySettingsProviderContext providerContext = (RegistrySettingsProviderContext)context;

				if (providerContext.SourceType == typeof(string))
					return Encoding.Unicode.GetString(result);
			}

			return result;
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (value == null)
				return null;

			if (value is string)
				value = Encoding.Unicode.GetBytes((string)value);

			if (!(value is byte[]))
				throw new ArgumentException("value");

			if (destinationType != typeof(byte[]))
				throw new ArgumentException("destinationType");

			return ProtectedData.Protect((byte[])value, this.OptionalEntropy, this.Scope);
		}
	}
}