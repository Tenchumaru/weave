using System;

namespace WeaveTest.Silverlight
{
	[AttributeUsage(AttributeTargets.Class)]
	public class XmlSerializableAttribute : Attribute
	{
		// The NameStyle and NameCase perform the same modifications.
		public NameStyle NameStyle { get; set; }
		public string NameCase { get; set; }
		public Type SerializationStyle { get; set; } // Weave considers only either XmlAttributeAttribute or XmlElementAttribute.
		public XmlSerializableAttribute() { }
		public XmlSerializableAttribute(NameStyle nameStyle) { }
		public XmlSerializableAttribute(string nameCase) { }
		public XmlSerializableAttribute(Type xmlAttributeType) { }
		public XmlSerializableAttribute(NameStyle nameStyle, Type xmlAttributeType) { }
		public XmlSerializableAttribute(string nameCase, Type xmlAttributeType) { }
	}

	public enum NameStyle { None, CamelCase, PascalCase }
}
