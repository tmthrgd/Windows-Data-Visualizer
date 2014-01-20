using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	[DataContract]
	public sealed class SeaMonkey : IData<SeaMonkey>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Firefox.Profile> Profiles { get; private set; }

		public SeaMonkey Initiate()
		{
			// Firefox.Initiate

			DirectoryInfo SeaMonkeyProfilesDir = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "SeaMonkey", "Profiles"));

			if (SeaMonkeyProfilesDir.Exists)
				this.Profiles = SeaMonkeyProfilesDir.GetDirectories().Select(dir => new Firefox.Profile(dir).Initiate()).Memoize().AsSerializable();
			else
				this.Profiles = Enumerable.Empty<Firefox.Profile>();

			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "SeaMonkey"
				: string.Format("SeaMonkey ({0})", this.Profiles.Count());
		}

		private string ProfilesConverter(IEnumerable<Firefox.Profile> Data)
		{
			return string.Format("{0}\r\n\t{1}", Data.Count(), string.Join("\r\n\t", Data)).TrimEnd('\r', '\n', '\t');
		}
	}
}