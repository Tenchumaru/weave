using System;

namespace WeaveTest.Silverlight
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
	public class NotifyAttribute : Attribute
	{
		public NotifyAttribute() { }
		public NotifyAttribute(bool included) { }
		public NotifyAttribute(string methodName) { }
	}
}
