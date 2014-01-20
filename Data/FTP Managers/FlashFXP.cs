using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	[DataContract]
	public class FlashFXP : IData<FlashFXP>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Profile> Profiles { get; private set; }

		public FlashFXP Initiate()
		{
			string FlashFXPPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FlashFXP", "4");

			if (Directory.Exists(FlashFXPPath))
				this.Profiles = EnumerableEx.Return(new Profile(new DirectoryInfo(FlashFXPPath)).Initiate()).AsSerializable();
			else
				this.Profiles = Enumerable.Empty<Profile>();

			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "FlashFXP"
				: string.Format("FlashFXP ({0})", this.Profiles.Count());
		}

		private string ProfilesConverter(IEnumerable<Profile> Data)
		{
			return string.Format("{0}\r\n\t{1}", Data.Count(), string.Join("\r\n\t", Data)).TrimEnd('\r', '\n', '\t');
		}

		#region Types
		[DataContract]
		public sealed class Profile : IData<Profile>
		{
			public Profile() { }

			public Profile(DirectoryInfo Path)
			{
				this.ProfilePath = Path;
			}

			[DataMember]
			[Utilities.DisplayConfiguration("Path")]
			public DirectoryInfo ProfilePath { get; private set; }

			[DataMember]
			public IEnumerable<Server> SitesServers { get; private set; }
			[DataMember]
			public IEnumerable<Server> QuickServers { get; private set; }

			public Profile Initiate()
			{
				this.SitesServers = Extensions.DeferExecutionOnce(FlashFXP.GetServers, this.ProfilePath, "Sites.dat").AsSerializable();
				this.QuickServers = Extensions.DeferExecutionOnce(FlashFXP.GetServers, this.ProfilePath, "quick.dat").AsSerializable();

				return this;
			}

			public override string ToString()
			{
				return this.ProfilePath.FullName;
			}
		}

		[DataContract]
		public sealed class Server : IData
		{
			[DataMember]
			public string Name { get; set; }
			[DataMember]
			public string Host { get; set; }
			[DataMember]
			public int Port { get; set; }
			[DataMember]
			public string Username { get; set; }
			[DataMember]
			public string Password { get; set; }
			[DataMember]
			public bool Anonymous { get; set; }

			public override string ToString()
			{
				return string.Format("{0} ({1})", this.Name, this.Host);
			}
		}
		#endregion

		#region Static Methods
		private static IEnumerable<Server> GetServers(DirectoryInfo ProfilePath, string File)
		{
			string IniPath = Path.Combine(ProfilePath.FullName, File);

			if (!Directory.Exists(IniPath))
				return Enumerable.Empty<Server>();

			List<Server> rData = new List<Server>();

			try
			{
				Utilities.IniParser IniParser = new Utilities.IniParser(IniPath);

				foreach (KeyValuePair<string, IDictionary<string,string>> Site in IniParser)
				{
					Server server = new Server
					{
						Name = Site.Key
					};

					if (Site.Value.ContainsKey("IP"))
						server.Host = Site.Value["IP"];

					int tempInt;

					if (Site.Value.ContainsKey("Port")
						&& int.TryParse(Site.Value["Port"], out tempInt))
						server.Port = tempInt;

					if (Site.Value.ContainsKey("User"))
						server.Username = Site.Value["User"];

					if (Site.Value.ContainsKey("pass") && !string.IsNullOrEmpty(Site.Value["pass"]))
					{
						byte[] pass = Utilities.Utilities.FromHEX(Site.Value["pass"]);
						byte[] Password = new byte[pass.Length - 1];
						string key = Site.Key.Contains((char)0x03)
							? Site.Key
							: "yA36zA48dEhfrvghGRg57h5UlDv3";

						for (int i = 0, m; i < Password.Length; i++)
						{
							m = (pass[i + 1] ^ key[i % key.Length]) - pass[i];

							if (m < 0)
								m += 255;

							Password[i] = (byte)m;
						}

						server.Password = Encoding.UTF8.GetString(Password);
					}

					bool tempBool;

					if (Site.Value.ContainsKey("anonymous") && bool.TryParse(Site.Value["anonymous"], out tempBool))
						server.Anonymous = tempBool;

					rData.Add(server);
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}

			return rData;
		}
		#endregion
	}
}