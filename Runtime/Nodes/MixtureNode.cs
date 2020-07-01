﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GraphProcessor;
using System;
using System.Linq;
using Object = UnityEngine.Object;
using UnityEngine.Experimental.Rendering;
using System.Text.RegularExpressions;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Rendering;
#endif
using UnityEngine.Profiling;

namespace Mixture
{
	public abstract class MixtureNode : BaseNode
	{
		protected new MixtureGraph			graph => base.graph as MixtureGraph;

		public MixtureRTSettings			rtSettings;

		protected virtual MixtureRTSettings	defaultRTSettings => MixtureRTSettings.defaultValue;
		public virtual float   				nodeWidth => MixtureUtils.defaultNodeWidth;
		public virtual Texture				previewTexture => null;
		public virtual bool					hasSettings => true;
		public virtual bool					hasPreview => true;
		public virtual List<OutputDimension> supportedDimensions => new List<OutputDimension>() {
			OutputDimension.Texture2D,
			OutputDimension.Texture3D,
			OutputDimension.CubeMap,
		};
		public virtual PreviewChannels		defaultPreviewChannels => PreviewChannels.RGBA;
		public virtual bool					showDefaultInspector => false;
		public virtual bool					showPreviewExposure => false;
		[SerializeField]
		public bool							isPreviewCollapsed = false;

		public event Action					onSettingsChanged;
		public event Action					beforeProcessSetup;
		public event Action					afterProcessCleanup;

		public override bool				showControlsOnHover => canShowOnHover && false; // Disable this feature for now

		public override bool				needsInspector => true;

		protected Dictionary<string, Material> temporaryMaterials = new Dictionary<string, Material>();

        // UI Serialization
        [SerializeField]
        public PreviewChannels previewMode;
        [SerializeField]
        public float previewMip = 0.0f;
        [SerializeField]
        public bool previewVisible = true;
		[SerializeField]
		public float previewEV100 = 0.0f;

		internal bool canShowOnHover = true;

		CustomSampler		_sampler;
		CustomSampler		sampler
		{
			get
			{
				if (_sampler == null)
				{
					_sampler = CustomSampler.Create(name, true);
					recorder = sampler.GetRecorder();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
					recorder.enabled = true;
#endif
				}

				return _sampler;
			}
		}
		protected Recorder	recorder { get; private set; }

		internal virtual float processingTimeInMillis
		{
			get
			{
				// By default we display the GPU processing time
				if (recorder != null)
					return recorder.gpuElapsedNanoseconds / 1000000.0f;
				return 0;
			}
		}

        public override void OnNodeCreated()
		{
			base.OnNodeCreated();
			rtSettings = defaultRTSettings;
			previewMode = defaultPreviewChannels;
		}

