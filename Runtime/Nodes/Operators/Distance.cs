﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;
using UnityEngine.Rendering;

namespace Mixture
{
	[Documentation(@"
Execute a flood fill operation on all pixels above the specified threshold.

Note that the computational cost of this node only depends on the texture resolution and not the distance parameter.

Smooth is only in alpha
")]

	[System.Serializable, NodeMenuItem("Operators/Distance")]
	public class Distance : ComputeShaderNode
	{
		public enum Mode
		{
			InputBlend,
			InputOnly,
			Mask,
			UV,
		}

		public enum DistanceMode
		{
			Euclidian,
			Manhattan,
			Chebyshev,
			// Minkovsky,
		}

		public enum ThresholdMode
		{
			Luminance,
			R, G, B, A,
			RGB,
			RGBA,
		}

		[Input]
		public Texture input;

		[Output]
		public CustomRenderTexture output;

		[Tooltip("Output mode of the distance, by default a blend with the distance is performed")]
		public Mode mode;

		[Tooltip("Threshold value to select pixels to dilate. Any value above this threshold will be selected")]
		public float threshold = 0.1f;

		[ShowInInspector, Tooltip("Select which value to compare against the threshold")]
		public ThresholdMode thresholdMode;

		[Tooltip("Distance value in percent of the texture size")]
		public float distance = 50;

		[ShowInInspector, Tooltip("How the distance is calculated")]
		public DistanceMode distanceMode;

		public override string name => "Distance";

		protected override string computeShaderResourcePath => "Mixture/Distance";

		public override bool showDefaultInspector => true;

		public override Texture previewTexture => output;

		public override List<OutputDimension> supportedDimensions => new List<OutputDimension>() {
			OutputDimension.Texture2D,
			OutputDimension.Texture3D,
		};

		int fillUvKernel;
		int jumpFloodingKernel;
		int finalPassKernel;

		[CustomPortBehavior(nameof(input))]
		protected IEnumerable< PortData > ChangeInputPortType(List< SerializableEdge > edges)
		{
			yield return new PortData{
				displayName = "Input",
				displayType = TextureUtils.GetTypeFromDimension(rtSettings.GetTextureDimension(graph)),
				identifier = "Input",
				acceptMultipleEdges = true,
			};
		}

		[CustomPortBehavior(nameof(output))]
		protected IEnumerable< PortData > ChangeOutputPortType(List< SerializableEdge > edges)
		{
			yield return new PortData{
				displayName = "Output",
				displayType = TextureUtils.GetTypeFromDimension(rtSettings.GetTextureDimension(graph)),
				identifier = "output",
				acceptMultipleEdges = true,
			};
		}

		protected override void Enable()
		{
			base.Enable();

			rtSettings.outputChannels = OutputChannel.RGBA;
			rtSettings.outputPrecision = OutputPrecision.Full;
			rtSettings.editFlags = EditFlags.Dimension | EditFlags.Size;

			UpdateTempRenderTexture(ref output);

			fillUvKernel = computeShader.FindKernel("FillUVMap");
			jumpFloodingKernel = computeShader.FindKernel("JumpFlooding");
			finalPassKernel = computeShader.FindKernel("FinalPass");
		}

		protected override bool ProcessNode(CommandBuffer cmd)
		{
			// Force the double buffering for multi-pass flooding
			rtSettings.doubleBuffered = true;

			if (!base.ProcessNode(cmd) || input == null)
				return false;

			UpdateTempRenderTexture(ref output);

			cmd.SetComputeFloatParam(computeShader, "_Threshold", threshold);
			cmd.SetComputeVectorParam(computeShader, "_Size", new Vector4(output.width, 1.0f / output.width));
			cmd.SetComputeFloatParam(computeShader, "_Distance", distance / 100.0f);
			cmd.SetComputeIntParam(computeShader, "_ThresholdMode", (int)thresholdMode);
			cmd.SetComputeIntParam(computeShader, "_DistanceMode", (int)distanceMode);
			cmd.SetComputeIntParam(computeShader, "_Mode", (int)mode);

			output.doubleBuffered = true;
			output.EnsureDoubleBufferConsistency();
			var rt = output.GetDoubleBufferRenderTexture();
			rt.Release();
			rt.enableRandomWrite = true;
			rt.Create();

			MixtureUtils.SetupComputeDimensionKeyword(computeShader, input.dimension);

			cmd.SetComputeTextureParam(computeShader, fillUvKernel, "_Input", input);
			cmd.SetComputeTextureParam(computeShader, fillUvKernel, "_Output", output);
			cmd.SetComputeTextureParam(computeShader, fillUvKernel, "_FinalOutput", rt);
			cmd.SetComputeIntParam(computeShader, "_DistanceMode", (int)distanceMode);
			cmd.SetComputeFloatParam(computeShader, "_InputScaleFactor", (float)input.width / (float)output.width);
			DispatchCompute(cmd, fillUvKernel, output.width, output.height, output.volumeDepth);

			int maxLevels = (int)Mathf.Log(input.width, 2);
			for (int i = 0; i <= maxLevels; i++)
			{
				float offset = 1 << (maxLevels - i);
				cmd.SetComputeFloatParam(computeShader, "_InputScaleFactor", 1);
				cmd.SetComputeFloatParam(computeShader, "_Offset", offset);
				cmd.SetComputeTextureParam(computeShader, jumpFloodingKernel, "_Input", output);
				cmd.SetComputeTextureParam(computeShader, jumpFloodingKernel, "_Output", rt);
				cmd.SetComputeIntParam(computeShader, "_DistanceMode", (int)distanceMode);
				DispatchCompute(cmd, jumpFloodingKernel, output.width, output.height, output.volumeDepth);
				cmd.CopyTexture(rt, output);
			}

			cmd.SetComputeFloatParam(computeShader, "_InputScaleFactor", (float)input.width / (float)output.width);
			cmd.SetComputeTextureParam(computeShader, finalPassKernel, "_Input", input);
			cmd.SetComputeTextureParam(computeShader, finalPassKernel, "_Output", rt);
			cmd.SetComputeIntParam(computeShader, "_DistanceMode", (int)distanceMode);
			cmd.SetComputeTextureParam(computeShader, finalPassKernel, "_FinalOutput", output);
			DispatchCompute(cmd, finalPassKernel, output.width, output.height, output.volumeDepth);

			return true;
		}

        protected override void Disable()
		{
			base.Disable();
			CoreUtils.Destroy(output);
		}

		public CustomRenderTexture GetCustomRenderTexture() => output;
	}
}