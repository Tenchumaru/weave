using System.ComponentModel;

namespace WeaveTest.Silverlight
{
	public class CheckedViewModel : INotifyPropertyChanged
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
					var handler = PropertyChanged;
					if(handler != null)
						handler(this, new PropertyChangedEventArgs("FullString"));
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
					var handler = PropertyChanged;
					if(handler != null)
						handler(this, new PropertyChangedEventArgs("FullInt"));
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
					var handler = PropertyChanged;
					if(handler != null)
						handler(this, new PropertyChangedEventArgs("FullEquals"));
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
					var handler = PropertyChanged;
					if(handler != null)
						handler(this, new PropertyChangedEventArgs("FullEquatable"));
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
					var handler = PropertyChanged;
					if(handler != null)
						handler(this, new PropertyChangedEventArgs("FullOpEq"));
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
	}
}
