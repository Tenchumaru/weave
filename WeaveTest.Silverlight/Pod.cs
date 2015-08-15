using System.Diagnostics;

namespace WeaveTest.Silverlight
{
	public class Pod
	{
		public string DataString { get; set; }
		public int DataInt { get; set; }
		public EqualsModel DataEquals { get; set; }
		public EquatableModel DataEquatable { get; set; }
		public OpEqModel DataOpEq { get; set; }

		public Pod()
		{
			Debug.WriteLine("constructing Pod " + GetHashCode());
		}
	}
}
