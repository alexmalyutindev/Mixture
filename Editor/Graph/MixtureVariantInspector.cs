using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using GraphProcessor;
using System.Linq;
using System;
using UnityEditor.UIElements;
using Object = UnityEngine.Object;

namespace Mixture
{
   	// By default textures don't have any CustomEditors so we can define them for Mixture
	[CustomEditor(typeof(MixtureVariant), false)]
	class MixtureVariantInspector : Editor 
	{
        MixtureVariant variant;
        MixtureGraph graph;
        ExposedParameterFieldFactory exposedParameterFactory;

        [SerializeField, SerializeReference]
        List<ExposedParameter> visibleParameters = new List<ExposedParameter>();

        Dictionary<ExposedParameter, VisualElement> parameterViews = new Dictionary<ExposedParameter, VisualElement>();
        VisualTreeAsset overrideParameterView;
        VisualElement root, parameters;

        internal RenderTexture variantPreview;
        Editor defaultTextureEditor;
        Editor variantPreviewEditor;

		protected void OnEnable()
		{
            variant = target as MixtureVariant;
            graph = variant.parentGraph;
            if (graph != null)
			{
				graph.onExposedParameterListChanged += UpdateParameters;
				graph.onExposedParameterModified += UpdateParameters;
				graph.onExposedParameterValueChanged += UpdateParameters;
                Undo.undoRedoPerformed += UpdateParameters;
			}

            MixtureVariant parent = variant.parentVariant;
            while (parent != null)
            {
                parent.parameterValueChanged += UpdateParameters;
                parent = parent.parentVariant;
            }

            // TODO: create a temp render texture to copy the result of the graph we process
            // it will be used to display in real time how the parameter changes affects the texture

            // Update serialized parameters (for the inspector)
            SyncParameters();
            exposedParameterFactory = new ExposedParameterFieldFactory(graph, visibleParameters);

            overrideParameterView = Resources.Load<VisualTreeAsset>("UI Blocks/MixtureVariantParameter");
		}

        internal void SetDefaultTextureEditor(Editor textureEditor)
            => defaultTextureEditor = textureEditor;

		protected void OnDisable()
        {
            if (graph != null)
			{
				graph.onExposedParameterListChanged -= UpdateParameters;
				graph.onExposedParameterModified -= UpdateParameters;
				graph.onExposedParameterValueChanged -= UpdateParameters;
                Undo.undoRedoPerformed -= UpdateParameters;
			}

            MixtureVariant parent = variant.parentVariant;
            while (parent != null)
            {
                parent.parameterValueChanged -= UpdateParameters;
                parent = parent.parentVariant;
            }

            if (variantPreview != null)
            {
                variantPreview.Release();
                variantPreview = null;
            }

            if (variantPreviewEditor != null)
            {
                Destroy(variantPreviewEditor);
                variantPreviewEditor = null;
            }

            exposedParameterFactory.Dispose();
        }

		public override VisualElement CreateInspectorGUI()
        {
			if (graph == null)
				return base.CreateInspectorGUI();

            root = new VisualElement();
            UpdateParentView();
			UpdateParameters();

            return root;
        }

        void UpdateParentView()
        {
            var container = new VisualElement();

            var headerLabel = new Label("Parent Hierarchy");
            headerLabel.AddToClassList("Header");
            container.Add(headerLabel);

            // Create a hierarchy queue
            Queue<Object> parents = new Queue<Object>();
            MixtureVariant currentVariant = variant.parentVariant;
            while (currentVariant != null)
            {
                parents.Enqueue(currentVariant);
                currentVariant = currentVariant.parentVariant;
            }
            parents.Enqueue(graph);

            // UIElements breadcrumbs bar
            var parentBar = new ToolbarBreadcrumbs(); 
            parentBar.AddToClassList("Indent");
            parentBar.AddToClassList("VariantBreadcrumbs");
            foreach (var obj in parents.Reverse())
            {
                var v = obj as MixtureVariant;
                var g = obj as MixtureGraph;

                parentBar.PushItem(obj.name, () => {
                    Selection.activeObject = v?.mainOutputTexture ?? g?.mainOutputTexture ?? obj;
                });
            }

            // Add new variant button:
            parentBar.PushItem("+", () => {
                MixtureAssetCallbacks.CreateMixtureVariant(null, variant);
            });

            container.Add(parentBar);

            root.Add(container);
        }

        void SyncParameters()
        {
            // Clone the graph parameters into visible parameters
            visibleParameters.Clear();
            visibleParameters.AddRange(graph.exposedParameters.Select(p => p.Clone()));

            MixtureVariant currentVariant = variant;
            foreach (var overrideParam in variant.GetAllOverrideParameters())
            {
                var param = visibleParameters.FirstOrDefault(p => p == overrideParam);
                if (param != null)
                {
                    if (param.GetValueType().IsAssignableFrom(overrideParam.GetValueType()))
                        param.value = overrideParam.value;
                }
            }
        }

