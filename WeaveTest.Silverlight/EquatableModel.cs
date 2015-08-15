using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace WeaveTest.Silverlight
{
	public class EquatableModel : IEquatable<EquatableModel>
	{
		public bool Equals(EquatableModel other)
		{
			return object.ReferenceEquals(this, other);
		}

		public override string ToString()
		{
			return GetHashCode().ToString();
		}
	}
}