		protected bool UpdateTempRenderTexture(ref CustomRenderTexture target, bool hasMips = false, bool autoGenerateMips = false)
		{
			if (graph.outputTexture == null)
				return false;

			int outputWidth = rtSettings.GetWidth(graph);
			int outputHeight = rtSettings.GetHeight(graph);
			int outputDepth = rtSettings.GetDepth(graph);
			GraphicsFormat targetFormat = rtSettings.GetGraphicsFormat(graph);
			TextureDimension dimension = rtSettings.GetTextureDimension(graph);
			
			if (dimension == TextureDimension.Cube)
				outputHeight = outputDepth = outputWidth; // we only use the width for cubemaps

            if (targetFormat == GraphicsFormat.None)
                targetFormat = graph.outputTexture.graphicsFormat;
			if (dimension == TextureDimension.None)
				dimension = TextureDimension.Tex2D;

			if (target == null)
			{
                target = new CustomRenderTexture(outputWidth, outputHeight, targetFormat)
                {
                    volumeDepth = Math.Max(1, outputDepth),
					depth = 0,
                    dimension = dimension,
                    name = $"Mixture Temp {name}",
                    updateMode = CustomRenderTextureUpdateMode.OnDemand,
                    doubleBuffered = rtSettings.doubleBuffered,
                    wrapMode = rtSettings.wrapMode,
                    filterMode = rtSettings.filterMode,
                    useMipMap = hasMips,
					autoGenerateMips = autoGenerateMips,
					enableRandomWrite = true,
					hideFlags = HideFlags.HideAndDontSave,
				};
				target.Create();
				target.material = MixtureUtils.dummyCustomRenderTextureMaterial;

				return true;
			}

			// TODO: check if format is supported by current system

			// Warning: here we use directly the settings from the 
			if (target.width != Math.Max(1, outputWidth)
				|| target.height != Math.Max(1, outputHeight)
				|| target.graphicsFormat != targetFormat
				|| target.dimension != dimension
				|| target.volumeDepth != outputDepth
				|| target.filterMode != rtSettings.filterMode
				|| target.doubleBuffered != rtSettings.doubleBuffered
                || target.wrapMode != rtSettings.wrapMode
				|| target.useMipMap != hasMips
				|| target.autoGenerateMips != autoGenerateMips)
			{
				target.Release();
				target.width = Math.Max(1, outputWidth);
				target.height = Math.Max(1, outputHeight);
				target.graphicsFormat = (GraphicsFormat)targetFormat;
				target.dimension = dimension;
				target.volumeDepth = outputDepth;
				target.doubleBuffered = rtSettings.doubleBuffered;
                target.wrapMode = rtSettings.wrapMode;
                target.filterMode = rtSettings.filterMode;
                target.useMipMap = hasMips;
				target.autoGenerateMips = autoGenerateMips;
				target.Create();
				if (target.material == null)
					target.material = MixtureUtils.dummyCustomRenderTextureMaterial;
			}

			return false;
		}

		public void OnProcess(CommandBuffer cmd)
		{
			inputPorts.PullDatas();

			ExceptionToLog.Call(() => Process(cmd));

			InvokeOnProcessed();

			outputPorts.PushDatas();
		}

		protected sealed override void Process() => throw new Exception("Do not use");

		void Process(CommandBuffer cmd)
		{
			var outputDimension = rtSettings.GetTextureDimension(graph);

			if (!supportedDimensions.Contains((OutputDimension)outputDimension))
			{
				AddMessage($"Dimension {outputDimension} is not supported by this node", NodeMessageType.Error);
				return ;
			}
			else
			{
				// TODO: simplify this with the node graph processor remove badges with matching words feature
				RemoveMessage($"Dimension {TextureDimension.Tex2D} is not supported by this node");
				RemoveMessage($"Dimension {TextureDimension.Tex3D} is not supported by this node");
				RemoveMessage($"Dimension {TextureDimension.Cube} is not supported by this node");
			}

			beforeProcessSetup?.Invoke();

			// Avoid adding markers if it's CRT processing (CRT  already have one)
			if (this is IUseCustomRenderTextureProcessing)
				ProcessNode(cmd);
			else
			{
				cmd.BeginSample(sampler);
				ProcessNode(cmd);
				cmd.EndSample(sampler);
			}
			afterProcessCleanup?.Invoke();
		}

		protected virtual bool ProcessNode(CommandBuffer cmd) => true;

		protected void AddObjectToGraph(Object obj) => graph.AddObjectToGraph(obj);
		protected void RemoveObjectFromGraph(Object obj) => graph.RemoveObjectFromGraph(obj);

		protected Type GetPropertyType(Shader shader, int shaderPropertyIndex)
		{
			var type = shader.GetPropertyType(shaderPropertyIndex);

			switch (type)
			{
				case ShaderPropertyType.Color:
					return typeof(Color);
				case ShaderPropertyType.Float:
				case ShaderPropertyType.Range:
					return typeof(float);
				case ShaderPropertyType.Texture:
					return TextureUtils.GetTypeFromDimension(shader.GetPropertyTextureDimension(shaderPropertyIndex));
				default:
				case ShaderPropertyType.Vector:
					return typeof(Vector4);
			}
		}

