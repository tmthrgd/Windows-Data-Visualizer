using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Serialization;

namespace Com.Xenthrax.WindowsDataVisualizer.Serializer
{
	public abstract partial class FileFormat
	{
		/// <summary>
		/// <see cref="Formats.Bson"/>
		/// </summary>
		internal sealed class Bson : FileFormat
		{
			static Bson()
			{
				Bson.Serializer.Converters.Add(new JsonGuidConverter());
				Bson.Serializer.Converters.Add(new JsonDirectoryInfoConverter());
				Bson.Serializer.Converters.Add(new JsonFileInfoConverter());

				Bson.Serializer.Error += (sender, e) =>
				{
					Utilities.Utilities.Log(e.ErrorContext.Error);
					e.ErrorContext.Handled = true;
				};
			}

			private static JsonSerializer Serializer = new JsonSerializer();

			public Bson()
			{
				this.Name = "BSON";
				this.Fullname = "Bin­ary JavaScript Object Notation";
				this.Identifier = new Guid("{3BCA6D24-3A5B-4BFD-9A4C-301EECB4CBDB}");
				this.WriteHeaderBytes = false;
			}

			internal protected override Data.Data DeSerialize(DeSerializeParam P)
			{
				return Bson.Serializer.Deserialize<Data.Data>(new BsonReader(P.InStream));
			}

			internal protected override void Serialize(SerializeParam P)
			{
				if (P.EncryptionType != DataEncryptionType.NoEncryption)
					throw new ArgumentException("Bson can only use NoEncryption.", "P.EncryptionType");

				BsonWriter writer = new BsonWriter(P.OutStream);
				Bson.Serializer.Serialize(writer, P.DataToSerialize);
				writer.Flush();
			}

			internal protected override bool IsEncrypted(Stream InStream)
			{
				return false;
			}

			internal protected override bool IsValid(Stream InStream)
			{
				return true;
			}

			internal protected override bool IsFormat(Stream InStream)
			{
				BsonReader reader = new BsonReader(InStream);
				return reader.Read()
					&& reader.TokenType == JsonToken.StartObject
					&& reader.Read()
					&& reader.TokenType == JsonToken.PropertyName
					&& (reader.Value as string) == "_"
					&& reader.Read()
					&& reader.TokenType == JsonToken.String
					&& string.Compare(this.Identifier.ToString(), reader.Value as string, true) == 0;
			}

			#region Json.NET
			private class JsonGuidConverter : JsonConverter
			{
				public override bool CanConvert(Type objectType)
				{
					return objectType == typeof(Guid);
				}

				public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
				{
					switch (reader.TokenType)
					{
						case JsonToken.Null:
							return Guid.Empty;
						case JsonToken.String:
							string str = reader.Value as string;
							return string.IsNullOrEmpty(str)
								? Guid.Empty
								: new Guid(str);
						default:
							throw new ArgumentException("Invalid token type");
					}
				}

				public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
				{
					writer.WriteValue(((Guid)value).ToString().ToUpper());
				}
			}

			private class JsonDirectoryInfoConverter : JsonConverter
			{
				public override bool CanConvert(Type objectType)
				{
					return objectType == typeof(DirectoryInfo);
				}

				public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
				{
					switch (reader.TokenType)
					{
						case JsonToken.Null:
							return null;
						case JsonToken.String:
							string str = reader.Value as string;
							return string.IsNullOrEmpty(str)
								? null
								: new DirectoryInfo(str);
						default:
							throw new ArgumentException("Invalid token type");
					}
				}

				public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
				{
					if (value == null)
						writer.WriteNull();
					else
						writer.WriteValue(((DirectoryInfo)value).FullName);
				}
			}

			private class JsonFileInfoConverter : JsonConverter
			{
				public override bool CanConvert(Type objectType)
				{
					return objectType == typeof(FileInfo);
				}

				public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
				{
					switch (reader.TokenType)
					{
						case JsonToken.Null:
							return null;
						case JsonToken.String:
							string str = reader.Value as string;
							return string.IsNullOrEmpty(str)
								? null
								: new FileInfo(str);
						default:
							throw new ArgumentException("Invalid token type");
					}
				}

				public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
				{
					if (value == null)
						writer.WriteNull();
					else
						writer.WriteValue(((FileInfo)value).FullName);
				}
			}
			#endregion
		}
	}
}