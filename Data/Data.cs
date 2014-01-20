using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	[DataContract]
	public sealed class Data : IData<Data>
	{
		#region Properties
		[DataMember(Name = "_", Order = 1)]
		[JsonProperty("_", Order = 1)]
		internal Guid SerializerType { get; set; }

		[DataMember(Order = 2)]
		public DateTime Retrieved { get; private set; }

		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public Chrome Chrome { get; private set; }
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public Digsby Digsby { get; private set; }
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter", IgnoreCamelCase = true)]
		public FileZilla FileZilla { get; private set; }
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public Firefox Firefox { get; private set; }
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter", IgnoreCamelCase = true)]
		public FlashFXP FlashFXP { get; private set; }
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter", IgnoreCamelCase = true)]
		public IE IE { get; private set; }
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public libpurple libpurple { get; private set; }
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public Opera Opera { get; private set; }
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public Safari Safari { get; private set; }
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter", IgnoreCamelCase = true)]
		public SeaMonkey SeaMonkey { get; private set; }
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public Thunderbird Thunderbird { get; private set; }
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public Trillian Trillian { get; private set; }
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public Windows Windows { get; private set; }
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public WindowsLiveMessenger WindowsLiveMessenger { get; private set; }
		#endregion

		#region Methods
		public Data Initiate()
		{
			try
			{
				this.Retrieved = DateTime.Now;

				this.Chrome               = new Chrome().Initiate();
				this.Digsby               = new Digsby().Initiate();
				this.FileZilla            = new FileZilla().Initiate();
				this.Firefox              = new Firefox().Initiate();
				this.FlashFXP             = new FlashFXP().Initiate();
				this.IE                   = new IE().Initiate();
				this.libpurple            = new libpurple().Initiate();
				this.Opera                = new Opera().Initiate();
				this.Safari               = new Safari().Initiate();
				this.SeaMonkey            = new SeaMonkey().Initiate();
				this.Thunderbird          = new Thunderbird().Initiate();
				this.Trillian             = new Trillian().Initiate();
				this.Windows              = new Windows().Initiate();
				this.WindowsLiveMessenger = new WindowsLiveMessenger().Initiate();
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}
			
			return this;
		}

		private IEnumerable<IData> ProfilesConverter(dynamic Data)
		{
			if (Data == null)
				return null;

			return Data.Profiles;
		}
		#endregion
	}
}