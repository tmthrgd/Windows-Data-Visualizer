using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Yaml;
using System.Yaml.Serialization;
using Microsoft.Win32;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	[DataContract]
	public sealed class Digsby : IData<Digsby>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Profile> Profiles { get; private set; }

		public Digsby Initiate()
		{
			string DigsbyAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Digsby");

			if (Directory.Exists(DigsbyAppData))
				this.Profiles = EnumerableEx.Return(new Profile(new DirectoryInfo(DigsbyAppData)).Initiate()).AsSerializable();
			else
				this.Profiles = Enumerable.Empty<Profile>();

			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "Digsby"
				: string.Format("Digsby ({0})", this.Profiles.Count());
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
				this.Accounts = Extensions.DeferExecutionOnce(Digsby.GetAccounts, this.ProfilePath).AsSerializable();

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
			public string Username { get; set; }
			[DataMember]
			public string Password { get; set; }

			public override string ToString()
			{
				return this.Username;
			}
		}
		#endregion

		#region Static Methods
		private static IEnumerable<Account> GetAccounts(DirectoryInfo ProfilePath)
		{
			string LoginInfoPath = Path.Combine(ProfilePath.FullName, "logininfo.yaml");

			if (!File.Exists(LoginInfoPath))
				return Enumerable.Empty<Account>();

			try
			{
				YamlConfig config = new YamlConfig();
				config.AddRule<string>("!python/unicode", ".*", match => match.Value, obj => obj);
				config.AddRule<byte[]>("!binary", ".*", match => Convert.FromBase64String(match.Value), obj => Convert.ToBase64String(obj));

				YamlSerializer Yaml = new YamlSerializer(config);
				object[] Docs;

				using (FileStream FS = new FileStream(LoginInfoPath, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (StreamReader SR = new StreamReader(FS))
					Docs = Yaml.Deserialize(SR.ReadToEnd().Replace("~:", "_~:"), typeof(Helpers.logininfo));
				
				byte[] baseKey;

				using (RegistryKey lm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
				using (RegistryKey rk = lm.OpenSubKey(WindowsRegKey, false))
					baseKey = (rk.GetValue("DigitalProductId") as byte[])
						.Concat(Encoding.ASCII.GetBytes(rk.GetValue("InstallDate").ToString()))
						.ToArray();

				List<Account> Accounts = new List<Account>();

				using (SHA1 Sha1 = new SHA1CryptoServiceProvider())
				{
					foreach (KeyValuePair<object, object> kvp in ((Helpers.logininfo)Docs[0]).users)
					{
						if (!(kvp.Key is string)
							|| !(kvp.Value is Dictionary<object, object>)
							|| (string)kvp.Key == ""
							|| (string)kvp.Key == "_~"
							|| string.Compare((string)kvp.Key, "pos", true) == 0)
							continue;

						Dictionary<object, object> user = (Dictionary<object, object>)kvp.Value;

						if (!user.ContainsKey("username")
							|| !user.ContainsKey("password")
							|| !(user["username"] is string)
							|| !(user["password"] is byte[]))
							continue;

						string username = (string)user["username"];
						byte[] password = (byte[])user["password"];
						
						byte[] byteSHA1Hash = Sha1.ComputeHash(baseKey
							.Concat(Encoding.ASCII.GetBytes(username))
							.ToArray());
						byte[] newHashData = Enumerable.Range(0, 256)
							.Convert<byte>()
							.ToArray();
						int i = 0;

						for (byte num = 0, bTemp; i < newHashData.Length; i++)
						{
							unchecked
							{
								num += bTemp = newHashData[i];
								num += byteSHA1Hash[i % byteSHA1Hash.Length];
							}

							newHashData[i] = newHashData[num];
							newHashData[num] = bTemp;
						}

						i = 0;

						for (byte s = 1, x = 0; i < password.Length; i++)
						{
							byte bTemp = newHashData[s];

							unchecked
							{
								x += bTemp;
							}

							byte b = newHashData[s] = newHashData[x];
							newHashData[x] = bTemp;

							unchecked
							{
								s++;
								b += bTemp;
							}

							password[i] ^= newHashData[b];
						}

						Accounts.Add(new Account
						{
							Password = Encoding.ASCII.GetString(password),
							Username = username
						});
					}
				}

				return Accounts;
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return Enumerable.Empty<Account>();
			}
		}
		#endregion

		private const string WindowsRegKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

		private static class Helpers
		{
			public sealed class logininfo
			{
				public string last;
				public Dictionary<object, object> users;
			}
		}
	}
}