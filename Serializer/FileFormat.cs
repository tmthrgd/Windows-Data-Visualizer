using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Com.Xenthrax.WindowsDataVisualizer.Serializer
{
	/// <summary>
	/// Represents a file format used by this program.
	/// </summary>
	public abstract partial class FileFormat
	{
		internal protected const int MinHeaderLength = 3;

		#region Constructors
		protected FileFormat()
		{
			this.WriteHeaderBytes = true;
			this.SupportsEncryption = false;
		}

		protected FileFormat(Guid Identifier)
		{
			this.Identifier = Identifier;
			this.WriteHeaderBytes = true;
			this.SupportsEncryption = false;
		}

		protected FileFormat(Guid Identifier, byte[] Header)
		{
			this.Header = (byte[])Header.Clone();
			this.Identifier = Identifier;
			this.WriteHeaderBytes = true;
			this.SupportsEncryption = false;
		}
		#endregion

		#region Members
		private Guid _Identifier;
		private byte[] _Header;
		private string _Name;
		private string _Fullname;
		#endregion

		#region Properties
		public Guid Identifier
		{
			get
			{
				if (this._Identifier == null || this._Identifier == default(Guid))
					throw new InvalidOperationException("Identifier has not been set.");

				return this._Identifier;
			}
			protected set
			{
				if (value == null || value == default(Guid))
					throw new ArgumentNullException("value");

				this._Identifier = value;

				if (this._Header == null)
					this.Header = value.ToByteArray();
			}
		}

		internal protected byte[] Header
		{
			get
			{
				if (this._Header == null)
					throw new InvalidOperationException("Header has not been set.");

				return this._Header;
			}
			protected set
			{
				if (value == null)
					throw new ArgumentNullException("value");

				if (value.Length < FileFormat.MinHeaderLength)
					throw new ArgumentException("value.Length cannot be less then MinHeaderLength.", "value");

				this._Header = value;
			}
		}

		public string Name
		{
			get
			{
				if (string.IsNullOrEmpty(this._Name))
					this.Name = this.GetType().Name;

				return this._Name;
			}
			protected set
			{
				if (string.IsNullOrWhiteSpace(value))
					throw new ArgumentNullException("value");

				this._Name = value;
			}
		}

		public string Fullname
		{
			get
			{
				if (string.IsNullOrEmpty(this._Fullname))
					this._Fullname = this.GetType().FullName;

				return this._Fullname;
			}
			protected set
			{
				if (string.IsNullOrWhiteSpace(value))
					throw new ArgumentNullException("value");

				this._Fullname = value;
			}
		}

		internal protected bool WriteHeaderBytes { get; protected set; }

		public bool SupportsEncryption { get; protected set; }
		#endregion

		#region Methods
		internal protected abstract Data.Data DeSerialize(DeSerializeParam Param);
		internal protected abstract void Serialize(SerializeParam Param);
		internal protected abstract bool IsEncrypted(Stream InStream);
		internal protected abstract bool IsValid(Stream InStream);
		
		internal protected virtual bool IsFormat(Stream InStream)
		{
			byte[] Header = new byte[this.Header.Length];
			int ReadCount = InStream.Read(Header, 0, Header.Length);

			if (ReadCount != Header.Length)
				return false;

			bool Success = true;

			for (int i = 0; i < Header.Length && Success; i++)
				if (this.Header[i] != Header[i])
					Success = false;

			return Success;
		}

		protected virtual SymmetricAlgorithm GetEncryptionAlgorithm(DataEncryptionType EncryptionType)
		{
			switch (EncryptionType)
			{
				case DataEncryptionType.AES:
					return new AesCryptoServiceProvider();
				case DataEncryptionType.NoEncryption:
					throw new NotSupportedException("Cannot return an encryption algorithm for NoEncryption.");
				case DataEncryptionType.TripleDES:
					return new TripleDESCryptoServiceProvider();
				default:
					throw new Exceptions.InvalidEncryptionException(string.Format("Invalid encryption algorithm {0}.", EncryptionType));
			}
		}
		#endregion

		#region Structures
		internal protected struct DeSerializeParam
		{
			public Stream InStream;
			public X509Certificate2 Certificate;
		}

		internal protected struct SerializeParam
		{
			public Data.Data DataToSerialize;
			public Stream OutStream;
			public DataEncryptionType EncryptionType;
			public X509Certificate2 Certificate;
			public int KeySize;
			public int BlockSize;
		}
		#endregion
	}
}