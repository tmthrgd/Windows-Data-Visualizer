using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Com.Xenthrax.WindowsDataVisualizer.Serializer
{
	/// <summary>
	/// A class used to Serialize and Deserialize data returneused by this program.
	/// <remarks>Serialization should be handled by <see cref="DataStealer.Stealer"/>.</remarks>
	/// </summary>
	public static class DataSerializer
	{
		static DataSerializer()
		{
			DataSerializer.FileFormats = new System.Collections.ObjectModel.ObservableCollection<FileFormat>(
				typeof(Formats)
				.GetProperties(BindingFlags.Public | BindingFlags.Static)
				.Select(f => f.GetValue(null, null))
				.OfType<FileFormat>());
			DataSerializer._FileFormats = DataSerializer.FileFormats
				.ToDictionary(format => format.Identifier);
			DataSerializer._HeaderSortedFileFormats = DataSerializer._FileFormats
				.OrderByDescending(format => format.Value.Header.Length)
				.Select(format => format.Key)
				.ToList();
			DataSerializer._FileFormatNames = DataSerializer._FileFormats
				.ToDictionary(format => format.Value.Name, format => format.Value.Identifier, StringComparer.InvariantCultureIgnoreCase);
		}

		#region Static Members
		private static IDictionary<Guid, FileFormat> _FileFormats;
		private static List<Guid> _HeaderSortedFileFormats;
		private static IDictionary<string, Guid> _FileFormatNames;
		#endregion

		#region Static Properties
		/// <summary>
		/// 
		/// </summary>
		public static IEnumerable<FileFormat> FileFormats { get; private set; }

		/// <summary>
		/// The default file format.
		/// <see cref="_FileFormats.CompressedJson"/>
		/// </summary>
		public static FileFormat DefaultFormat
		{
			get
			{
				return Formats.CompressedJson;
			}
		}

		/// <summary>
		/// The default algorithm used if one is not specified.
		/// <see cref="DataEncryptionType.AES"/>
		/// </summary>
		public static DataEncryptionType DefaultEncryptionType
		{
			get
			{
				return DataEncryptionType.AES;
			}
		}
		#endregion

		#region Static Methods
		#region Deserialize
		/// <summary>
		/// Deserialize <paramref name="FS"/>.
		/// </summary>
		/// <param name="Data">The serialized data</param>
		/// <returns>Deserialized Data</returns>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.FileFormatNotSupportedException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.StreamTooSmallException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static Data.Data Deserialize(byte[] Data)
		{
			using (MemoryStream MS = new MemoryStream(Data))
				return DataSerializer.Deserialize(MS, null as X509Certificate2);
		}

		/// <summary>
		/// Deserialize <paramref name="InStream"/>.
		/// </summary>
		/// <param name="InStream">Input stream</param>
		/// <returns>Deserialized Data</returns>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.FileFormatNotSupportedException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.StreamTooSmallException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static Data.Data Deserialize(Stream InStream)
		{
			return DataSerializer.Deserialize(InStream, null as X509Certificate2);
		}

		/// <summary>
		/// Deserialize <paramref name="FS"/>.
		/// </summary>
		/// <param name="Data">The serialized data</param>
		/// <param name="CertificatePath">The path to the certificate containing private key corosponding to the public key used during Serialization.</param>
		/// <returns>Deserialized Data</returns>
		/// <remarks>Prompts the user to enter the certificate password if required.</remarks>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.Security.Cryptography.CryptographicException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.FileFormatNotSupportedException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.StreamTooSmallException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static Data.Data Deserialize(byte[] Data, string CertificatePath)
		{
			using (MemoryStream MS = new MemoryStream(Data))
				return DataSerializer.Deserialize(MS, CertificatePath);
		}

		/// <summary>
		/// Deserialize <paramref name="InStream"/>.
		/// </summary>
		/// <param name="InStream">Input stream</param>
		/// <param name="CertificatePath">The path to the certificate containing private key corosponding to the public key used during Serialization.</param>
		/// <returns>Deserialized Data</returns>
		/// <remarks>Prompts the user to enter the certificate password if required.</remarks>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.Security.Cryptography.CryptographicException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.FileFormatNotSupportedException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.StreamTooSmallException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static Data.Data Deserialize(Stream InStream, string CertificatePath)
		{
			X509Certificate2 Certificate = null;

			try
			{
				Certificate = new X509Certificate2(CertificatePath);
			}
			catch (CryptographicException e)
			{
				if (e.Message != "The specified network password is not correct.\r\n")
					throw;

				Certificate = new X509Certificate2(CertificatePath, Microsoft.VisualBasic.Interaction.InputBox("Please enter the password used when exporting the certificate:", "Certificate Password"));
			}

			return DataSerializer.Deserialize(InStream, Certificate);
		}

		/// <summary>
		/// Deserialize <paramref name="FS"/>.
		/// </summary>
		/// <param name="Data">The serialized data</param>
		/// <param name="CertificatePath">The path to the certificate containing private key corosponding to the public key used during Serialization.</param>
		/// <param name="Password">The password used when exporting the certificate (if applicable)</param>
		/// <returns>Deserialized Data</returns>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.Security.Cryptography.CryptographicException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.FileFormatNotSupportedException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.StreamTooSmallException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static Data.Data Deserialize(byte[] Data, string CertificatePath, string Password)
		{
			using (MemoryStream MS = new MemoryStream(Data))
				return DataSerializer.Deserialize(MS, new X509Certificate2(CertificatePath, Password));
		}

		/// <summary>
		/// Deserialize <paramref name="InStream"/>.
		/// </summary>
		/// <param name="InStream">Input stream</param>
		/// <param name="CertificatePath">The path to the certificate containing private key corosponding to the public key used during Serialization.</param>
		/// <param name="Password">The password used when exporting the certificate (if applicable)</param>
		/// <returns>Deserialized Data</returns>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.Security.Cryptography.CryptographicException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.FileFormatNotSupportedException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.StreamTooSmallException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static Data.Data Deserialize(Stream InStream, string CertificatePath, string Password)
		{
			return DataSerializer.Deserialize(InStream, new X509Certificate2(CertificatePath, Password));
		}

		/// <summary>
		/// Deserialize <paramref name="FS"/>.
		/// </summary>
		/// <param name="Data">The serialized data</param>
		/// <param name="Certificate">The certificate containing private key corosponding to the public key used during Serialization.</param>
		/// <returns>Deserialized Data</returns>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.FileFormatNotSupportedException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.StreamTooSmallException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static Data.Data Deserialize(byte[] Data, X509Certificate2 Certificate)
		{
			using (MemoryStream MS = new MemoryStream(Data))
				return DataSerializer.Deserialize(MS, Certificate);
		}

		/// <summary>
		/// Deserialize <paramref name="InStream"/>.
		/// </summary>
		/// <param name="InStream">Input stream</param>
		/// <param name="Certificate">The certificate containing private key corosponding to the public key used during Serialization.</param>
		/// <returns>Deserialized Data</returns>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.FileFormatNotSupportedException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.StreamTooSmallException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static Data.Data Deserialize(Stream InStream, X509Certificate2 Certificate)
		{
			if (InStream == null)
				throw new ArgumentNullException("InStream");

			if (!InStream.CanRead)
				throw new ArgumentException("Stream cannot be read.", "InStream");

			InStream = Stream.Synchronized(InStream);
			long Position = InStream.Position;

			try
			{
				FileFormat Format = DataSerializer.GetFileFormat(InStream);

				if (Format == null)
					throw new Exceptions.FileFormatNotSupportedException("The format of this file is not supported.");

				Data.Data Data = Format.DeSerialize(new FileFormat.DeSerializeParam
				{
					Certificate = Certificate,
					InStream = InStream
				});

				if (Data == null)
					throw new InvalidDataException("Data == null");

				if (Data.SerializerType != Format.Identifier)
					Utilities.Utilities.Log(new InvalidDataException("Data.SerializedType != Format.FormatIdentifier"));

				return Data;
			}
			catch
			{
				InStream.Seek(Position, SeekOrigin.Begin);
				throw;
			}
		}

		/// <summary>
		/// Deserialize <paramref name="InStream"/>.
		/// </summary>
		/// <param name="InStream">Input stream</param>
		/// <param name="Certificate">The certificate containing private key corosponding to the public key used during Serialization.</param>
		/// <returns>Deserialized Data</returns>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.FileFormatNotSupportedException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.StreamTooSmallException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static Data.Data Deserialize(Stream InStream, X509Certificate2 Certificate, FileFormat Format)
		{
			if (InStream == null)
				throw new ArgumentNullException("InStream");

			if (!InStream.CanRead)
				throw new ArgumentException("Stream cannot be read.", "InStream");

			if (Format == null)
				throw new ArgumentNullException("Format");

			InStream = Stream.Synchronized(InStream);
			long Position = InStream.Position;

			try
			{
				Data.Data Data = Format.DeSerialize(new FileFormat.DeSerializeParam
				{
					Certificate = Certificate,
					InStream = InStream
				});

				if (Data == null)
					throw new InvalidDataException("Data == null");

				if (Data.SerializerType != Format.Identifier)
					Utilities.Utilities.Log(new InvalidDataException("Data.SerializedType != Format.FormatIdentifier"));

				return Data;
			}
			catch
			{
				InStream.Seek(Position, SeekOrigin.Begin);
				throw;
			}
		}
		#endregion

		#region Serialize
		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> without any encryption and returns the result.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <returns>Serialized data</returns>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static byte[] Serialize(Data.Data DataToSerialize)
		{
			using (MemoryStream MS = new MemoryStream())
			{
				DataSerializer.Serialize(DataToSerialize, MS, DataEncryptionType.NoEncryption, null as X509Certificate2, 0, 0, DataSerializer.DefaultFormat);
				return MS.ToArray();
			}
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> to <paramref name="OutStream"/> without any encryption.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="OutStream">Output stream</param>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static void Serialize(Data.Data DataToSerialize, Stream OutStream)
		{
			DataSerializer.Serialize(DataToSerialize, OutStream, DataEncryptionType.NoEncryption, null as X509Certificate2, 0, 0, DataSerializer.DefaultFormat);
		}

		/// <summary>
		/// Serializes <paramref name="DataToSerialize"/> using <see cref="DefaultEncryptionType"/> with the strongest block and key size, and then returns the result.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="CertificatePath">The path to the certificate to use</param>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.Security.Cryptography.CryptographicException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static byte[] Serialize(Data.Data DataToSerialize, string CertificatePath)
		{
			using (MemoryStream MS = new MemoryStream())
			{
				DataSerializer.Serialize(DataToSerialize, MS, DataSerializer.DefaultEncryptionType, new X509Certificate2(CertificatePath), DataSerializer.DefaultFormat);
				return MS.ToArray();
			}
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> to <paramref name="OutStream"/> using <see cref="DefaultEncryptionType"/> with the strongest block and key size.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="OutStream">Output stream</param>
		/// <param name="CertificatePath">The path to the certificate to use</param>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.Security.Cryptography.CryptographicException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static void Serialize(Data.Data DataToSerialize, Stream OutStream, string CertificatePath)
		{
			DataSerializer.Serialize(DataToSerialize, OutStream, DataSerializer.DefaultEncryptionType, new X509Certificate2(CertificatePath), DataSerializer.DefaultFormat);
		}

		/// <summary>
		/// Serializes <paramref name="DataToSerialize"/> using <see cref="DefaultEncryptionType"/> with the strongest block and key size, and then returns the result.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="Certificate">The path to the certificate to use</param>
		/// <returns>Serialized data</returns>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static byte[] Serialize(Data.Data DataToSerialize, X509Certificate2 Certificate)
		{
			using (MemoryStream MS = new MemoryStream())
			{
				DataSerializer.Serialize(DataToSerialize, MS, DataSerializer.DefaultEncryptionType, Certificate, DataSerializer.DefaultFormat);
				return MS.ToArray();
			}
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> to <paramref name="OutStream"/> using using <see cref="DefaultEncryptionType"/> with the strongest block and key size.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="OutStream">Output stream</param>
		/// <param name="Certificate">The path to the certificate to use</param>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static void Serialize(Data.Data DataToSerialize, Stream OutStream, X509Certificate2 Certificate)
		{
			DataSerializer.Serialize(DataToSerialize, OutStream, DataSerializer.DefaultEncryptionType, Certificate, DataSerializer.DefaultFormat);
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> using the specified encryption and returns the result.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="OutStream">Output stream</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <remarks><paramref name="EncryptionType"/> MUST be NoEncryption.</remarks>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static byte[] Serialize(Data.Data DataToSerialize, DataEncryptionType EncryptionType)
		{
			using (MemoryStream MS = new MemoryStream())
			{
				DataSerializer.Serialize(DataToSerialize, MS, EncryptionType);
				return MS.ToArray();
			}
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> to <paramref name="OutStream"/> using the specified encryption.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="OutStream">Output stream</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <remarks><paramref name="EncryptionType"/> MUST be NoEncryption.</remarks>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static void Serialize(Data.Data DataToSerialize, Stream OutStream, DataEncryptionType EncryptionType)
		{
			if (EncryptionType == DataEncryptionType.NoEncryption)
				DataSerializer.Serialize(DataToSerialize, OutStream, EncryptionType, null as X509Certificate2, 0, 0, DataSerializer.DefaultFormat);
			else
				throw new ArgumentException("This method is only valid for NoEncryption.", "EncryptionType");
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> using the specified encryption with the strongest block and size, and then returns the result.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <param name="CertificatePath">The path to the certificate to use</param>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.Security.Cryptography.CryptographicException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static byte[] Serialize(Data.Data DataToSerialize, DataEncryptionType EncryptionType, string CertificatePath)
		{
			using (MemoryStream MS = new MemoryStream())
			{
				DataSerializer.Serialize(DataToSerialize, MS, EncryptionType, new X509Certificate2(CertificatePath));
				return MS.ToArray();
			}
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> to <paramref name="OutStream"/> using the specified encryption with the strongest block and key size.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="OutStream">Output stream</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <param name="CertificatePath">The path to the certificate to use</param>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.Security.Cryptography.CryptographicException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static void Serialize(Data.Data DataToSerialize, Stream OutStream, DataEncryptionType EncryptionType, string CertificatePath)
		{
			DataSerializer.Serialize(DataToSerialize, OutStream, EncryptionType, new X509Certificate2(CertificatePath));
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> to <paramref name="OutStream"/> using the specified encryption wih the strongest block and key size.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="OutStream">Output stream</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <param name="Certificate">The path to the certificate to use</param>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static byte[] Serialize(Data.Data DataToSerialize, DataEncryptionType EncryptionType, X509Certificate2 Certificate)
		{
			using (MemoryStream MS = new MemoryStream())
			{
				DataSerializer.Serialize(DataToSerialize, MS, EncryptionType, Certificate, DataSerializer.DefaultFormat);
				return MS.ToArray();
			}
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> to <paramref name="OutStream"/> using the specified encryption with the strongest block and key size.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="OutStream">Output stream</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <param name="Certificate">The path to the certificate to use</param>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static void Serialize(Data.Data DataToSerialize, Stream OutStream, DataEncryptionType EncryptionType, X509Certificate2 Certificate)
		{
			DataSerializer.Serialize(DataToSerialize, OutStream, EncryptionType, Certificate, DataSerializer.DefaultFormat);
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> using the specified encryption and returns the result.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <param name="CertificatePath">The path to the certificate to use</param>
		/// <param name="KeySize">Key size</param>
		/// <param name="BlockSize">Block size</param>
		/// <returns>Serialized data</returns>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.Security.Cryptography.CryptographicException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static byte[] Serialize(Data.Data DataToSerialize, DataEncryptionType EncryptionType, string CertificatePath, int KeySize, int BlockSize)
		{
			using (MemoryStream MS = new MemoryStream())
			{
				DataSerializer.Serialize(DataToSerialize, MS, EncryptionType, new X509Certificate2(CertificatePath), KeySize, BlockSize, DataSerializer.DefaultFormat);
				return MS.ToArray();
			}
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> to <paramref name="OutStream"/> using the specified encryption.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="OutStream">Output stream</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <param name="CertificatePath">The path to the certificate to use</param>
		/// <param name="KeySize">Key size</param>
		/// <param name="BlockSize">Block size</param>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.Security.Cryptography.CryptographicException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static void Serialize(Data.Data DataToSerialize, Stream OutStream, DataEncryptionType EncryptionType, string CertificatePath, int KeySize, int BlockSize)
		{
			DataSerializer.Serialize(DataToSerialize, OutStream, EncryptionType, new X509Certificate2(CertificatePath), KeySize, BlockSize, DataSerializer.DefaultFormat);
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> using the specified encryption and returns the result.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <param name="Certificate">The path to the certificate to use</param>
		/// <param name="KeySize">Key size</param>
		/// <param name="BlockSize">Block size</param>
		/// <returns>Serialized data</returns>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static byte[] Serialize(Data.Data DataToSerialize, DataEncryptionType EncryptionType, X509Certificate2 Certificate, int KeySize, int BlockSize)
		{
			using (MemoryStream MS = new MemoryStream())
			{
				DataSerializer.Serialize(DataToSerialize, MS, EncryptionType, Certificate, KeySize, BlockSize, DataSerializer.DefaultFormat);
				return MS.ToArray();
			}
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> to <paramref name="OutStream"/> using the specified encryption.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="OutStream">Output stream</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <param name="Certificate">The path to the certificate to use</param>
		/// <param name="KeySize">Key size</param>
		/// <param name="BlockSize">Block size</param>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static void Serialize(Data.Data DataToSerialize, Stream OutStream, DataEncryptionType EncryptionType, X509Certificate2 Certificate, int KeySize, int BlockSize)
		{
			DataSerializer.Serialize(DataToSerialize, OutStream, EncryptionType, Certificate, KeySize, BlockSize, DataSerializer.DefaultFormat);
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> using the specified encryption and return the result.
		/// And the specifed file format.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <param name="CertificatePath">The path to the certificate to use</param>
		/// <param name="KeySize">Key size</param>
		/// <param name="BlockSize">Block size</param>
		/// <param name="Format">File format</param>
		/// <returns>Serialized data</returns>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.Security.Cryptography.CryptographicException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static byte[] Serialize(Data.Data DataToSerialize, DataEncryptionType EncryptionType, string CertificatePath, int KeySize, int BlockSize, FileFormat Format)
		{
			using (MemoryStream MS = new MemoryStream())
			{
				DataSerializer.Serialize(DataToSerialize, MS, EncryptionType, new X509Certificate2(CertificatePath), KeySize, BlockSize, Format);
				return MS.ToArray();
			}
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> to <paramref name="OutStream"/> using the specified encryption.
		/// And the specifed file format.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="OutStream">Output stream</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <param name="CertificatePath">The path to the certificate to use</param>
		/// <param name="KeySize">Key size</param>
		/// <param name="BlockSize">Block size</param>
		/// <param name="Format">File format</param>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.Security.Cryptography.CryptographicException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static void Serialize(Data.Data DataToSerialize, Stream OutStream, DataEncryptionType EncryptionType, string CertificatePath, int KeySize, int BlockSize, FileFormat Format)
		{
			DataSerializer.Serialize(DataToSerialize, OutStream, EncryptionType, new X509Certificate2(CertificatePath), KeySize, BlockSize, Format);
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> using the specified encryption and return the result.
		/// And the specifed file format.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <param name="Certificate">The path to the certificate to use</param>
		/// <param name="KeySize">Key size</param>
		/// <param name="BlockSize">Block size</param>
		/// <param name="Format">File format</param>
		/// <returns>Serialized data</returns>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static byte[] Serialize(Data.Data DataToSerialize, DataEncryptionType EncryptionType, X509Certificate2 Certificate, int KeySize, int BlockSize, FileFormat Format)
		{
			using (MemoryStream MS = new MemoryStream())
			{
				DataSerializer.Serialize(DataToSerialize, MS, EncryptionType, Certificate, KeySize, BlockSize, Format);
				return MS.ToArray();
			}
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> to <paramref name="OutStream"/> using the specified encryption with the strongest block and key size.
		/// And the specifed file format.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="OutStream">Output stream</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <param name="Certificate">The path to the certificate to use</param>
		/// <param name="KeySize">Key size</param>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static void Serialize(Data.Data DataToSerialize, Stream OutStream, DataEncryptionType EncryptionType, X509Certificate2 Certificate, FileFormat Format)
		{
			DataSerializer.Serialize(DataToSerialize, OutStream, EncryptionType, Certificate, DataSerializer.GetStrongestKeySize(EncryptionType), DataSerializer.GetStrongestBlockSize(EncryptionType), Format);
		}

		/// <summary>
		/// Serialize <paramref name="DataToSerialize"/> to <paramref name="OutStream"/> using the specified encryption.
		/// And the specifed file format.
		/// </summary>
		/// <param name="DataToSerialize">Data</param>
		/// <param name="OutStream">Output stream</param>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <param name="Certificate">The path to the certificate to use</param>
		/// <param name="KeySize">Key size</param>
		/// <param name="BlockSize">Block size</param>
		/// <param name="Format">File format</param>
		/// <exception cref="System.ArgumentNullException"/>
		/// <exception cref="System.ArgumentException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidEncryptionException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidBlockSizeException"/>
		/// <exception cref="Com.Xenthrax.WindowsDataVisualizer.Exceptions.InvalidKeySizeException"/>
		public static void Serialize(Data.Data DataToSerialize, Stream OutStream, DataEncryptionType EncryptionType, X509Certificate2 Certificate, int KeySize, int BlockSize, FileFormat Format)
		{
			if (DataToSerialize == null)
				throw new ArgumentNullException("DataToSerialize");

			if (OutStream == null)
				throw new ArgumentNullException("OutStream");

			if (EncryptionType != DataEncryptionType.NoEncryption && Certificate == null)
				throw new ArgumentNullException("Certificate");

			if (Format == null)
				throw new ArgumentNullException("Format");

			if (!DataSerializer._FileFormats.ContainsKey(Format.Identifier))
				throw new ArgumentException("Invalid file format.", "Format");

			if (!OutStream.CanWrite)
				throw new ArgumentException("Stream cannot be written to.", "OutStream");

			OutStream = Stream.Synchronized(OutStream);

			long Position = OutStream.Position;
			OutStream.SetLength(OutStream.Position);

			OutStream.Flush();

			try
			{
				DataToSerialize.SerializerType = Format.Identifier;
 
				if (Format.WriteHeaderBytes)
					OutStream.Write(Format.Header, 0, Format.Header.Length);

				Format.Serialize(new FileFormat.SerializeParam
				{
					DataToSerialize = DataToSerialize,
					OutStream = OutStream,
					EncryptionType = EncryptionType,
					Certificate = Certificate,
					KeySize = KeySize,
					BlockSize = BlockSize
				});
				OutStream.Flush();
			}
			catch
			{
				OutStream.Seek(Position, SeekOrigin.Begin);
				OutStream.SetLength(Position);
				OutStream.Flush();
				throw;
			}
		}
		#endregion

		#region IsValid
		/// <summary>
		/// 
		/// </summary>
		/// <param name="Data"></param>
		/// <returns></returns>
		public static bool IsValid(byte[] Data)
		{
			using (MemoryStream MS = new MemoryStream(Data))
				return DataSerializer.IsValid(MS);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="InStream"></param>
		/// <returns></returns>
		public static bool IsValid(Stream InStream)
		{
			if (InStream == null)
				throw new ArgumentNullException("InStream");

			if (!InStream.CanRead)
				throw new ArgumentException("Stream cannot be read.", "InStream");

			InStream = Stream.Synchronized(InStream);
			long Position = InStream.Position;

			try
			{
				FileFormat Format = DataSerializer.GetFileFormat(InStream);
				return Format != null
					&& Format.IsValid(InStream);
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return false;
			}
			finally
			{
				if (InStream.CanSeek)
					InStream.Seek(Position, SeekOrigin.Begin);
			}
		}
		#endregion

		#region EncryptionType
		/// <summary>
		/// Returns true if the data contained with <paramref name="InStream"/> is encrypted.
		/// </summary>
		/// <param name="Data">The serialized data</param>
		/// <returns>Whether or not the data is encrypted.</returns>
		/// <exception cref="Exceptions.FileFormatNotSupportedException"/>
		/// <exception cref="Exceptions.StreamTooSmallException"/>
		public static bool IsEncrypted(byte[] Data)
		{
			using (MemoryStream MS = new MemoryStream(Data))
				return DataSerializer.IsEncrypted(MS);
		}

		/// <summary>
		/// Returns true if the data contained with <paramref name="InStream"/> is encrypted.
		/// </summary>
		/// <param name="InStream">Input stream</param>
		/// <returns>Whether or not the data is encrypted.</returns>
		/// <exception cref="Exceptions.FileFormatNotSupportedException"/>
		/// <exception cref="Exceptions.StreamTooSmallException"/>
		public static bool IsEncrypted(Stream InStream)
		{
			if (InStream == null)
				throw new ArgumentNullException("InStream");

			if (!InStream.CanRead)
				throw new ArgumentException("Stream cannot be read.", "InStream");

			InStream = Stream.Synchronized(InStream);
			long Position = InStream.Position;

			try
			{
				FileFormat Format = DataSerializer.GetFileFormat(InStream);

				if (Format == null)
					throw new Exceptions.FileFormatNotSupportedException("The format of this file is not supported.");

				return Format.IsEncrypted(InStream);
			}
			finally
			{
				if (InStream.CanSeek)
					InStream.Seek(Position, SeekOrigin.Begin);
			}
		}
		#endregion

		/// <summary>
		/// Determines the strongest avalible block size for the specified algorithm.
		/// </summary>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <returns>The strongest avalible block size.</returns>
		public static int GetStrongestBlockSize(DataEncryptionType EncryptionType)
		{
			switch (EncryptionType)
			{
				case DataEncryptionType.AES:
					return 128;
				case DataEncryptionType.NoEncryption:
					return 0;
				case DataEncryptionType.TripleDES:
					return 64;
				default:
					return 0;
					//throw new Exceptions.InvalidEncryptionException(string.Format("{0} is not a valid encryption type.", EncryptionType));
			}
		}

		/// <summary>
		/// Determines the strongest avalible key size for the specified algorithm.
		/// </summary>
		/// <param name="EncryptionType">The type of encryption</param>
		/// <returns>The strongest avalible key size.</returns>
		public static int GetStrongestKeySize(DataEncryptionType EncryptionType)
		{
			switch (EncryptionType)
			{
				case DataEncryptionType.AES:
					return 256;
				case DataEncryptionType.NoEncryption:
					return 0;
				case DataEncryptionType.TripleDES:
					return 192;
				default:
					return 0;
					//throw new Exceptions.InvalidEncryptionException(string.Format("{0} is not a valid encryption type.", EncryptionType));
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Format"></param>
		/// <returns></returns>
		public static bool AddFormat(FileFormat Format)
		{
			try
			{
				if (Format == null
					|| DataSerializer._FileFormats.ContainsKey(Format.Identifier)
					|| DataSerializer._FileFormatNames.ContainsKey(Format.Name))
					return false;
				
				int idx = DataSerializer._HeaderSortedFileFormats
					.FindIndex(id => DataSerializer._FileFormats[id].Header.Length < Format.Header.Length);
				
				if (idx == -1)
					idx = DataSerializer._HeaderSortedFileFormats.Count;

				((ICollection<FileFormat>)DataSerializer.FileFormats).Add(Format);
				DataSerializer._FileFormats.Add(Format.Identifier, Format);
				DataSerializer._FileFormatNames.Add(Format.Name, Format.Identifier);
				DataSerializer._HeaderSortedFileFormats.Insert(idx, Format.Identifier);
				return true;
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return false;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Format"></param>
		/// <returns></returns>
		public static bool RemoveFormat(FileFormat Format)
		{
			try
			{
				return Format != null
					&& DataSerializer._FileFormats.ContainsKey(Format.Identifier)
					&& DataSerializer._HeaderSortedFileFormats.Remove(Format.Identifier)
					&& DataSerializer._FileFormatNames.Remove(Format.Name)
					&& DataSerializer._FileFormats.Remove(Format.Identifier)
					&& ((ICollection<FileFormat>)DataSerializer.FileFormats).Remove(Format);
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return false;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Identifier"></param>
		/// <returns></returns>
		public static FileFormat GetFileFormatFromIdentifier(Guid Identifier)
		{
			FileFormat Format;

			if (DataSerializer._FileFormats.TryGetValue(Identifier, out Format))
				return Format;
			else
				throw new Exceptions.FileFormatNotSupportedException("The format specified is not supported.");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Name"></param>
		/// <returns></returns>
		public static FileFormat GetFileFormatFromName(string Name)
		{
			Guid Id;

			if (DataSerializer._FileFormatNames.TryGetValue(Name, out Id))
				return DataSerializer._FileFormats[Id];
			else
				throw new Exceptions.FileFormatNotSupportedException("The format specified is not supported.");
		}

		private static FileFormat GetFileFormat(Stream InStream)
		{
			if (DataSerializer._HeaderSortedFileFormats.Count == 0)
				return null;

			long Position = InStream.Position;

			foreach (Guid Id in DataSerializer._HeaderSortedFileFormats)
			{
				try
				{
					FileFormat Format = DataSerializer._FileFormats[Id];

					if (Format.IsFormat(InStream))
						return Format;
				}
				catch (Exception e)
				{
					Utilities.Utilities.Log(e);
				}
				finally
				{
					InStream.Seek(Position, SeekOrigin.Begin);
				}
			}

			return null;
		}
		#endregion
	}

	/// <summary>
	/// The possible encryption algorithms.
	/// </summary>
	public enum DataEncryptionType
	{
		/// <summary>
		/// Specifies that the file should not be encrypted.
		/// </summary>
		NoEncryption = 0,

		/// <summary>
		/// Specifies that the file should be encrypted using AesCryptoServiceProvider.
		/// <see cref="http://msdn.microsoft.com/en-us/library/system.security.cryptography.aescryptoserviceprovider.aspx"/>
		/// </summary>
		AES = 1,

		/// <summary>
		/// Specifies that the file should be encrypted using TripleDESCryptoServiceProvider.
		/// <see cref="http://msdn.microsoft.com/en-us/library/system.security.cryptography.tripledescryptoserviceprovider.aspx"/>
		/// </summary>
		TripleDES = 2
	}

	/// <summary>
	/// File Formats
	/// </summary>
	public static class Formats
	{
		static Formats()
		{
			Formats.Bson = new FileFormat.Bson();
			Formats.Json = new FileFormat.Json();
			Formats.XML  = new FileFormat.XML();

			Formats.CompressedBson = new FileFormat.CompressedBson();
			Formats.CompressedJson = new FileFormat.CompressedJson();
			Formats.CompressedXML  = new FileFormat.CompressedXML();
		}

		/// <summary>
		/// BSON (Bin­ary JavaScript Object Notation)
		/// </summary>
		public static FileFormat Bson { get; private set; }

		/// <summary>
		/// JSON (JavaScript Object Notation)
		/// </summary>
		public static FileFormat Json { get; private set; }

		/// <summary>
		/// XML (Extensible Markup Language)
		/// </summary>
		public static FileFormat XML { get; private set; }


		/// <summary>
		/// Compressed BSON (GZip Compressed Bin­ary JavaScript Object Notation)
		/// </summary>
		public static FileFormat CompressedBson { get; private set; }

		/// <summary>
		/// Compressed JSON (GZip Compressed JavaScript Object Notation)
		/// </summary>
		public static FileFormat CompressedJson { get; private set; }

		/// <summary>
		/// Compressed XML (GZip Compressed Extensible Markup Language)
		/// </summary>
		public static FileFormat CompressedXML { get; private set; }
	}
}