		static Regex tooltipRegex = new Regex(@"Tooltip\((.*)\)");
		protected IEnumerable< PortData > GetMaterialPortDatas(Material material)
		{
			if (material == null)
				yield break;

			var currentDimension = rtSettings.GetTextureDimension(graph);

			var s = material.shader;
			for (int i = 0; i < material.shader.GetPropertyCount(); i++)
			{
				var flags = s.GetPropertyFlags(i);
				var name = s.GetPropertyName(i);
				var displayName = s.GetPropertyDescription(i);
				var type = s.GetPropertyType(i);
				var tooltip = s.GetPropertyAttributes(i).Where(s => s.Contains("Tooltip")).FirstOrDefault();

				if (tooltip != null)
				{
					// Parse tooltip:
					var m = tooltipRegex.Match(tooltip);
					tooltip = m.Groups[1].Value;
				}

				if (flags == ShaderPropertyFlags.HideInInspector
					|| flags == ShaderPropertyFlags.NonModifiableTextureData
					|| flags == ShaderPropertyFlags.PerRendererData)
					continue;
				
				if (!PropertySupportsDimension(s.GetPropertyName(i), currentDimension))
					continue;

				yield return new PortData{
					identifier = name,
					displayName = displayName,
					displayType = GetPropertyType(s, i),
					tooltip = tooltip,
				};
			}
		}

		bool PropertySupportsDimension(string name, TextureDimension dim)
		{
			return MixtureUtils.GetAllowedDimentions(name).Contains(dim);
		}

		protected void AssignMaterialPropertiesFromEdges(List< SerializableEdge > edges, Material material)
		{
			// Update material settings when processing the graph:
			foreach (var edge in edges)
			{
				string propName = edge.inputPort.portData.identifier;
				int propertyIndex = material.shader.FindPropertyIndex(propName);
				switch (material.shader.GetPropertyType(propertyIndex))
				{
					case ShaderPropertyType.Color:
						material.SetColor(propName, (Color)edge.passThroughBuffer);
						break;
					case ShaderPropertyType.Texture:
						// TODO: texture scale and offset
						material.SetTexture(propName, (Texture)edge.passThroughBuffer);
						break;
					case ShaderPropertyType.Float:
					case ShaderPropertyType.Range:
						switch (edge.passThroughBuffer)
						{
							case float f:
								material.SetFloat(propName, f);
								break;
							case Vector2 v:
								material.SetFloat(propName, v.x);
								break;
							case Vector3 v:
								material.SetFloat(propName, v.x);
								break;
							case Vector4 v:
								material.SetFloat(propName, v.x);
								break;
							default:
								throw new Exception($"Can't assign {edge.passThroughBuffer.GetType()} to material float property");
						}
						break;
					case ShaderPropertyType.Vector:
						material.SetVector(propName, MixtureConversions.ConvertObjectToVector4(edge.passThroughBuffer));
						break;
				}
			}
		}

#if UNITY_EDITOR
		protected bool IsShaderCompiled(Shader s)
		{
			return !ShaderUtil.GetShaderMessages(s).Any(m => m.severity == ShaderCompilerMessageSeverity.Error);
		}

		protected void LogShaderErrors(Shader s)
		{
			foreach (var m in ShaderUtil.GetShaderMessages(s).Where(m => m.severity == ShaderCompilerMessageSeverity.Error))
			{
				string file = String.IsNullOrEmpty(m.file) ? s.name : m.file;
				Debug.LogError($"{file}:{m.line} {m.message} {m.messageDetails}");
			}
		}
#endif

		public void OnSettingsChanged() => onSettingsChanged?.Invoke();

		Dictionary<Material, Material>		defaultMaterials = new Dictionary<Material, Material>();

		public Material	GetDefaultMaterial(Material mat)
		{
			Material defaultMat;

			if (defaultMaterials.TryGetValue(mat, out defaultMat))
				return defaultMat;
			
			return defaultMaterials[mat] = new Material(mat.shader) { hideFlags =  HideFlags.HideAndDontSave };
		}

		public void ResetMaterialPropertyToDefault(Material mat, string propName)
		{
			if (mat == null)
				return;

			int idx = mat.shader.FindPropertyIndex(propName);
			switch (mat.shader.GetPropertyType(idx))
			{
				case ShaderPropertyType.Float:
				case ShaderPropertyType.Range:
					mat.SetFloat(propName, GetDefaultMaterial(mat).GetFloat(propName));
					break;
				case ShaderPropertyType.Vector:
					mat.SetVector(propName, GetDefaultMaterial(mat).GetVector(propName));
					break;
				case ShaderPropertyType.Texture:
					mat.SetTexture(propName, GetDefaultMaterial(mat).GetTexture(propName));
					break;
			}
		}
		
