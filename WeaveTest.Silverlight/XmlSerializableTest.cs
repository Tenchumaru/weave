using System;
using System.Xml.Serialization;

namespace WeaveTest.Silverlight
{
	[XmlSerializable(NameStyle.CamelCase)]
	public class XmlSerializableTest
	{
		public string Name { get; set; }
		public TimeSpan Duration { get; set; }
		public DateTime Holiday { get; set; }
		public Uri Location { get; set; }
		[XmlIgnore]
		public string NotSerialized { get; set; }
		[XmlIgnore]
		public Uri AlsoNotSerialized { get; set; }
		[XmlAttribute("Id")]
		public int Id { get; set; }
	}
}
