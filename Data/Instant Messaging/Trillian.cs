
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	[DataContract]
	public class Trillian : IData<Trillian>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Profile> Profiles { get; private set; }

		public Trillian Initiate()
		{
			DirectoryInfo ProfilePath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Trillian"));

			if (ProfilePath.Exists)
				this.Profiles = EnumerableEx.Return(new Profile(ProfilePath).Initiate()).AsSerializable();
			else
				this.Profiles = Enumerable.Empty<Profile>();

			return this;
		}
		
		public override string ToString()
		{
			return (this.Profiles == null)
				? "Trillian"
				: string.Format("Trillian ({0})", this.Profiles.Count());
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
			public IEnumerable<Account> Accounts { get; private set; }

			public Profile Initiate()
			{
				this.Accounts = Extensions.DeferExecutionOnce(Trillian.GetAccounts, this.ProfilePath).AsSerializable();

				return this;
			}

			public override string ToString()
			{
				return this.ProfilePath.FullName;
			}
		}

		[DataContract]
		public sealed class Account : IData
		{
			[DataMember]
			public string DisplayName { get; set;}
			[DataMember]
			public string Password { get; set; }
			[DataMember]
			public string Username { get; set; }

			public override string ToString()
			{
				return this.Username;
			}
		}
		#endregion

		#region Static Methods
		private static IEnumerable<Account> GetAccounts(DirectoryInfo ProfilePath)
		{
			string Acounts = Path.Combine(ProfilePath.FullName, "users", "global", "accounts.ini");

			if (!File.Exists(Acounts))
				return Enumerable.Empty<Account>();

			List<Account> rData = new List<Account>();

			try
			{
				Utilities.IniParser Parser = new Utilities.IniParser(Acounts);
				string TempString = Parser["Accounts"]["num"];
				int Num;

				if (!int.TryParse(TempString, out Num))
					return Enumerable.Empty<Account>();

				for (int i = 0; i < Num; i++)
				{
					IDictionary<string, string> Account = Parser[string.Format("Account{0:000}", i)];
					string _EncPassword = Account["Password"];

					StringBuilder Password = null;

					if (!string.IsNullOrEmpty(_EncPassword))
					{
						byte[] EncPassword = Convert.FromBase64String(_EncPassword);
						Password = new StringBuilder(EncPassword.Length / 2);

						for (int x = 0; 2 * x + 1 < EncPassword.Length; x++)
						{
							int a = EncPassword[2 * x];
							int c;

							if (a >= '0' && a <= '9')
								c = a - '0';
							else
								c = 0xA + (a - 'A');

							a = EncPassword[2 * x + 1];

							if (a >= '0' && a <= '9')
								a = a - '0';
							else
								a = 0xA + (a - 'A');

							c = (c << 4) + a;
							c ^= Trillian.MagicTrillian[x % Trillian.MagicTrillian.Length];

							Password.Append((char)c);
						}
					}

					rData.Add(new Account
					{
						DisplayName = Account["Display Name"],
						Password = (Password == null)
							? string.Empty
							: Password.ToString(),
						Username = Account["Account"]
					});
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}

			return rData;
		}
		#endregion

		private static readonly byte[] MagicTrillian = new byte[] { 243, 038, 129, 196, 057, 134, 219, 146, 113, 163, 185, 230, 083, 122, 149, 124 };
	}
}