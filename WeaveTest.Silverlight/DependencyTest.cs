using System.Diagnostics;
using System.Windows;

namespace WeaveTest.Silverlight
{
	public class DependencyTest : DependencyObject
	{
		public int ManualInt
		{
			get { return (int)GetValue(ManualIntProperty); }
			set { SetValue(ManualIntProperty, value); }
		}

		public static readonly DependencyProperty ManualIntProperty =
			DependencyProperty.Register("ManualInt", typeof(int), typeof(DependencyTest), null);

		public int ManualDefaultedInt
		{
			get { return (int)GetValue(ManualDefaultedIntProperty); }
			set { SetValue(ManualDefaultedIntProperty, value); }
		}

		public static readonly DependencyProperty ManualDefaultedIntProperty =
			DependencyProperty.Register("ManualDefaultedInt", typeof(int), typeof(DependencyTest),
			new PropertyMetadata(11));

		public int ManualHandledInt
		{
			get { return (int)GetValue(ManualHandledIntProperty); }
			set { SetValue(ManualHandledIntProperty, value); }
		}

		public static readonly DependencyProperty ManualHandledIntProperty =
			DependencyProperty.Register("ManualHandledInt", typeof(int), typeof(DependencyTest),
			new PropertyMetadata(OnManualHandledIntChanged));

		private static void OnManualHandledIntChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			Debug.WriteLine("OnManualHandledIntChanged");
		}

		public int ManualDefaultedHandledInt
		{
			get { return (int)GetValue(ManualDefaultedHandledIntProperty); }
			set { SetValue(ManualDefaultedHandledIntProperty, value); }
		}

		public static readonly DependencyProperty ManualDefaultedHandledIntProperty =
			DependencyProperty.Register("ManualDefaultedHandledInt", typeof(int), typeof(DependencyTest),
			new PropertyMetadata(13, OnManualDefaultedHandledIntChanged));

		private static void OnManualDefaultedHandledIntChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			Debug.WriteLine("OnManualDefaultedHandledIntChanged");
		}

		[Dependency]
		public int AutoInt { get; set; }
		[Dependency(DefaultValue = 1)]
		public int AutoDefaultedInt { get; set; }
		[Dependency(PropertyChangedCallback = "OnAutoHandledIntChanged")]
		public int AutoHandledInt { get; set; }
		[Dependency(DefaultValue = 3, PropertyChangedCallback = "OnAutoDefaultedHandledIntChanged")]
		public int AutoDefaultedHandledInt { get; set; }

		private static void OnAutoHandledIntChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			Debug.WriteLine("OnAutoHandledIntChanged");
		}

		private static void OnAutoDefaultedHandledIntChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			Debug.WriteLine("OnAutoDefaultedHandledIntChanged");
		}
	}
}
