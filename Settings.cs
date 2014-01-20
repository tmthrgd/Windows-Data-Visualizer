using System;
using System.Configuration;
using Com.Xenthrax.RegistrySettings;

namespace Com.Xenthrax.WindowsDataVisualizer.Properties
{
	[SettingsProvider(typeof(RegistrySettingsProvider))]
	[TypeConverter(typeof(Utilities.ProtectedDataConverter), SourceType = typeof(string), TargetType = typeof(byte[]))]
	internal sealed partial class Settings : ApplicationSettingsBase
	{
		private static Settings defaultInstance = (Settings)ApplicationSettingsBase.Synchronized(new Settings());

		public static Settings Default
		{
			get
			{
				return defaultInstance;
			}
		}

		[UserScopedSetting]
		[DefaultSettingValue("")]
		[SubKey("{9A00916F-2678-4666-83E1-1B9710E14F80}")]
		[DefaultKey]
		public string InputLastDataFile
		{
			get
			{
				return (string)this["inputlastdatafile"];
			}
			set
			{
				this["inputlastdatafile"] = value;
			}
		}
		
		[UserScopedSetting]
		[DefaultSettingValue("")]
		[SubKey("{1AB2A06E-4BD3-46D4-BE21-CABC6F4C93DC}")]
		[DefaultKey]
		public string InputLastCertificate
		{
			get
			{
				return (string)this["inputlastcertificate"];
			}
			set
			{
				this["inputlastcertificate"] = value;
			}
		}

		[UserScopedSetting]
		[DefaultSettingValue("")]
		[SubKey("{811EAF7A-3C6D-4CF8-B228-805205E7D5BD}")]
		[DefaultKey]
		public string SaveLastDataFile
		{
			get
			{
				return (string)this["savelastdatafile"];
			}
			set
			{
				this["savelastdatafile"] = value;
			}
		}

		[UserScopedSetting]
		[DefaultSettingValue("")]
		[SubKey("{63906FA9-234B-4C18-A062-C19F13716526}")]
		[DefaultKey]
		public string SaveLastCertificate
		{
			get
			{
				return (string)this["savelastcertificate"];
			}
			set
			{
				this["savelastcertificate"] = value;
			}
		}

		[UserScopedSetting]
		[DefaultSettingValue("")]
		[SubKey("{5571FEDD-B365-4C63-A15B-F89BFDAB9858}")]
		[DefaultKey]
		public string SaveLastEncryptionType
		{
			get
			{
				return (string)this["savelastencryptiontype"];
			}
			set
			{
				this["savelastencryptiontype"] = value;
			}
		}

		[UserScopedSetting]
		[DefaultSettingValue("")]
		[SubKey("{C1C75A61-9D62-4CC3-8A35-92042A2B2F20}")]
		[DefaultKey]
		public string SaveLastFormat
		{
			get
			{
				return (string)this["savelastformat"];
			}
			set
			{
				this["savelastformat"] = value;
			}
		}
	}
}