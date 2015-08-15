using System;

namespace WeaveTest.Silverlight
{
	[AttributeUsage(AttributeTargets.Property)]
	public class OnDemandAttribute : Attribute
	{
		public string ConstructorMethod { get; set; }
		public Type Type { get; set; }
	}
}