		void UpdateParameters(ExposedParameter param) => UpdateParameters();
		void UpdateParameters()
		{
            if (root == null)
                root = new VisualElement();
			if (parameters == null || !root.Contains(parameters))
			{
				parameters = new VisualElement();
				root.Add(parameters);
			}

            SyncParameters();
            var serializedInspector = new SerializedObject(this);

			parameters.Clear();
			bool header = true;
			bool showUpdateButton = false;

            foreach (var param in visibleParameters)
            {
                if (param.settings.isHidden)
                    continue;

				if (header)
				{
					var headerLabel = new Label("Exposed Parameters");
					headerLabel.AddToClassList("Header");
					parameters.Add(headerLabel);
					header = false;
					showUpdateButton = true;
				}

                var field = CreateParameterVariantView(param, serializedInspector);
                parameters.Add(field);
            }

			if (showUpdateButton)
			{
				var updateButton = new Button(UpdateAllVariantTextures){ text = "Update Texture(s)" };
				updateButton.AddToClassList("Indent");
				parameters.Add(updateButton);
			}
		}

        void UpdateAllVariantTextures()
        {
            variant.UpdateAllVariantTextures();

            // If the parentGraph is opened in the editor, we don't want to mess with previews
            // so we update the parentGraph with the original params again.
            if (MixtureUpdater.IsMixtureEditorOpened(graph))
                MixtureGraphProcessor.RunOnce(graph);
        }

        VisualElement CreateParameterVariantView(ExposedParameter param, SerializedObject serializedInspector)
        {
            VisualElement prop = new VisualElement();
            prop.AddToClassList("Indent");
            prop.style.display = DisplayStyle.Flex;
            var parameterView = overrideParameterView.CloneTree();
            prop.Add(parameterView);

            var parameterValueField = exposedParameterFactory.GetParameterValueField(param, (newValue) => {
                param.value = newValue;
                UpdateOverrideParameter(param, newValue);
            });

            prop.AddManipulator(new ContextualMenuManipulator(e => {
                e.menu.AppendAction("Reset", _ => RemoveOverride(param));
            }));

            parameterValueField.Bind(serializedInspector);
            var paramContainer = parameterView.Q("Parameter");
            paramContainer.Add(parameterValueField);

            parameterViews[param] = parameterView;

            if (variant.overrideParameters.Contains(param))
                AddOverrideClass(parameterView);

            return prop;
        }

        void RemoveOverride(ExposedParameter parameter)
        {
            Undo.RegisterCompleteObjectUndo(variant, "Reset parameter");

            variant.overrideParameters.RemoveAll(p => p == parameter);
            parameter.value = GetDefaultParameterValue(parameter);
            exposedParameterFactory.ResetOldParameter(parameter);

            variant.NotifyOverrideValueChanged(parameter);
            UpdateParameters();

            if (parameterViews.TryGetValue(parameter, out var view))
            {
                view.RemoveFromClassList("Override");
            }
        }

        object GetDefaultParameterValue(ExposedParameter parameter)
        {
            foreach (var param in variant.GetAllOverrideParameters())
                if (param == parameter)
                    return param.value;
            return parameter.value;
        }

        void UpdateOverrideParameter(ExposedParameter parameter, object overrideValue)
        {
            if (!variant.overrideParameters.Contains(parameter))
            {
                Undo.RegisterCompleteObjectUndo(variant, "Override Parameter");
                variant.overrideParameters.Add(parameter);
                EditorUtility.SetDirty(variant);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(variant, "Override Parameter");
                int index = variant.overrideParameters.FindIndex(p => p == parameter);
                variant.overrideParameters[index].value = parameter.value;
                EditorUtility.SetDirty(variant);
            }

            // Let know variant of variants that a property value changed
            variant.NotifyOverrideValueChanged(parameter);

            // Enable the override overlay:
            if (parameterViews.TryGetValue(parameter, out var view))
                AddOverrideClass(view);
        }

        void AddOverrideClass(VisualElement view)
        {
            view.AddToClassList("Override");
        }

		// This block of functions allows us dynamically switch between the render texture inspector preview
        // and the default texture2D preview (the one for the asset on the disk)
		Editor GetPreviewEditor() => variantPreviewEditor ?? defaultTextureEditor ?? this;
		public override string GetInfoString() => GetPreviewEditor().GetInfoString();
		public override void ReloadPreviewInstances() => GetPreviewEditor().ReloadPreviewInstances();
		public override bool RequiresConstantRepaint() => GetPreviewEditor().RequiresConstantRepaint();
		public override bool UseDefaultMargins() => GetPreviewEditor().UseDefaultMargins();
		public override void DrawPreview(Rect previewArea) => GetPreviewEditor().DrawPreview(previewArea);
		public override GUIContent GetPreviewTitle() => GetPreviewEditor().GetPreviewTitle();
		public override bool HasPreviewGUI() => GetPreviewEditor().HasPreviewGUI();
		public override void OnInteractivePreviewGUI(Rect r, GUIStyle background) => GetPreviewEditor().OnInteractivePreviewGUI(r, background);
		public override void OnPreviewGUI(Rect r, GUIStyle background) => GetPreviewEditor().OnPreviewGUI(r, background);
		public override void OnPreviewSettings() => GetPreviewEditor().OnPreviewSettings();
	}
}