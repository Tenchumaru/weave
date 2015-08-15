using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace WeaveTest.Silverlight
{
	public partial class MainPage : UserControl
	{
		public MainPage()
		{
			InitializeComponent();
			DataContext = new Container
			{
				Checked = new CheckedViewModel
				{
					FullEquals = new EqualsModel(),
					FullEquatable = new EquatableModel(),
					FullInt = 111,
					FullOpEq = new OpEqModel(),
					FullString = "Original Checked Full",
					NotifiedEquals = new EqualsModel(),
					NotifiedEquatable = new EquatableModel(),
					NotifiedInt = 121,
					NotifiedOpEq = new OpEqModel(),
					NotifiedString = "Original Checked Notified",
				},
				Initialized = new InitializedViewModel
				{
					FullEquals = new EqualsModel(),
					FullEquatable = new EquatableModel(),
					FullInt = 211,
					FullOpEq = new OpEqModel(),
					FullString = "Original Initialized Full",
					NotifiedEquals = new EqualsModel(),
					NotifiedEquatable = new EquatableModel(),
					NotifiedInt = 221,
					NotifiedOpEq = new OpEqModel(),
					NotifiedString = "Original Initialized Notified",
				},
				Method = new MethodViewModel
				{
					FullEquals = new EqualsModel(),
					FullEquatable = new EquatableModel(),
					FullInt = 311,
					FullOpEq = new OpEqModel(),
					FullString = "Original Method Full",
					NotifiedEquals = new EqualsModel(),
					NotifiedEquatable = new EquatableModel(),
					NotifiedInt = 321,
					NotifiedOpEq = new OpEqModel(),
					NotifiedString = "Original Method Notified",
				},
				Dependency = new DependencyTest
				{
					AutoHandledInt = 4,
					AutoInt = 2,
					ManualHandledInt = 14,
					ManualInt = 12,
				},
				OnDemand = new OnDemandTest(),
			};
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			var container = (Container)DataContext;
			container.Checked.FullEquals = new EqualsModel();
			container.Checked.FullEquatable = new EquatableModel();
			++container.Checked.FullInt;
			container.Checked.FullOpEq = new OpEqModel();
			container.Checked.FullString = "Modified Checked Full";
			container.Checked.NotifiedEquals = new EqualsModel();
			container.Checked.NotifiedEquatable = new EquatableModel();
			++container.Checked.NotifiedInt;
			container.Checked.NotifiedOpEq = new OpEqModel();
			container.Checked.NotifiedString = "Modified Checked Notified";
			container.Initialized.FullEquals = new EqualsModel();
			container.Initialized.FullEquatable = new EquatableModel();
			++container.Initialized.FullInt;
			container.Initialized.FullOpEq = new OpEqModel();
			container.Initialized.FullString = "Modified Initialized Full";
			container.Initialized.NotifiedEquals = new EqualsModel();
			container.Initialized.NotifiedEquatable = new EquatableModel();
			++container.Initialized.NotifiedInt;
			container.Initialized.NotifiedOpEq = new OpEqModel();
			container.Initialized.NotifiedString = "Modified Initialized Notified";
			container.Method.FullEquals = new EqualsModel();
			container.Method.FullEquatable = new EquatableModel();
			++container.Method.FullInt;
			container.Method.FullOpEq = new OpEqModel();
			container.Method.FullString = "Modified Method Full";
			container.Method.NotifiedEquals = new EqualsModel();
			container.Method.NotifiedEquatable = new EquatableModel();
			++container.Method.NotifiedInt;
			container.Method.NotifiedOpEq = new OpEqModel();
			container.Method.NotifiedString = "Modified Method Notified";
			container.Dependency.AutoDefaultedHandledInt += 20;
			container.Dependency.AutoDefaultedInt += 20;
			container.Dependency.AutoHandledInt += 20;
			container.Dependency.AutoInt += 20;
			container.Dependency.ManualDefaultedHandledInt += 20;
			container.Dependency.ManualDefaultedInt += 20;
			container.Dependency.ManualHandledInt += 20;
			container.Dependency.ManualInt += 20;
			Debug.WriteLine(container.OnDemand.ManualPod.DataInt);
			Debug.WriteLine(container.OnDemand.ManualConstructedPod.DataInt);
			Debug.WriteLine(container.OnDemand.ManualGetPod.DataInt);
			Debug.WriteLine(container.OnDemand.OnDemandPod.DataInt);
			Debug.WriteLine(container.OnDemand.OnDemandConstructedPod.DataInt);
			Debug.WriteLine(container.OnDemand.OnDemandGetPod.DataInt);
		}
	}

	public class Container
	{
		public CheckedViewModel Checked { get; set; }
		public InitializedViewModel Initialized { get; set; }
		public MethodViewModel Method { get; set; }
		public DependencyTest Dependency { get; set; }
		public OnDemandTest OnDemand { get; set; }
		public XmlSerializableTest XmlSerializable = new XmlSerializableTest();
	}
}
