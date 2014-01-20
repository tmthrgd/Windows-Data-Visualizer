using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace Com.Xenthrax.WindowsDataVisualizer.Serializer
{
	public abstract partial class FileFormat
	{
		/// <summary>
		/// <see cref="Formats.XML"/>
		/// </summary>
		internal sealed class XML : FileFormat
		{
			static XML()
			{
				XML.Serializer = new DataContractSerializer(typeof(Data.Data));
			}

			private static DataContractSerializer Serializer;

			public XML()
			{
				this.Name = "XML";
				this.Fullname = "Extensible Markup Language";
				this.Header = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\" ?><!--46F2F3F5-80B2-4F4C-B166-09B36ABEC75C-->");
				this.Identifier = new Guid("{46F2F3F5-80B2-4F4C-B166-09B36ABEC75C}");
			}

			internal protected override Data.Data DeSerialize(DeSerializeParam P)
			{
				P.InStream.Seek(this.Header.Length, SeekOrigin.Current);
				return XML.Serializer.ReadObject(P.InStream) as Data.Data;
			}

			internal protected override void Serialize(SerializeParam P)
			{
				if (P.EncryptionType != DataEncryptionType.NoEncryption)
					throw new ArgumentException("XML can only use NoEncryption.", "P.EncryptionType");

				XML.Serializer.WriteObject(P.OutStream, P.DataToSerialize);
			}

			internal protected override bool IsEncrypted(Stream InStream)
			{
				return false;
			}

			internal protected override bool IsValid(Stream InStream)
			{
				return true;
			}
		}
	}
}