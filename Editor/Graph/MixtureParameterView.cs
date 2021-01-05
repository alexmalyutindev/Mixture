using System;
using System.Collections.Generic;
using GraphProcessor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mixture
{
    public class MixtureParameterView : ExposedParameterView
    {
		static readonly string mixtureParameterStyleSheet = "MixtureParameterView";

		public MixtureParameterView()
		{
            var style = Resources.Load<StyleSheet>(mixtureParameterStyleSheet);
            if (style != null)
                styleSheets.Add(style);
		}

		protected override IEnumerable< Type > GetExposedParameterTypes()
        {
			// We only accept these types:
			yield return typeof(BoolParameter);
			yield return typeof(ColorParameter);
			yield return typeof(TextureParameter);
			yield return typeof(RenderTextureParameter);
			yield return typeof(Texture2DParameter);
			yield return typeof(Texture3DParameter);
			yield return typeof(CubemapParameter);
			yield return typeof(IntParameter);
			yield return typeof(FloatParameter);
			yield return typeof(Vector2Parameter);
			yield return typeof(Vector3Parameter);
			yield return typeof(Vector4Parameter);
			yield return typeof(GradientParameter);
			yield return typeof(AnimationCurveParameter);
			yield return typeof(MeshParameter);
			yield return typeof(StringParameter);
			yield return typeof(ComputeBufferParameter);
        }
    }
}
