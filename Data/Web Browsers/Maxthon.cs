using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;

namespace FE3458D878534D9183D79D9318BB08C0.Data
{
	[DataContract]
	public sealed class Maxthon : IData<Maxthon>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<sProfile> Profiles { get; private set; }

		public Maxthon Initiate()
		{
			DirectoryInfo Maxthon3Users = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Maxthon3", "Users"));

			if (Maxthon3Users.Exists)
				this.Profiles = Maxthon3Users.GetDirectories().Select(dir => new sProfile(dir).Initiate()).Memoize().AsSerializable();
			else
				this.Profiles = Enumerable.Empty<sProfile>();

			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "Maxthon"
				: string.Format("Maxthon ({0})", this.Profiles.Count());
		}

		private string ProfilesConverter(IEnumerable<sProfile> Data)
		{
			return string.Format("{0}\r\n\t{1}", Data.Count(), string.Join("\r\n\t", Data)).TrimEnd('\r', '\n', '\t');
		}

		#region Types
		[DataContract]
		public class sProfile : IData<sProfile>
		{
			public sProfile() { }

			public sProfile(DirectoryInfo Path)
			{
				this.ProfilePath = Path;
			}

			[DataMember]
			[Utilities.DisplayConfiguration("Path")]
			public DirectoryInfo ProfilePath { get; private set; }

			public sProfile Initiate()
			{
				return this;
			}

			public override string ToString()
			{
				return this.ProfilePath.FullName;
			}
		}
		#endregion
	}
}