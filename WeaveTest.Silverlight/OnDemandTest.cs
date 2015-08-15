namespace WeaveTest.Silverlight
{
	public class OnDemandTest
	{
		[OnDemand]
		public Pod OnDemandPod { get; set; }

		[OnDemand(Type = typeof(DerivedPod))]
		public Pod OnDemandDerivedPod { get; set; }

		[OnDemand]
		public Pod OnDemandConstructedPod { get; set; }
		private Pod CreateOnDemandConstructedPod() { return new Pod { DataInt = 1 }; }

		[OnDemand]
		public Pod OnDemandGetPod { get { return new Pod { DataInt = 2 }; } }

		private Pod manualPod;
		public Pod ManualPod
		{
			get
			{
				if(manualPod == null)
					manualPod = new Pod();
				return manualPod;
			}
			set { manualPod = value; }
		}

		private Pod manualConstructedPod;
		public Pod ManualConstructedPod
		{
			get
			{
				if(manualConstructedPod == null)
					manualConstructedPod = CreateOnDemandConstructedPod();
				return manualConstructedPod;
			}
			set { manualConstructedPod = value; }
		}

		private Pod manualGetPod;
		public Pod ManualGetPod
		{
			get
			{
				if(manualGetPod == null)
					manualGetPod = new Pod { DataInt = 2 };
				return manualGetPod;
			}
		}
	}

	public class DerivedPod : Pod { }
}
