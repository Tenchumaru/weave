using System;

namespace WeaveTest.Silverlight
{
	[AttributeUsage(AttributeTargets.Property)]
	public class InitialValueAttribute : Attribute
	{
		// Create as many constructors as needed.
		public InitialValueAttribute(int initialValue) { }
		public InitialValueAttribute(string initialValue) { }
	}
}
