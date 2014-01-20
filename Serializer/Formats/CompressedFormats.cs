using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace Com.Xenthrax.WindowsDataVisualizer.Serializer
{
	public abstract partial class FileFormat
	{
		internal abstract class CompressedFormat<T> : FileFormat
			where T : FileFormat, new()
		{
			private static T Serializer = new T();

			protected CompressedFormat()
			{
				this.SupportsEncryption = true;
			}

			private Data.Data DeSerializeInternal(Stream InStream)
			{
				return CompressedFormat<T>.Serializer.DeSerialize(new FileFormat.DeSerializeParam
				{
					Certificate = null,
					InStream = InStream
				});
			}

			internal protected override Data.Data DeSerialize(DeSerializeParam P)
			{
				P.InStream.Seek(this.Header.Length, SeekOrigin.Current);
				long RequiredLength = P.InStream.Position + 1;

				if (P.InStream.Length < RequiredLength)
					throw new Exceptions.StreamTooSmallException("Stream does not contain enough data.", "InStream");

				byte IsEncryptedByte = (byte)P.InStream.ReadByte();

				if (IsEncryptedByte == 0)
					using (GZipStream GZS = new GZipStream(P.InStream, CompressionMode.Decompress, true))
						return this.DeSerializeInternal(GZS);
				else if (P.Certificate == null)
					throw new ArgumentNullException("Certificate");
				else if (!P.Certificate.HasPrivateKey)
					throw new ArgumentException("Certificate does not contain a private key.", "Certificate");
				else
				{
					RequiredLength += 1 + sizeof(long);

					if (P.InStream.Length < RequiredLength)
						throw new Exceptions.StreamTooSmallException("Stream does not contain enough data.", "InStream");

					byte HeaderRandomByte = (byte)P.InStream.ReadByte();

					byte[] HeaderLengthBytes = new byte[sizeof(long)];
					P.InStream.Read(HeaderLengthBytes, 0, HeaderLengthBytes.Length);

					Array.Reverse(HeaderLengthBytes);

					long HeaderLength = BitConverter.ToInt64(HeaderLengthBytes, 0) / -IsEncryptedByte;
					RequiredLength += HeaderLength;

					if (P.InStream.Length < RequiredLength)
						throw new Exceptions.StreamTooSmallException("Stream does not contain enough data.", "InStream");

					byte[] EncryptedHeader = new byte[HeaderLength];
					P.InStream.Read(EncryptedHeader, 0, EncryptedHeader.Length);

					RSAOAEPKeyExchangeDeformatter KeyDeformatter = new RSAOAEPKeyExchangeDeformatter(P.Certificate.PrivateKey);

					byte[] Header = KeyDeformatter.DecryptKeyExchange(EncryptedHeader);

					if (/*Header.Length < 8
						|| */Header.Length < sizeof(int) + sizeof(uint) + sizeof(long) * 2 + sizeof(double) * 3 + 173)
						throw new Exceptions.StreamTooSmallException("Stream does not contain enough data.", "InStream");

					int HeaderOffset = 18 + HeaderRandomByte % 21;

					uint KeySize = (uint)-BitConverter.ToInt32(Header, HeaderOffset);
					HeaderOffset += sizeof(int) + 13 + HeaderRandomByte % 10;

					long IVLength = -BitConverter.ToInt64(Header, HeaderOffset);
					HeaderOffset += sizeof(long) + 25;

					uint BlockSize = BitConverter.ToUInt32(Header, HeaderOffset) / 3;
					HeaderOffset += sizeof(uint) + 45 + HeaderRandomByte % 20;

					byte[] EncryptionTypeBytes = new byte[sizeof(double)];

					for (int i = 0; i < EncryptionTypeBytes.Length; i++, HeaderOffset += 2)
						EncryptionTypeBytes[i] = Header[++HeaderOffset];

					HeaderOffset += 12;

					DataEncryptionType EncryptionType = (DataEncryptionType)(Math.Sqrt(Math.Sqrt(Math.Sqrt(BitConverter.ToDouble(EncryptionTypeBytes, 0)))) / 0xFA);

					long KeyLength = (long)Math.Sqrt(Math.Sqrt(Math.Sqrt(Math.Sqrt(BitConverter.ToDouble(Header, HeaderOffset))) / 0xDD));

					RequiredLength += IVLength + KeyLength;

					if (P.InStream.Length < RequiredLength)
						throw new Exceptions.StreamTooSmallException("Stream does not contain enough data.", "InStream");

					byte[] IVEncrypted = new byte[IVLength];
					byte[] KeyEncrypted = new byte[KeyLength];

					P.InStream.Read(IVEncrypted, 0, IVEncrypted.Length / 2);
					P.InStream.Read(KeyEncrypted, KeyEncrypted.Length / 2, KeyEncrypted.Length / 2);

					P.InStream.Read(IVEncrypted, IVEncrypted.Length / 2, IVEncrypted.Length / 2);
					P.InStream.Read(KeyEncrypted, 0, KeyEncrypted.Length / 2);

					Array.Reverse(KeyEncrypted);

					SymmetricAlgorithm EncryptionAlgorithm = this.GetEncryptionAlgorithm(EncryptionType);

					if (!EncryptionAlgorithm.ValidKeySize((int)KeySize))
					{
						EncryptionAlgorithm.Dispose();
						throw new Exceptions.InvalidKeySizeException(string.Format("Key size ({0}) is not valid for specified algorithm ({1}).", KeySize, EncryptionType));
					}

					if (!EncryptionAlgorithm.ValidBlockSize((int)BlockSize))
					{
						EncryptionAlgorithm.Dispose();
						throw new Exceptions.InvalidBlockSizeException(string.Format("Block size ({0}) is not valid for specified algorithm ({1}).", BlockSize, EncryptionType));
					}

					byte[] Key = KeyDeformatter.DecryptKeyExchange(KeyEncrypted);
					byte[] IV = KeyDeformatter.DecryptKeyExchange(IVEncrypted);

					EncryptionAlgorithm.BlockSize = (int)BlockSize;
					EncryptionAlgorithm.KeySize = (int)KeySize;
					EncryptionAlgorithm.Mode = CipherMode.CBC;
					EncryptionAlgorithm.Padding = PaddingMode.PKCS7;

					using (EncryptionAlgorithm)
					using (ICryptoTransform Decryptor = EncryptionAlgorithm.CreateDecryptor(Key, IV))
					using (CryptoStream CS = new CryptoStream(new NonClosingStream(P.InStream), Decryptor, CryptoStreamMode.Read))
					using (GZipStream GZS = new GZipStream(CS, CompressionMode.Decompress))
						return this.DeSerializeInternal(GZS);
				}
			}

			private void SerializeInternal(Data.Data DataToSerialize, Stream OutStream)
			{
				if (CompressedFormat<T>.Serializer.WriteHeaderBytes)
					OutStream.Write(CompressedFormat<T>.Serializer.Header, 0, CompressedFormat<T>.Serializer.Header.Length);

				CompressedFormat<T>.Serializer.Serialize(new FileFormat.SerializeParam
				{
					DataToSerialize = DataToSerialize,
					OutStream = OutStream,
					EncryptionType = DataEncryptionType.NoEncryption,
					Certificate = null,
					KeySize = 0,
					BlockSize = 0
				});
			}

			internal protected override void Serialize(SerializeParam P)
			{
				if (P.EncryptionType == DataEncryptionType.NoEncryption)
				{
					P.OutStream.WriteByte(0);

					using (GZipStream GZS = new GZipStream(P.OutStream, CompressionMode.Compress, true))
						this.SerializeInternal(P.DataToSerialize, GZS);
				}
				else
				{
					SymmetricAlgorithm EncryptionAlgorithm = this.GetEncryptionAlgorithm(P.EncryptionType);

					if (!EncryptionAlgorithm.ValidKeySize((int)P.KeySize))
					{
						EncryptionAlgorithm.Dispose();
						throw new Exceptions.InvalidKeySizeException(string.Format("Key size ({0}) is not valid for specified algorithm ({1}).", P.KeySize, P.EncryptionType));
					}

					if (!EncryptionAlgorithm.ValidBlockSize((int)P.BlockSize))
					{
						EncryptionAlgorithm.Dispose();
						throw new Exceptions.InvalidBlockSizeException(string.Format("Block size ({0}) is not valid for specified algorithm ({1}).", P.BlockSize, P.EncryptionType));
					}

					EncryptionAlgorithm.BlockSize = (int)P.BlockSize;
					EncryptionAlgorithm.KeySize = (int)P.KeySize;
					EncryptionAlgorithm.Mode = CipherMode.CBC;
					EncryptionAlgorithm.Padding = PaddingMode.PKCS7;

					byte[] RandomBytes = new byte[2];
					byte[] Header = new byte[sizeof(int) + sizeof(uint) + sizeof(long) * 2 + sizeof(double) * 3 + 173];

					using (RNGCryptoServiceProvider RNG = new RNGCryptoServiceProvider())
					{
						RNG.GetNonZeroBytes(RandomBytes);
						RNG.GetBytes(Header);
					}

					P.OutStream.Write(RandomBytes, 0, RandomBytes.Length);

					byte IsEncryptedByte = RandomBytes[0];
					byte HeaderRandomByte = RandomBytes[1];

					RSAOAEPKeyExchangeFormatter KeyFormatter = new RSAOAEPKeyExchangeFormatter(P.Certificate.PublicKey.Key);

					byte[] IVEncrypted = KeyFormatter.CreateKeyExchange(EncryptionAlgorithm.IV);

					byte[] KeyEncrypted = KeyFormatter.CreateKeyExchange(EncryptionAlgorithm.Key);
					Array.Reverse(KeyEncrypted);

					byte[] KeySizeBytes = BitConverter.GetBytes(-P.KeySize);
					byte[] BlockSizeBytes = BitConverter.GetBytes(P.BlockSize * 3);
					byte[] IVEncryptedLengthBytes = BitConverter.GetBytes(-IVEncrypted.LongLength);
					byte[] KeyEncryptedLengthBytes = BitConverter.GetBytes(Math.Pow(Math.Pow(KeyEncrypted.LongLength, 4) * 0xDD, 4));
					byte[] EncryptionTypeBytes = BitConverter.GetBytes(Math.Pow((int)P.EncryptionType * 0xFA, 8));

					int HeaderOffset = 18 + HeaderRandomByte % 21;

					KeySizeBytes.CopyTo(Header, HeaderOffset);
					HeaderOffset += KeySizeBytes.Length + 13 + HeaderRandomByte % 10;

					IVEncryptedLengthBytes.CopyTo(Header, HeaderOffset);
					HeaderOffset += IVEncryptedLengthBytes.Length + 25;

					BlockSizeBytes.CopyTo(Header, HeaderOffset);
					HeaderOffset += BlockSizeBytes.Length + 45 + HeaderRandomByte % 20;

					for (int i = 0; i < EncryptionTypeBytes.Length; i++, HeaderOffset += 2)
						Header[++HeaderOffset] = EncryptionTypeBytes[i];

					HeaderOffset += 12;

					KeyEncryptedLengthBytes.CopyTo(Header, HeaderOffset);

					byte[] HeaderEncrypted = KeyFormatter.CreateKeyExchange(Header);
					byte[] HeaderLengthBytes = BitConverter.GetBytes(HeaderEncrypted.LongLength * -IsEncryptedByte);

					Array.Reverse(HeaderLengthBytes);

					P.OutStream.Write(HeaderLengthBytes, 0, HeaderLengthBytes.Length);
					P.OutStream.Write(HeaderEncrypted, 0, HeaderEncrypted.Length);

					P.OutStream.Write(IVEncrypted, 0, IVEncrypted.Length / 2);
					P.OutStream.Write(KeyEncrypted, KeyEncrypted.Length / 2, KeyEncrypted.Length / 2);

					P.OutStream.Write(IVEncrypted, IVEncrypted.Length / 2, IVEncrypted.Length / 2);
					P.OutStream.Write(KeyEncrypted, 0, KeyEncrypted.Length / 2);

					P.OutStream.Flush();

					using (EncryptionAlgorithm)
					using (ICryptoTransform Encryptor = EncryptionAlgorithm.CreateEncryptor())
					using (CryptoStream CS = new CryptoStream(new NonClosingStream(P.OutStream), Encryptor, CryptoStreamMode.Write))
					using (GZipStream GZS = new GZipStream(CS, CompressionMode.Compress))
						this.SerializeInternal(P.DataToSerialize, GZS);
				}
			}

			internal protected override bool IsEncrypted(Stream InStream)
			{
				long RequiredLength = InStream.Position + 1;

				if (InStream.Length < RequiredLength)
					throw new Exceptions.StreamTooSmallException("Stream does not contain enough data.", "InStream");

				return InStream.ReadByte() > 0;
			}

			internal protected override bool IsValid(Stream InStream)
			{
				long RequiredLength = InStream.Position + 1;

				if (InStream.Length < RequiredLength)
					return false;

				byte IsEncryptedByte = (byte)InStream.ReadByte();

				if (IsEncryptedByte > 0)
				{
					RequiredLength += 1 + sizeof(long);

					if (InStream.Length < RequiredLength)
						return false;

					byte HeaderRandomByte = (byte)InStream.ReadByte();

					byte[] HeaderLengthBytes = new byte[sizeof(long)];
					InStream.Read(HeaderLengthBytes, 0, HeaderLengthBytes.Length);

					Array.Reverse(HeaderLengthBytes);

					long HeaderLength = BitConverter.ToInt64(HeaderLengthBytes, 0) / -IsEncryptedByte;
					RequiredLength += HeaderLength;

					if (InStream.Length < RequiredLength)
						return false;
				}

				return true;
			}
		}

		/// <summary>
		/// <see cref="Formats.CompressedBson"/>
		/// </summary>
		internal sealed class CompressedBson : CompressedFormat<Bson>
		{
			internal CompressedBson()
			{
				this.Name = "Compressed BSON";
				this.Fullname = "GZip Compressed Bin­ary JavaScript Object Notation";
				this.Identifier = new Guid("{8CD5FE4B-C201-460E-9DA3-7B886D71A054}");
			}
		}

		/// <summary>
		/// <see cref="Formats.CompressedJson"/>
		/// </summary>
		internal sealed class CompressedJson : CompressedFormat<Json>
		{
			internal CompressedJson()
			{
				this.Name = "Compressed JSON";
				this.Fullname = "GZip Compressed JavaScript Object Notation";
				this.Identifier = new Guid("{FACB73D2-92D3-4211-A29B-137C0E36149A}");
			}
		}

		/// <summary>
		/// <see cref="Formats.CompressedXML"/>
		/// </summary>
		internal sealed class CompressedXML : CompressedFormat<XML>
		{
			internal CompressedXML()
			{
				this.Name = "Compressed XML";
				this.Fullname = "GZip Compressed Extensible Markup Language";
				this.Identifier = new Guid("{3EDD37D0-E3E7-42C9-A093-689A54794F2F}");
			}
		}
	}
}