using System;

namespace WeaveTest.Silverlight
{
	[AttributeUsage(AttributeTargets.Property)]
	public class DependencyAttribute : Attribute
	{
		public object DefaultValue { get; set; }
		public string PropertyChangedCallback { get; set; }
	}
}
