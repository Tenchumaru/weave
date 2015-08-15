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
	public class OpEqModel
	{
		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public override string ToString()
		{
			return GetHashCode().ToString();
		}

		public static bool operator ==(OpEqModel left, OpEqModel right)
		{
			return object.ReferenceEquals(left, null) ? object.ReferenceEquals(right, null) : left.Equals(right);
		}

		public static bool operator !=(OpEqModel left, OpEqModel right)
		{
			return !(left == right);
		}
	}
}
