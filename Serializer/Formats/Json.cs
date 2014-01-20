using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Com.Xenthrax.WindowsDataVisualizer.Serializer
{
	public abstract partial class FileFormat
	{
		/// <summary>
		/// <see cref="Formats.Json"/>
		/// </summary>
		internal sealed class Json : FileFormat
		{
			static Json()
			{
				Json.Serializer.Converters.Add(new JsonGuidConverter());
				Json.Serializer.Converters.Add(new JsonDirectoryInfoConverter());
				Json.Serializer.Converters.Add(new JsonFileInfoConverter());
				Json.Serializer.Converters.Add(new JsonBinaryAscii85Converter());

				Json.Serializer.Error += (sender, e) =>
				{
					Utilities.Utilities.Log(e.ErrorContext.Error);
					e.ErrorContext.Handled = true;
				};
			}

			private static JsonSerializer Serializer = new JsonSerializer();

			public Json()
			{
				this.Name = "JSON";
				this.Fullname = "JavaScript Object Notation";
				this.Identifier = new Guid("{A91BC4A2-3D43-4666-B40E-6CF5375F969C}");
				this.WriteHeaderBytes = false;
			}

			internal protected override Data.Data DeSerialize(DeSerializeParam P)
			{
				return Json.Serializer.Deserialize<Data.Data>(new JsonTextReader(new StreamReader(P.InStream)));
			}

			internal protected override void Serialize(SerializeParam P)
			{
				if (P.EncryptionType != DataEncryptionType.NoEncryption)
					throw new ArgumentException("Json can only use NoEncryption.", "P.EncryptionType");

				JsonTextWriter writer = new JsonTextWriter(new StreamWriter(P.OutStream));
				writer.WriteComment(this.Identifier.ToString().ToUpper());

				if (Json.Serializer.Formatting == Formatting.Indented)
					writer.WriteWhitespace("\r\n");
				
				Json.Serializer.Serialize(writer, P.DataToSerialize);
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
				JsonTextReader reader = new JsonTextReader(new StreamReader(InStream));
				return reader.Read()
					&& reader.TokenType == JsonToken.Comment
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

			private class JsonBinaryAscii85Converter : JsonConverter
			{
				private static Utilities.Ascii85 Ascii85 = new Utilities.Ascii85()
				{
					EnforceMarks = true
				};

				public override bool CanConvert(Type objectType)
				{
					return objectType == typeof(byte[]);
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
								: JsonBinaryAscii85Converter.Ascii85.Decode(str);
						default:
							throw new ArgumentException("Invalid token type");
					}
				}

				public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
				{
					if (value == null)
						writer.WriteNull();
					else
						writer.WriteValue(JsonBinaryAscii85Converter.Ascii85.Encode((byte[])value));
				}
			}
			#endregion
		}
	}
}