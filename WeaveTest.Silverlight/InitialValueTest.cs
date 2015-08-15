namespace WeaveTest.Silverlight
{
	public class InitialValueTest
	{
		[InitialValue(5)]
		public int MyIntProperty { get; set; }
		[InitialValue("the value")]
		public string MyStringProperty { get; set; }
	}
}
