using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;

namespace Mixture
{
	[System.Serializable, NodeMenuItem("Custom/Combine Mask (HDRP)")]
	public class HDRPMaskCombine : FixedShaderNode
	{
		public override string name => "Combine Mask (HDRP)";

		public override string shaderName => "Hidden/Mixture/HDRPMaskCombine";

		public override bool displayMaterialInspector => true;

		// Enumerate the list of material properties that you don't want to be turned into a connectable port.
		protected override IEnumerable<string> filteredOutProperties => new string[]{};

		protected override MixtureRTSettings defaultRTSettings
        {
            get {
                var rts = MixtureRTSettings.defaultValue;
                rts.dimension = OutputDimension.Texture2D;
                return rts;
            }
        }

		// Override this if you node is not compatible with all dimensions
		public override List<OutputDimension> supportedDimensions => new List<OutputDimension>() {
			OutputDimension.Texture2D,
		};
	}
}