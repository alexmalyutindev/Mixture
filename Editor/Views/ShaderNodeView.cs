using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using GraphProcessor;
using System.Collections.Generic;

namespace Mixture
{
	[NodeCustomEditor(typeof(ShaderNode))]
	public class ShaderNodeView : MixtureNodeView
	{
		VisualElement	shaderCreationUI;
		VisualElement	materialEditorUI;
		MaterialEditor	materialEditor;
		ShaderNode		shaderNode => nodeTarget as ShaderNode;

		ObjectField		debugCustomRenderTextureField;
		ObjectField		shaderField;

		int				materialCRC;

		protected override string header => "Shader Properties";

		public override void Enable()
		{
			base.Enable();

			if (shaderNode.material != null && !owner.graph.IsObjectInGraph(shaderNode.material))
				owner.graph.AddObjectToGraph(shaderNode.material);

			shaderField = new ObjectField
			{
				value = shaderNode.shader,
				objectType = typeof(Shader),
			};

			shaderField.RegisterValueChangedCallback((v) => {
				SetShader((Shader)v.newValue);
			});
			
			InitializeDebug();

			controlsContainer.Add(shaderField);

			shaderCreationUI = new VisualElement();
			controlsContainer.Add(shaderCreationUI);
			UpdateShaderCreationUI();

			controlsContainer.Add(new IMGUIContainer(MaterialGUI));
			materialEditor = Editor.CreateEditor(shaderNode.material) as MaterialEditor;

			onPortDisconnected += ResetMaterialPropertyToDefault;
		}

		~ShaderNodeView()
		{
			onPortDisconnected -= ResetMaterialPropertyToDefault;
		}

		void InitializeDebug()
		{
			shaderNode.onProcessed += () => {
				debugCustomRenderTextureField.value = shaderNode.output;
			};

			debugCustomRenderTextureField = new ObjectField("Output")
			{
				value = shaderNode.output
			};
			
			debugContainer.Add(debugCustomRenderTextureField);
		}

		void UpdateShaderCreationUI()
		{
			shaderCreationUI.Clear();

			if (shaderNode.shader == null)
			{
				shaderCreationUI.Add(new Button(CreateNewShader) {
					text = "New Shader"
				});
			}
			else
			{
				shaderCreationUI.Add(new Button(OpenCurrentShader){
					text = "Open"
				});
			}

			void CreateNewShader()
			{
				// TODO: create a popupwindow instead of a context menu
				var menu = new GenericMenu();
				var dim = (OutputDimension)shaderNode.rtSettings.GetTextureDimension(owner.graph);

#if MIXTURE_SHADERGRAPH
				GUIContent shaderGraphContent = EditorGUIUtility.TrTextContentWithIcon("Graph", Resources.Load<Texture2D>("sg_graph_icon@64"));
				menu.AddItem(shaderGraphContent, false, () => SetShader(MixtureEditorUtils.CreateNewShaderGraph(title, dim)));
#endif
				GUIContent shaderTextContent = EditorGUIUtility.TrTextContentWithIcon("Text", "Shader Icon");
				menu.AddItem(shaderTextContent, false, () => SetShader(MixtureEditorUtils.CreateNewShaderText(title, dim)));
				menu.ShowAsContext();
			}

			void OpenCurrentShader()
			{
				AssetDatabase.OpenAsset(shaderNode.shader);
			}
		}
		
		void ResetMaterialPropertyToDefault(PortView pv)
		{
			foreach (var p in shaderNode.ListMaterialProperties(null))
			{
				if (pv.portData.identifier == p.identifier)
					shaderNode.ResetMaterialPropertyToDefault(shaderNode.material, p.identifier);
			}
		}

		void SetShader(Shader newShader)
		{
			owner.RegisterCompleteObjectUndo("Updated Shader of ShaderNode");
			shaderNode.shader = newShader;
			shaderField.value = newShader;
			shaderNode.material.shader = newShader;
			UpdateShaderCreationUI();

			// We fore the update of node ports
			ForceUpdatePorts();
		}

		void MaterialGUI()
		{
			if (shaderNode.material.ComputeCRC() != materialCRC)
			{
				NotifyNodeChanged();
				materialCRC = shaderNode.material.ComputeCRC();
			}

			// Update the GUI when shader is modified
			if (MaterialPropertiesGUI(shaderNode.material))
			{
				UpdateShaderCreationUI();
				// We fore the update of node ports
				ForceUpdatePorts();
			}
		}

		public override void OnRemoved() => owner.graph.RemoveObjectFromGraph(shaderNode.material);
	}
}