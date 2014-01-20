using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	[DataContract]
	public sealed class libpurple : IData<libpurple>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Profile> Profiles { get; private set; }

		public libpurple Initiate()
		{
			string libpurpleAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".purple");

			if (Directory.Exists(libpurpleAppData))
				this.Profiles = EnumerableEx.Return(new Profile(new DirectoryInfo(libpurpleAppData)).Initiate()).AsSerializable();
			else
				this.Profiles = Enumerable.Empty<Profile>();

			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "libpurple"
				: string.Format("libpurple ({0})", this.Profiles.Count());
		}

		private string ProfilesConverter(IEnumerable<Profile> Data)
		{
			return string.Format("{0}\r\n\t{1}", Data.Count(), string.Join("\r\n\t", Data)).TrimEnd('\r', '\n', '\t');
		}

		#region Types
		[DataContract]
		public class Profile : IData<Profile>
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
			public IEnumerable<Account> Accounts { get; private set; }

			public Profile Initiate()
			{
				this.Accounts = Extensions.DeferExecutionOnce(libpurple.GetAccounts, this.ProfilePath).AsSerializable();

				return this;
			}

			public override string ToString()
			{
				return this.ProfilePath.FullName;
			}
		}

		[DataContract]
		public class Account : IData
		{
			[DataMember]
			public string Protocol { get; set; }
			[DataMember]
			public string Name { get; set; }
			[DataMember]
			public string Password { get; set; }
			[DataMember]
			public int Port { get; set; }
			[DataMember]
			public string Server { get; set; }

			public override string ToString()
			{
				return this.Name;
			}
		}
		#endregion

		#region Static Methods
		private static IEnumerable<Account> GetAccounts(DirectoryInfo ProfilePath)
		{
			string AccountsPath = Path.Combine(ProfilePath.FullName, "accounts.xml");

			if (!File.Exists(AccountsPath))
				return Enumerable.Empty<Account>();

			try
			{
				XmlDocument Doc = new XmlDocument();
				Doc.XmlResolver = null;
				Doc.Load(AccountsPath);

				return Doc.SelectNodes("/account/account")
					.Cast<XmlNode>()
					.Where(ServerNode => ServerNode.HasChildNodes)
					.Select(ServerNode =>
					{
						XmlNode Protocol = ServerNode.SelectSingleNode("protocol");
						XmlNode Name     = ServerNode.SelectSingleNode("name");
						XmlNode Password = ServerNode.SelectSingleNode("password");

						XmlNode Settings = ServerNode.SelectSingleNode("settings[not(@ui)]");
						XmlNode Port     = Settings.SelectSingleNode("setting[@name='port']");
						XmlNode Server   = Settings.SelectSingleNode("setting[@name='server']");

						int TempInt;
						return new Account
						{
							Name = (Name != null) ? Name.InnerText : null,
							Password = (Password != null) ? Password.InnerText : null,
							Protocol = (Protocol != null) ? Protocol.InnerText : null,
							Port = (Port != null && int.TryParse(Port.InnerText, out TempInt)) ? TempInt : 0,
							Server = (Server != null) ? Server.InnerText : null
						};
					});
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return Enumerable.Empty<Account>();
			}
		}
		#endregion
	}
}