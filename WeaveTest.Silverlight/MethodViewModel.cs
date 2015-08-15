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
using System.ComponentModel;

namespace WeaveTest.Silverlight
{
	[Notify("FirePropertyChanged")]
	public class MethodViewModel : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		public string FullString
		{
			get { return _FullString; }
			set
			{
				if(_FullString != value)
				{
					_FullString = value;
					FirePropertyChanged("FullString");
				}
			}
		}
		private string _FullString;

		public int FullInt
		{
			get { return _FullInt; }
			set
			{
				if(_FullInt != value)
				{
					_FullInt = value;
					FirePropertyChanged("FullInt");
				}
			}
		}
		private int _FullInt;

		public EqualsModel FullEquals
		{
			get { return _FullEquals; }
			set
			{
				if(_FullEquals != value)
				{
					_FullEquals = value;
					FirePropertyChanged("FullEquals");
				}
			}
		}
		private EqualsModel _FullEquals;

		public EquatableModel FullEquatable
		{
			get { return _FullEquatable; }
			set
			{
				if(_FullEquatable != value)
				{
					_FullEquatable = value;
					FirePropertyChanged("FullEquatable");
				}
			}
		}
		private EquatableModel _FullEquatable;

		public OpEqModel FullOpEq
		{
			get { return _FullOpEq; }
			set
			{
				if(_FullOpEq != value)
				{
					_FullOpEq = value;
					FirePropertyChanged("FullOpEq");
				}
			}
		}
		private OpEqModel _FullOpEq;

		[Notify]
		public string NotifiedString { get; set; }
		[Notify]
		public int NotifiedInt { get; set; }
		[Notify]
		public EqualsModel NotifiedEquals { get; set; }
		[Notify]
		public EquatableModel NotifiedEquatable { get; set; }
		[Notify]
		public OpEqModel NotifiedOpEq { get; set; }

		private void FirePropertyChanged(string propertyName)
		{
			var handler = PropertyChanged;
			if(handler != null)
				handler(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
