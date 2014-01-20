using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	[DataContract]
	public class FileZilla : IData<FileZilla>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Profile> Profiles { get; private set; }

		public FileZilla Initiate()
		{
			string FileZillaAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileZilla");

			if (Directory.Exists(FileZillaAppData))
				this.Profiles = EnumerableEx.Return(new Profile(new DirectoryInfo(FileZillaAppData)).Initiate()).AsSerializable();
			else
				this.Profiles = Enumerable.Empty<Profile>();

			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "FileZilla"
				: string.Format("FileZilla ({0})", this.Profiles.Count());
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
			public IEnumerable<Server> Servers { get; private set; }
			[DataMember]
			public IEnumerable<Server> RecentServers { get; private set; }

			public Profile Initiate()
			{
				this.Servers = Extensions.DeferExecutionOnce(FileZilla.GetServers, this.ProfilePath, "sitemanager.xml", "/FileZilla3/Servers/Server").AsSerializable();
				this.RecentServers = Extensions.DeferExecutionOnce(FileZilla.GetServers, this.ProfilePath, "recentservers.xml", "/FileZilla3/RecentServers/Server").AsSerializable();

				return this;
			}

			public override string ToString()
			{
				return this.ProfilePath.FullName;
			}
		}

		[DataContract]
		public class Server : IData
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

			public override string ToString()
			{
				return string.IsNullOrEmpty(this.Name)
					? this.Host
					: string.Format("{0} ({1})", this.Name, this.Host);
			}
		}
		#endregion

		#region Static Methods
		private static IEnumerable<Server> GetServers(DirectoryInfo ProfilePath, string ServersFile, string XPath)
		{
			string SiteManager = Path.Combine(ProfilePath.FullName, ServersFile);

			if (!File.Exists(SiteManager))
				return Enumerable.Empty<Server>();

			try
			{
				XmlDocument Doc = new XmlDocument();
				Doc.XmlResolver = null;
				Doc.Load(SiteManager);

				return Doc.SelectNodes(XPath)
					.Cast<XmlNode>()
					.Where(ServerNode => ServerNode.HasChildNodes)
					.Select(ServerNode =>
					{
						XmlNode Name = ServerNode.SelectSingleNode("Name");
						XmlNode Host = ServerNode.SelectSingleNode("Host");
						XmlNode Port = ServerNode.SelectSingleNode("Port");
						XmlNode User = ServerNode.SelectSingleNode("User");
						XmlNode Pass = ServerNode.SelectSingleNode("Pass");

						int TempInt;
						return new Server
						{
							Name = (Name != null) ? Name.InnerText : null,
							Host = (Host != null) ? Host.InnerText : null,
							Port = (Port != null && int.TryParse(Port.InnerText, out TempInt)) ? TempInt : 0,
							Username = (User != null) ? User.InnerText : null,
							Password = (Pass != null) ? Pass.InnerText : null
						};
					});
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return Enumerable.Empty<Server>();
			}
		}
		#endregion
	}
}