		static IEnumerable<BaseNode> GetNonCRTInputNodes(BaseNode child)
		{
			foreach (var node in child.GetInputNodes())
				if (!(node is IUseCustomRenderTextureProcessing))
					yield return node;
		}

		public List<BaseNode> GetMixtureDependencies()
		{
			HashSet<BaseNode> dependencies = new HashSet<BaseNode>();
			Stack<BaseNode> inputNodes = new Stack<BaseNode>(GetNonCRTInputNodes(this));

			dependencies.Add(this);

			while (inputNodes.Count > 0)
			{
				var child = inputNodes.Pop();

				foreach (var parent in GetNonCRTInputNodes(child))
					inputNodes.Push(parent);
				
				dependencies.Add(child);
			}

			return dependencies.OrderBy(d => d.computeOrder).ToList();
		}

		public Material GetTempMaterial(string shaderName)
		{
			temporaryMaterials.TryGetValue(shaderName, out var material);

			if (material == null)
			{
				var shader = Shader.Find(shaderName);
				if (shader == null)
					throw new Exception("Can't find shader " + shaderName);
				material = temporaryMaterials[shaderName] = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
			}

			return material;
		}

		protected override void Disable()
		{
			foreach (var matKp in temporaryMaterials)
				CoreUtils.Destroy(matKp.Value);
			base.Disable();
		}
	}

	[Flags]
	public enum EditFlags
	{
		None			= 0,
		Width			= 1 << 0,
		WidthMode		= 1 << 1,
		Height			= 1 << 2,
		HeightMode		= 1 << 3,
		Depth			= 1 << 4,
		DepthMode		= 1 << 5,
		Dimension		= 1 << 6,
		TargetFormat	= 1 << 7,
		POTSize			= 1 << 8,

		// Headers
		Size			= Width | WidthMode | Height | HeightMode | Depth | DepthMode,
		Format			= POTSize | Dimension | TargetFormat,
		
		All				= ~0,
	}

	public enum POTSize
	{
		_32			= 32,
		_64			= 64,
		_128		= 128,
		_256		= 256,
		_512		= 512,
		_1024		= 1024,
		_2048		= 2048,
		_4096		= 4096,
		_8192		= 8192,
		Custom		= -1,
	}

	public enum OutputSizeMode
	{
		Default = 0,
		Fixed = 1,
		PercentageOfOutput = 2
	}

	public enum OutputDimension
	{
		Default = TextureDimension.None,
		Texture2D = TextureDimension.Tex2D,
		CubeMap = TextureDimension.Cube,
		Texture3D = TextureDimension.Tex3D,
		// Texture2DArray = TextureDimension.Tex2DArray, // Not supported by CRT, will be handled as Texture3D and then saved as Tex2DArray
	}

	public enum OutputFormat
	{
		Default = GraphicsFormat.None,
		RGBA_LDR = GraphicsFormat.R8G8B8A8_UNorm,
		RGBA_sRGB = GraphicsFormat.R8G8B8A8_SRGB,
		RGBA_Half = GraphicsFormat.R16G16B16A16_SFloat,
		RGBA_Float = GraphicsFormat.R32G32B32A32_SFloat,
		R8_Unsigned = GraphicsFormat.R8_UNorm,
		R16 = GraphicsFormat.R16_SFloat,
	}

	[Flags]
    public enum PreviewChannels
    {
        R = 1,
        G = 2,
        B = 4,
        A = 8,
        RG = R | G,
        RB = R | B,
        GB = G | B,
        RGB = R | G | B,
        RGBA = R | G | B | A,
    }

	// Note: to keep in sync with UnityEditor.TextureCompressionQuality
	public enum MixtureCompressionQuality
	{
    	Fast     = 0,
    	Normal   = 50,
    	Best     = 100,
	}
}