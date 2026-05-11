using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using UnityEditor.Experimental.GraphView;

namespace VoxelPlay {

    public class TerrainStepNode : Node {
        const string DEFAULT_NOISE_TEXTURE_RESOURCE = "VoxelPlay/Textures/Noise";
        static readonly Color nodeBodyColor = new Color(0.22f, 0.22f, 0.22f, 1f);

        enum HeightValueUnit {
            Normalized = 0,
            Percentage = 1,
            Meters = 2
        }

        enum HeightUnitSlot {
            SamplerMin = 0,
            SamplerMax = 1,
            ConstantValue = 2,
            ShiftAdd = 3,
            AddAndMultiplyAdd = 4,
            MultiplyAndAddThenAdd = 5,
            Threshold = 6,
            ThresholdShift = 7,
            ThresholdFallback = 8,
            FlattenOrRaiseMin = 9,
            ClampMin = 10,
            ClampMax = 11,
            SelectMin = 12,
            SelectMax = 13,
            SelectOutsideValue = 14,
            FillMin = 15,
            FillMax = 16,
            FillValue = 17,
            TestMin = 18,
            TestMax = 19,
            RemapFromMin = 20,
            RemapFromMax = 21,
            RemapToMin = 22,
            RemapToMax = 23
        }

        const int HEIGHT_UNIT_SLOT_COUNT = (int)HeightUnitSlot.RemapToMax + 1;
        const uint HEIGHT_UNIT_VALUE_MASK = (1u << HEIGHT_UNIT_SLOT_COUNT) - 1u;
        const uint HEIGHT_UNIT_METADATA_PRESENT_BIT = 1u << 31;
        const uint DEFAULT_HEIGHT_UNIT_MASK = HEIGHT_UNIT_METADATA_PRESENT_BIT | HEIGHT_UNIT_VALUE_MASK;

        public TerrainStepType Operation { get; private set; }
        public Port OutputPort { get; private set; }
        public Port InputPort { get; private set; }
        public Port InputPortB { get; private set; }
        public string Description { get; set; }
        public int StepIndex { get; set; } = -1;

        // Cached parameter values (mirrors StepData fields)
        public Texture2D noiseTexture;
        public TerrainData terrainData;
        public float frecuency = 0.1f;
        public Vector2 offset;
        public float noiseRangeMin;
        public float noiseRangeMax = 0.5f;
        public int octaves = 1;
        public float persistence = 0.5f;
        public float lacunarity = 2f;
        public float threshold;
        public float thresholdShift;
        public float thresholdParam;
        public float param;
        public float param2;
        public float param3;
        public float weight0 = 1f;
        public float weight1 = 1f;
        public float min;
        public float max = 1f;
        public uint editorHeightUnitMask = DEFAULT_HEIGHT_UNIT_MASK;
        public uint editorHeightPercentMask;

        public Action OnValueChanged;
        public float TerrainMaxHeight { get; private set; } = 1f;

        Toggle enabledToggle;
        bool stepEnabled = true;
        public bool StepEnabled {
            get => stepEnabled;
            set {
                stepEnabled = value;
                style.opacity = value ? 1f : 0.45f;
                if (enabledToggle != null) {
                    enabledToggle.SetValueWithoutNotify(value);
                }
            }
        }

        public TerrainStepNode(TerrainStepType operation, float terrainMaxHeight = 1f) {
            Operation = operation;
            TerrainMaxHeight = Mathf.Max(0.0001f, terrainMaxHeight);
            editorHeightUnitMask = DEFAULT_HEIGHT_UNIT_MASK;
            editorHeightPercentMask = 0u;
            AssignDefaultResources();
            AssignDefaultValues();
            title = GetOperationLabel(operation);
            Description = "";

            mainContainer.style.backgroundColor = nodeBodyColor;
            topContainer.style.backgroundColor = nodeBodyColor;
            inputContainer.style.backgroundColor = nodeBodyColor;
            outputContainer.style.backgroundColor = nodeBodyColor;
            extensionContainer.style.backgroundColor = nodeBodyColor;

            var color = GetCategoryColor(operation);
            titleContainer.style.backgroundColor = color;
            titleContainer.style.borderBottomColor = color * 0.7f;
            titleContainer.style.borderBottomWidth = 2;

            // Enabled toggle in title bar
            enabledToggle = new Toggle() { value = true, tooltip = "Enable/disable this step" };
            enabledToggle.style.marginRight = 4;
            enabledToggle.RegisterValueChangedCallback(evt => {
                StepEnabled = evt.newValue;
                OnValueChanged?.Invoke();
            });
            titleContainer.Insert(0, enabledToggle);

            // Output port (all nodes have one)
            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
            OutputPort.portName = "Out";
            outputContainer.Add(OutputPort);

            // Input ports based on operation category
            if (HasImplicitValueAndSingleRefOp(operation)) {
                InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(float));
                InputPort.portName = "Value";
                inputContainer.Add(InputPort);
                InputPortB = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(float));
                InputPortB.portName = operation == TerrainStepType.BeachMask ? "Mask" : "Ref";
                inputContainer.Add(InputPortB);
            } else if (IsImplicitValueOp(operation) || IsSingleRefOp(operation)) {
                InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(float));
                InputPort.portName = "In";
                inputContainer.Add(InputPort);
            } else if (HasTwoInputs(operation)) {
                InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(float));
                InputPort.portName = "A";
                inputContainer.Add(InputPort);
                InputPortB = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(float));
                InputPortB.portName = "B";
                inputContainer.Add(InputPortB);
            }
            // Generators have no input ports

            ApplyDocumentation();

            BuildParameterFields();
            RefreshExpandedState();
            RefreshPorts();
        }

        void AssignDefaultResources() {
            switch (Operation) {
                case TerrainStepType.SampleHeightMapTexture:
                case TerrainStepType.SampleRidgeNoiseFromTexture:
                    noiseTexture = Resources.Load<Texture2D>(DEFAULT_NOISE_TEXTURE_RESOURCE);
                    break;
            }
        }

        void AssignDefaultValues() {
            switch (Operation) {
                case TerrainStepType.Terraces:
                    octaves = 6;
                    param = 0.2f;
                    param2 = 1f;
                    break;
            }
        }

        public void SetTerrainMaxHeight(float terrainMaxHeight) {
            float clampedMaxHeight = Mathf.Max(0.0001f, terrainMaxHeight);
            if (Mathf.Approximately(TerrainMaxHeight, clampedMaxHeight)) return;
            TerrainMaxHeight = clampedMaxHeight;
            extensionContainer.Clear();
            BuildParameterFields();
            RefreshExpandedState();
        }

        void BuildParameterFields() {
            var container = new VisualElement();
            container.style.paddingLeft = 8;
            container.style.paddingRight = 8;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;
            container.style.minWidth = 180;

            switch (Operation) {
                case TerrainStepType.SampleHeightMapTexture:
                case TerrainStepType.SampleRidgeNoiseFromTexture:
                    AddObjectField<Texture2D>(container, "Noise Texture", noiseTexture, v => noiseTexture = v, GetFieldTooltip("Noise Texture"));
                    AddFloatField(container, "Frequency", frecuency, v => frecuency = v, GetFieldTooltip("Frequency"));
                    AddVector2Field(container, "Offset", offset, v => offset = v, GetFieldTooltip("Offset"));
                    AddHeightField(container, "Min", () => noiseRangeMin, v => noiseRangeMin = v, HeightUnitSlot.SamplerMin, GetFieldTooltip("Min"));
                    AddHeightField(container, "Max", () => noiseRangeMax, v => noiseRangeMax = v, HeightUnitSlot.SamplerMax, GetFieldTooltip("Max"));
                    break;

                case TerrainStepType.SampleHeightMapFractal:
                    AddFloatField(container, "Frequency", frecuency, v => frecuency = v, GetFieldTooltip("Frequency"));
                    AddIntField(container, "Octaves", octaves, v => octaves = Mathf.Clamp(v, 1, 8), GetFieldTooltip("Octaves"));
                    AddFloatField(container, "Persistence", persistence, v => persistence = v, GetFieldTooltip("Persistence"));
                    AddFloatField(container, "Lacunarity", lacunarity, v => lacunarity = v, GetFieldTooltip("Lacunarity"));
                    AddHeightField(container, "Min", () => noiseRangeMin, v => noiseRangeMin = v, HeightUnitSlot.SamplerMin, GetFieldTooltip("Min"));
                    AddHeightField(container, "Max", () => noiseRangeMax, v => noiseRangeMax = v, HeightUnitSlot.SamplerMax, GetFieldTooltip("Max"));
                    break;

                case TerrainStepType.SampleHeightMapUnityTerrain:
                    AddObjectField<TerrainData>(container, "Terrain Data", terrainData, v => terrainData = v, GetFieldTooltip("Terrain Data"));
                    AddFloatField(container, "Frequency", frecuency, v => frecuency = v, GetFieldTooltip("Frequency"));
                    AddVector2Field(container, "Offset", offset, v => offset = v, GetFieldTooltip("Offset"));
                    AddHeightField(container, "Min", () => noiseRangeMin, v => noiseRangeMin = v, HeightUnitSlot.SamplerMin, GetFieldTooltip("Min"));
                    AddHeightField(container, "Max", () => noiseRangeMax, v => noiseRangeMax = v, HeightUnitSlot.SamplerMax, GetFieldTooltip("Max"));
                    break;

                case TerrainStepType.Constant:
                    AddHeightField(container, "Value", () => param, v => param = v, HeightUnitSlot.ConstantValue, GetFieldTooltip("Value"));
                    break;

                case TerrainStepType.Shift:
                    AddHeightField(container, "Add", () => param, v => param = v, HeightUnitSlot.ShiftAdd, GetFieldTooltip("Add"));
                    break;

                case TerrainStepType.AddAndMultiply:
                    AddHeightField(container, "Add", () => param, v => param = v, HeightUnitSlot.AddAndMultiplyAdd, GetFieldTooltip("Add"));
                    AddFloatField(container, "Then Multiply", param2, v => param2 = v, GetFieldTooltip("Then Multiply"));
                    break;

                case TerrainStepType.MultiplyAndAdd:
                    AddFloatField(container, "Multiply", param, v => param = v, GetFieldTooltip("Multiply"));
                    AddHeightField(container, "Then Add", () => param2, v => param2 = v, HeightUnitSlot.MultiplyAndAddThenAdd, GetFieldTooltip("Then Add"));
                    break;

                case TerrainStepType.Exponential:
                    AddFloatField(container, "Exponent", param, v => param = v, GetFieldTooltip("Exponent"));
                    break;

                case TerrainStepType.Remap:
                    AddHeightField(container, "From Min", () => min, v => min = v, HeightUnitSlot.RemapFromMin, GetFieldTooltip("From Min"));
                    AddHeightField(container, "From Max", () => max, v => max = v, HeightUnitSlot.RemapFromMax, GetFieldTooltip("From Max"));
                    AddHeightField(container, "To Min", () => threshold, v => threshold = v, HeightUnitSlot.RemapToMin, GetFieldTooltip("To Min"));
                    AddHeightField(container, "To Max", () => thresholdParam, v => thresholdParam = v, HeightUnitSlot.RemapToMax, GetFieldTooltip("To Max"));
                    break;

                case TerrainStepType.Terraces:
                    AddIntField(container, "Steps", octaves, v => octaves = Mathf.Clamp(v, 2, 64), GetFieldTooltip("Steps"));
                    AddFloatField(container, "Smoothness", param, v => param = Mathf.Clamp01(v), GetFieldTooltip("Smoothness"));
                    AddFloatField(container, "Strength", param2, v => param2 = Mathf.Clamp01(v), GetFieldTooltip("Strength"));
                    break;

                case TerrainStepType.Island:
                    AddFloatField(container, "Radius", param, v => param = v, GetFieldTooltip("Radius"));
                    AddFloatField(container, "Slope Multiplier", param2, v => param2 = v, GetFieldTooltip("Slope Multiplier"));
                    break;

                case TerrainStepType.Threshold:
                    AddHeightField(container, "Threshold", () => threshold, v => threshold = v, HeightUnitSlot.Threshold, GetFieldTooltip("Threshold"));
                    AddHeightField(container, "If Greater, Add", () => thresholdShift, v => thresholdShift = v, HeightUnitSlot.ThresholdShift, GetFieldTooltip("If Greater, Add"));
                    AddHeightField(container, "If Not, Output", () => thresholdParam, v => thresholdParam = v, HeightUnitSlot.ThresholdFallback, GetFieldTooltip("If Not, Output"));
                    break;

                case TerrainStepType.FlattenOrRaise:
                    AddHeightField(container, "Min Elevation", () => threshold, v => threshold = v, HeightUnitSlot.FlattenOrRaiseMin, GetFieldTooltip("Min Elevation"));
                    AddFloatField(container, "Multiplier", thresholdParam, v => thresholdParam = v, GetFieldTooltip("Multiplier"));
                    break;

                case TerrainStepType.BeachMask:
                    AddFloatField(container, "Threshold", threshold, v => threshold = v, GetFieldTooltip("Threshold"));
                    break;

                case TerrainStepType.BlendAdditive:
                    AddFloatField(container, "Weight A", weight0, v => weight0 = v, GetFieldTooltip("Weight A"));
                    AddFloatField(container, "Weight B", weight1, v => weight1 = v, GetFieldTooltip("Weight B"));
                    break;

                case TerrainStepType.Clamp:
                    AddHeightField(container, "Min", () => min, v => min = v, HeightUnitSlot.ClampMin, GetFieldTooltip("Min"));
                    AddHeightField(container, "Max", () => max, v => max = v, HeightUnitSlot.ClampMax, GetFieldTooltip("Max"));
                    break;

                case TerrainStepType.Select:
                    AddHeightField(container, "Range Min", () => min, v => min = v, HeightUnitSlot.SelectMin, GetFieldTooltip("Range Min"));
                    AddHeightField(container, "Range Max", () => max, v => max = v, HeightUnitSlot.SelectMax, GetFieldTooltip("Range Max"));
                    AddHeightField(container, "Outside Value", () => thresholdParam, v => thresholdParam = v, HeightUnitSlot.SelectOutsideValue, GetFieldTooltip("Outside Value"));
                    break;

                case TerrainStepType.Fill:
                    AddHeightField(container, "Range Min", () => min, v => min = v, HeightUnitSlot.FillMin, GetFieldTooltip("Range Min"));
                    AddHeightField(container, "Range Max", () => max, v => max = v, HeightUnitSlot.FillMax, GetFieldTooltip("Range Max"));
                    AddHeightField(container, "Fill Value", () => thresholdParam, v => thresholdParam = v, HeightUnitSlot.FillValue, GetFieldTooltip("Fill Value"));
                    break;

                case TerrainStepType.Test:
                    AddHeightField(container, "Range Min", () => min, v => min = v, HeightUnitSlot.TestMin, GetFieldTooltip("Range Min"));
                    AddHeightField(container, "Range Max", () => max, v => max = v, HeightUnitSlot.TestMax, GetFieldTooltip("Range Max"));
                    break;

                // Random, Invert, Copy, Abs, BlendMultiply, Min, Max, Subtract, Divide: no extra params
            }

            if (container.childCount > 0) {
                extensionContainer.Add(container);
            }
        }

        void AddFloatField(VisualElement container, string label, float value, Action<float> setter, string tooltip = null) {
            var field = new FloatField(label) { value = value, tooltip = tooltip };
            field.style.minWidth = 160;
            field.RegisterValueChangedCallback(evt => {
                setter(evt.newValue);
                OnValueChanged?.Invoke();
            });
            ApplyFieldTooltip(field, tooltip);
            container.Add(field);
        }

        void AddHeightField(VisualElement container, string label, Func<float> getter, Action<float> setter, HeightUnitSlot slot, string tooltip = null) {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexEnd;
            row.style.width = Length.Percent(100);
            row.style.minWidth = 0;
            row.style.flexShrink = 1;

            string fieldTooltip = GetHeightFieldTooltip(tooltip);
            var unitField = new PopupField<string>(GetHeightUnitChoices(label), (int)GetHeightUnit(slot)) {
                tooltip = GetHeightUnitTooltip(label)
            };
            unitField.style.width = 112;
            unitField.style.minWidth = 0;
            unitField.style.flexShrink = 1;
            unitField.style.marginRight = 4;
            var unitLabel = unitField.Q<Label>();
            if (unitLabel != null) {
                unitLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            }
            row.Add(unitField);

            var field = new FloatField {
                value = ToDisplayHeightValue(getter(), GetHeightUnit(slot)),
                tooltip = fieldTooltip
            };
            field.style.flexGrow = 1;
            field.style.flexBasis = 0;
            field.style.flexShrink = 1;
            field.style.minWidth = 0;
            field.style.marginLeft = 2;
            field.RegisterValueChangedCallback(evt => {
                setter(ToStoredHeightValue(evt.newValue, GetHeightUnit(slot)));
                OnValueChanged?.Invoke();
            });
            ApplyFieldTooltip(field, fieldTooltip);
            row.Add(field);

            unitField.RegisterValueChangedCallback(evt => {
                SetHeightUnit(slot, ParseHeightUnitLabel(evt.newValue));
                field.SetValueWithoutNotify(ToDisplayHeightValue(getter(), GetHeightUnit(slot)));
                OnValueChanged?.Invoke();
            });

            container.Add(row);
        }

        void AddIntField(VisualElement container, string label, int value, Action<int> setter, string tooltip = null) {
            var field = new IntegerField(label) { value = value, tooltip = tooltip };
            field.style.minWidth = 160;
            field.RegisterValueChangedCallback(evt => {
                setter(evt.newValue);
                OnValueChanged?.Invoke();
            });
            ApplyFieldTooltip(field, tooltip);
            container.Add(field);
        }

        void AddVector2Field(VisualElement container, string label, Vector2 value, Action<Vector2> setter, string tooltip = null) {
            var field = new Vector2Field(label) { value = value, tooltip = tooltip };
            field.style.minWidth = 160;
            field.RegisterValueChangedCallback(evt => {
                setter(evt.newValue);
                OnValueChanged?.Invoke();
            });

            field.Query<FloatField>().ForEach(axisField => {
                axisField.style.flexGrow = 0f;
                axisField.style.minWidth = 36;
                axisField.style.maxWidth = 36;

                var axisLabel = axisField.Q<Label>();
                if (axisLabel != null) {
                    axisLabel.style.minWidth = 10;
                    axisLabel.style.maxWidth = 10;
                    axisLabel.style.marginRight = 2;
                    axisLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                    axisLabel.tooltip = tooltip;
                }

                ApplyFieldTooltip(axisField, tooltip);
            });

            ApplyFieldTooltip(field, tooltip);
            container.Add(field);
        }

        void AddObjectField<T>(VisualElement container, string label, UnityEngine.Object value, Action<T> setter, string tooltip = null) where T : UnityEngine.Object {
            var field = new ObjectField(label) {
                objectType = typeof(T),
                value = value,
                tooltip = tooltip
            };
            field.style.minWidth = 160;
            field.RegisterValueChangedCallback(evt => {
                setter((T)evt.newValue);
                OnValueChanged?.Invoke();
            });
            ApplyFieldTooltip(field, tooltip);
            container.Add(field);
        }

        void ApplyDocumentation() {
            string operationTooltip = GetOperationTooltip(Operation);
            tooltip = operationTooltip;
            titleContainer.tooltip = operationTooltip;
            if (enabledToggle != null) {
                enabledToggle.tooltip = $"Enable or disable this step.\n\n{operationTooltip}";
            }
            ApplyPortTooltip(OutputPort, GetOutputTooltip(Operation));
            ApplyPortTooltip(InputPort, GetPrimaryInputTooltip(Operation));
            ApplyPortTooltip(InputPortB, GetSecondaryInputTooltip(Operation));
        }

        void ApplyPortTooltip(Port port, string tooltipText) {
            if (port == null || string.IsNullOrEmpty(tooltipText)) return;
            port.tooltip = tooltipText;
            var label = port.Q<Label>();
            if (label != null) {
                label.tooltip = tooltipText;
            }
        }

        void ApplyFieldTooltip(VisualElement field, string tooltipText) {
            if (field == null || string.IsNullOrEmpty(tooltipText)) return;
            field.tooltip = tooltipText;
            var label = field.Q<Label>();
            if (label != null) {
                label.tooltip = tooltipText;
            }
        }

        string GetFieldTooltip(string label) {
            switch (Operation) {
                case TerrainStepType.SampleHeightMapTexture:
                case TerrainStepType.SampleRidgeNoiseFromTexture:
                    switch (label) {
                        case "Noise Texture": return "Texture source used for sampling. The red channel is used as the height/noise value.";
                        case "Frequency": return "Multiplier applied to world X/Z before sampling. Higher values make the pattern repeat more often.";
                        case "Offset": return "Offset added to the sampling coordinates before reading the source.";
                        case "Min": return "Lower bound of the remapped output range.";
                        case "Max": return "Upper bound of the remapped output range.";
                    }
                    break;
                case TerrainStepType.SampleHeightMapFractal:
                    switch (label) {
                        case "Frequency": return "Base frequency applied to the procedural fractal noise.";
                        case "Octaves": return "Number of procedural fractal layers combined together.";
                        case "Persistence": return "Amplitude multiplier applied from one octave to the next.";
                        case "Lacunarity": return "Frequency multiplier applied from one octave to the next.";
                        case "Min": return "Lower bound of the remapped output range.";
                        case "Max": return "Upper bound of the remapped output range.";
                    }
                    break;
                case TerrainStepType.SampleHeightMapUnityTerrain:
                    switch (label) {
                        case "Terrain Data": return "Unity Terrain heightmap asset to sample.";
                        case "Frequency": return "Multiplier applied to world X/Z before sampling. Higher values make the terrain data repeat more often.";
                        case "Offset": return "Offset added to the sampling coordinates before reading the heightmap.";
                        case "Min": return "Lower bound of the remapped output range.";
                        case "Max": return "Upper bound of the remapped output range.";
                    }
                    break;
                case TerrainStepType.Constant:
                    if (label == "Value") return "Constant value output by this node.";
                    break;
                case TerrainStepType.Shift:
                    if (label == "Add") return "Amount added to the incoming value.";
                    break;
                case TerrainStepType.AddAndMultiply:
                    if (label == "Add") return "Amount added before multiplication.";
                    if (label == "Then Multiply") return "Factor applied after adding the incoming value and Add.";
                    break;
                case TerrainStepType.MultiplyAndAdd:
                    if (label == "Multiply") return "Factor applied to the incoming value before adding Then Add.";
                    if (label == "Then Add") return "Amount added after multiplication.";
                    break;
                case TerrainStepType.Exponential:
                    if (label == "Exponent") return "Power applied to the incoming value. Negative inputs are clamped to 0 first.";
                    break;
                case TerrainStepType.Remap:
                    if (label == "From Min") return "Lower bound of the input range to remap from.";
                    if (label == "From Max") return "Upper bound of the input range to remap from.";
                    if (label == "To Min") return "Lower bound of the output range to remap into.";
                    if (label == "To Max") return "Upper bound of the output range to remap into.";
                    break;
                case TerrainStepType.Terraces:
                    if (label == "Steps") return "How many terrace bands are generated across the value range.";
                    if (label == "Smoothness") return "How soft the transitions are between one terrace and the next. 0 creates hard steps.";
                    if (label == "Strength") return "Blend between the original input and the terraced result.";
                    break;
                case TerrainStepType.Island:
                    if (label == "Radius") return "Island radius in world units. Outside this distance, height is reduced.";
                    if (label == "Slope Multiplier") return "How strongly terrain is lowered beyond Radius.";
                    break;
                case TerrainStepType.Threshold:
                    if (label == "Threshold") return "Comparison value against the referenced input.";
                    if (label == "If Greater, Add") return "Amount added to the referenced input when it is greater than or equal to Threshold.";
                    if (label == "If Not, Output") return "Fallback value used when the referenced input is below Threshold.";
                    break;
                case TerrainStepType.FlattenOrRaise:
                    if (label == "Min Elevation") return "Threshold above which the terrain is modified.";
                    if (label == "Multiplier") return "Slope multiplier applied above Min Elevation. 0 flattens to the threshold; 1 keeps the original slope.";
                    break;
                case TerrainStepType.BeachMask:
                    if (label == "Threshold") return "If Mask is above this normalized value, beach generation is disabled at that position.";
                    break;
                case TerrainStepType.BlendAdditive:
                    if (label == "Weight A") return "Multiplier applied to input A before summing.";
                    if (label == "Weight B") return "Multiplier applied to input B before summing.";
                    break;
                case TerrainStepType.Clamp:
                    if (label == "Min") return "Lower clamp bound for the incoming value.";
                    if (label == "Max") return "Upper clamp bound for the incoming value.";
                    break;
                case TerrainStepType.Select:
                    if (label == "Range Min") return "Lower bound of the accepted range.";
                    if (label == "Range Max") return "Upper bound of the accepted range.";
                    if (label == "Outside Value") return "Value output when the referenced input is outside the accepted range.";
                    break;
                case TerrainStepType.Fill:
                    if (label == "Range Min") return "Lower bound of the fill test range.";
                    if (label == "Range Max") return "Upper bound of the fill test range.";
                    if (label == "Fill Value") return "Value written when the referenced input falls inside the fill range.";
                    break;
                case TerrainStepType.Test:
                    if (label == "Range Min") return "Lower bound of the test range.";
                    if (label == "Range Max") return "Upper bound of the test range.";
                    break;
            }
            return null;
        }

        string GetHeightFieldTooltip(string baseTooltip) {
            string unitNote = $"Edit this value as normalized (typically 0..1), percentage (0..100), or meters. Percentage values are divided by 100, and meter values are divided by Max Height ({TerrainMaxHeight:0.##}) before the graph uses them.";
            if (string.IsNullOrEmpty(baseTooltip)) {
                return unitNote;
            }
            return $"{baseTooltip}\n\n{unitNote}";
        }

        List<string> GetHeightUnitChoices(string label) {
            return new List<string> {
                $"{label}: Normalized",
                $"{label}: Percentage",
                $"{label}: Meters"
            };
        }

        string GetHeightUnitTooltip(string label) {
            return $"Choose how to edit {label.ToLowerInvariant()}.\n\nNormalized keeps the raw graph value (typically 0..1).\nPercentage edits the same value in 0..100 form, where 100 means 100% of the terrain generator Max Height ({TerrainMaxHeight:0.##}).\nMeters converts using Max Height ({TerrainMaxHeight:0.##}).";
        }

        HeightValueUnit GetHeightUnit(HeightUnitSlot slot) {
            uint slotBit = 1u << (int)slot;
            if ((editorHeightPercentMask & slotBit) != 0) {
                return HeightValueUnit.Percentage;
            }
            return (editorHeightUnitMask & slotBit) != 0 ? HeightValueUnit.Meters : HeightValueUnit.Normalized;
        }

        void SetHeightUnit(HeightUnitSlot slot, HeightValueUnit unit) {
            uint bit = 1u << (int)slot;
            editorHeightUnitMask |= HEIGHT_UNIT_METADATA_PRESENT_BIT;
            if (unit == HeightValueUnit.Meters) {
                editorHeightUnitMask |= bit;
            } else {
                editorHeightUnitMask &= ~bit;
            }
            if (unit == HeightValueUnit.Percentage) {
                editorHeightPercentMask |= bit;
            } else {
                editorHeightPercentMask &= ~bit;
            }
            editorHeightUnitMask &= HEIGHT_UNIT_VALUE_MASK | HEIGHT_UNIT_METADATA_PRESENT_BIT;
            editorHeightPercentMask &= HEIGHT_UNIT_VALUE_MASK;
        }

        HeightValueUnit ParseHeightUnitLabel(string label) {
            if (label != null) {
                if (label.EndsWith("Meters", StringComparison.Ordinal)) return HeightValueUnit.Meters;
                if (label.EndsWith("Percentage", StringComparison.Ordinal)) return HeightValueUnit.Percentage;
            }
            return HeightValueUnit.Normalized;
        }

        float ToDisplayHeightValue(float storedValue, HeightValueUnit unit) {
            switch (unit) {
                case HeightValueUnit.Percentage:
                    return storedValue * 100f;
                case HeightValueUnit.Meters:
                    return storedValue * TerrainMaxHeight;
                default:
                    return storedValue;
            }
        }

        float ToStoredHeightValue(float displayValue, HeightValueUnit unit) {
            switch (unit) {
                case HeightValueUnit.Percentage:
                    return displayValue / 100f;
                case HeightValueUnit.Meters:
                    return displayValue / TerrainMaxHeight;
                default:
                    return displayValue;
            }
        }

        static uint NormalizeHeightUnitMask(uint storedMask) {
            if ((storedMask & HEIGHT_UNIT_METADATA_PRESENT_BIT) != 0) {
                return HEIGHT_UNIT_METADATA_PRESENT_BIT | (storedMask & HEIGHT_UNIT_VALUE_MASK);
            }

            // Legacy assets had no explicit metadata and defaulted to meters in the UI.
            return DEFAULT_HEIGHT_UNIT_MASK;
        }

        static uint NormalizeHeightPercentMask(uint storedMask) {
            return storedMask & HEIGHT_UNIT_VALUE_MASK;
        }

        public static uint GetNormalizedHeightUnitMask() {
            return HEIGHT_UNIT_METADATA_PRESENT_BIT;
        }

        public bool UsesMetersHeightUnits() {
            foreach (var slot in GetUsedHeightUnitSlots()) {
                if (GetHeightUnit(slot) == HeightValueUnit.Meters) {
                    return true;
                }
            }
            return false;
        }

        IEnumerable<HeightUnitSlot> GetUsedHeightUnitSlots() {
            switch (Operation) {
                case TerrainStepType.SampleHeightMapTexture:
                case TerrainStepType.SampleRidgeNoiseFromTexture:
                case TerrainStepType.SampleHeightMapFractal:
                case TerrainStepType.SampleHeightMapUnityTerrain:
                    yield return HeightUnitSlot.SamplerMin;
                    yield return HeightUnitSlot.SamplerMax;
                    yield break;
                case TerrainStepType.Constant:
                    yield return HeightUnitSlot.ConstantValue;
                    yield break;
                case TerrainStepType.Shift:
                    yield return HeightUnitSlot.ShiftAdd;
                    yield break;
                case TerrainStepType.AddAndMultiply:
                    yield return HeightUnitSlot.AddAndMultiplyAdd;
                    yield break;
                case TerrainStepType.MultiplyAndAdd:
                    yield return HeightUnitSlot.MultiplyAndAddThenAdd;
                    yield break;
                case TerrainStepType.Threshold:
                    yield return HeightUnitSlot.Threshold;
                    yield return HeightUnitSlot.ThresholdShift;
                    yield return HeightUnitSlot.ThresholdFallback;
                    yield break;
                case TerrainStepType.Remap:
                    yield return HeightUnitSlot.RemapFromMin;
                    yield return HeightUnitSlot.RemapFromMax;
                    yield return HeightUnitSlot.RemapToMin;
                    yield return HeightUnitSlot.RemapToMax;
                    yield break;
                case TerrainStepType.FlattenOrRaise:
                    yield return HeightUnitSlot.FlattenOrRaiseMin;
                    yield break;
                case TerrainStepType.Clamp:
                    yield return HeightUnitSlot.ClampMin;
                    yield return HeightUnitSlot.ClampMax;
                    yield break;
                case TerrainStepType.Select:
                    yield return HeightUnitSlot.SelectMin;
                    yield return HeightUnitSlot.SelectMax;
                    yield return HeightUnitSlot.SelectOutsideValue;
                    yield break;
                case TerrainStepType.Fill:
                    yield return HeightUnitSlot.FillMin;
                    yield return HeightUnitSlot.FillMax;
                    yield return HeightUnitSlot.FillValue;
                    yield break;
                case TerrainStepType.Test:
                    yield return HeightUnitSlot.TestMin;
                    yield return HeightUnitSlot.TestMax;
                    yield break;
            }
        }

        public void ApplyFromStepData(StepData data) {
            StepEnabled = data.enabled;
            Description = data.description ?? "";
            noiseTexture = Operation == TerrainStepType.SampleHeightMapFractal ? null : data.noiseTexture;
            terrainData = data.terrainData;
            frecuency = data.frecuency;
            offset = data.offset;
            noiseRangeMin = data.noiseRangeMin;
            noiseRangeMax = data.noiseRangeMax;
            octaves = data.octaves;
            persistence = data.persistence;
            lacunarity = data.lacunarity;
            threshold = data.threshold;
            thresholdShift = data.thresholdShift;
            thresholdParam = data.thresholdParam;
            param = data.param;
            param2 = data.param2;
            param3 = data.param3;
            weight0 = data.weight0;
            weight1 = data.weight1;
            min = data.min;
            max = data.max;
            editorHeightUnitMask = NormalizeHeightUnitMask(data.editorHeightUnitMask);
            editorHeightPercentMask = NormalizeHeightPercentMask(data.editorHeightPercentMask);

            // Rebuild UI fields with the loaded values
            extensionContainer.Clear();
            BuildParameterFields();
            RefreshExpandedState();
        }

        public void UpdateDisplayTitle() {
            string label = !string.IsNullOrEmpty(Description) ? Description : GetOperationLabel(Operation);
            title = StepIndex >= 0 ? $"[{StepIndex}] {label}" : label;
        }

        public StepData CollectStepData() {
            return new StepData {
                enabled = StepEnabled,
                operation = Operation,
                description = Description,
                noiseTexture = Operation == TerrainStepType.SampleHeightMapFractal ? null : noiseTexture,
                terrainData = terrainData,
                frecuency = frecuency,
                offset = offset,
                noiseRangeMin = noiseRangeMin,
                noiseRangeMax = noiseRangeMax,
                octaves = octaves,
                persistence = persistence,
                lacunarity = lacunarity,
                threshold = threshold,
                thresholdShift = thresholdShift,
                thresholdParam = thresholdParam,
                param = param,
                param2 = param2,
                param3 = param3,
                weight0 = weight0,
                weight1 = weight1,
                min = min,
                max = max,
                editorHeightUnitMask = HEIGHT_UNIT_METADATA_PRESENT_BIT | (editorHeightUnitMask & HEIGHT_UNIT_VALUE_MASK),
                editorHeightPercentMask = editorHeightPercentMask & HEIGHT_UNIT_VALUE_MASK
            };
        }

        // --- Classification helpers ---

        public static bool IsGenerator(TerrainStepType op) {
            switch (op) {
                case TerrainStepType.SampleHeightMapTexture:
                case TerrainStepType.SampleRidgeNoiseFromTexture:
                case TerrainStepType.SampleHeightMapFractal:
                case TerrainStepType.SampleHeightMapUnityTerrain:
                case TerrainStepType.Constant:
                case TerrainStepType.Random:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsImplicitValueOp(TerrainStepType op) {
            switch (op) {
                case TerrainStepType.Invert:
                case TerrainStepType.Shift:
                case TerrainStepType.AddAndMultiply:
                case TerrainStepType.MultiplyAndAdd:
                case TerrainStepType.Exponential:
                case TerrainStepType.Remap:
                case TerrainStepType.Abs:
                case TerrainStepType.Terraces:
                case TerrainStepType.Island:
                case TerrainStepType.FlattenOrRaise:
                case TerrainStepType.Clamp:
                    return true;
                default:
                    return false;
            }
        }

        public static bool HasImplicitValueAndSingleRefOp(TerrainStepType op) {
            switch (op) {
                case TerrainStepType.BeachMask:
                case TerrainStepType.Fill:
                    return true;
                default:
                    return false;
            }
        }

        public static bool UsesImplicitValueFlow(TerrainStepType op) {
            return IsImplicitValueOp(op) || HasImplicitValueAndSingleRefOp(op);
        }

        public static bool IsSingleRefOp(TerrainStepType op) {
            switch (op) {
                case TerrainStepType.Copy:
                case TerrainStepType.Threshold:
                case TerrainStepType.Select:
                case TerrainStepType.Test:
                    return true;
                default:
                    return false;
            }
        }

        public static bool HasTwoInputs(TerrainStepType op) {
            switch (op) {
                case TerrainStepType.BlendAdditive:
                case TerrainStepType.BlendMultiply:
                case TerrainStepType.Min:
                case TerrainStepType.Max:
                case TerrainStepType.Subtract:
                case TerrainStepType.Divide:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsBlendOp(TerrainStepType op) {
            switch (op) {
                case TerrainStepType.BlendAdditive:
                case TerrainStepType.BlendMultiply:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsMathOp(TerrainStepType op) {
            if (IsImplicitValueOp(op)) return true;
            switch (op) {
                case TerrainStepType.Min:
                case TerrainStepType.Max:
                case TerrainStepType.Subtract:
                case TerrainStepType.Divide:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsAltitudeOnlyOp(TerrainStepType op) {
            switch (op) {
                case TerrainStepType.BeachMask:
                case TerrainStepType.FlattenOrRaise:
                case TerrainStepType.Island:
                    return true;
                default:
                    return false;
            }
        }

        public static string GetOperationLabel(TerrainStepType op) {
            switch (op) {
                case TerrainStepType.SampleHeightMapTexture: return "Sample Height Map Texture";
                case TerrainStepType.SampleRidgeNoiseFromTexture: return "Sample Ridge Noise From Texture";
                case TerrainStepType.SampleHeightMapFractal: return "Sample Height Map Fractal";
                case TerrainStepType.SampleHeightMapUnityTerrain: return "Sample Height Map Unity Terrain";
                case TerrainStepType.Constant: return "Constant";
                case TerrainStepType.Copy: return "Copy";
                case TerrainStepType.Random: return "Random";
                case TerrainStepType.Invert: return "Invert";
                case TerrainStepType.Shift: return "Shift";
                case TerrainStepType.BeachMask: return "Beach Mask";
                case TerrainStepType.AddAndMultiply: return "Add And Multiply";
                case TerrainStepType.MultiplyAndAdd: return "Multiply And Add";
                case TerrainStepType.Exponential: return "Exponential";
                case TerrainStepType.Remap: return "Remap";
                case TerrainStepType.Abs: return "Abs";
                case TerrainStepType.Terraces: return "Terraces";
                case TerrainStepType.Threshold: return "Threshold";
                case TerrainStepType.FlattenOrRaise: return "Flatten Or Raise";
                case TerrainStepType.Island: return "Island";
                case TerrainStepType.BlendAdditive: return "Blend Additive";
                case TerrainStepType.BlendMultiply: return "Blend Multiply";
                case TerrainStepType.Clamp: return "Clamp";
                case TerrainStepType.Select: return "Select";
                case TerrainStepType.Fill: return "Fill";
                case TerrainStepType.Test: return "Test";
                case TerrainStepType.Min: return "Min";
                case TerrainStepType.Max: return "Max";
                case TerrainStepType.Subtract: return "Subtract";
                case TerrainStepType.Divide: return "Divide";
                default: return op.ToString();
            }
        }

        public static float EstimateNodeHeight(TerrainStepType op) {
            int fieldCount = 0;
            switch (op) {
                case TerrainStepType.SampleHeightMapTexture:
                case TerrainStepType.SampleRidgeNoiseFromTexture:
                case TerrainStepType.SampleHeightMapUnityTerrain:
                    fieldCount = 5;
                    break;
                case TerrainStepType.SampleHeightMapFractal:
                    fieldCount = 6;
                    break;
                case TerrainStepType.Constant:
                case TerrainStepType.Shift:
                case TerrainStepType.Exponential:
                case TerrainStepType.Remap:
                case TerrainStepType.Terraces:
                case TerrainStepType.Island:
                case TerrainStepType.FlattenOrRaise:
                case TerrainStepType.BeachMask:
                case TerrainStepType.BlendAdditive:
                case TerrainStepType.Clamp:
                case TerrainStepType.Test:
                    if (op == TerrainStepType.Remap) {
                        fieldCount = 4;
                    } else if (op == TerrainStepType.Terraces) {
                        fieldCount = 3;
                    } else {
                        fieldCount = 1 + (op == TerrainStepType.Island || op == TerrainStepType.BlendAdditive || op == TerrainStepType.Clamp || op == TerrainStepType.Test ? 1 : 0);
                    }
                    break;
                case TerrainStepType.AddAndMultiply:
                case TerrainStepType.MultiplyAndAdd:
                    fieldCount = 2;
                    break;
                case TerrainStepType.Threshold:
                case TerrainStepType.Select:
                case TerrainStepType.Fill:
                    fieldCount = 3;
                    break;
                default:
                    fieldCount = 0;
                    break;
            }

            // Title + ports + padding + per-field row height.
            return 58f + fieldCount * 24f;
        }

        public static string GetOperationTooltip(TerrainStepType op) {
            switch (op) {
                case TerrainStepType.SampleHeightMapTexture:
                    return "Samples a repeating 2D texture as a heightmap.\nThe sampled grayscale value is remapped into the configured Min/Max range.";
                case TerrainStepType.SampleRidgeNoiseFromTexture:
                    return "Samples a repeating 2D texture and converts it into ridge-style noise.\nThe result is remapped into the configured Min/Max range.";
                case TerrainStepType.SampleHeightMapFractal:
                    return "Builds procedural fractal noise.\nFrequency, octaves, persistence and lacunarity control the fractal shape.";
                case TerrainStepType.SampleHeightMapUnityTerrain:
                    return "Samples a Unity Terrain heightmap and remaps the sampled value into the configured Min/Max range.";
                case TerrainStepType.Constant:
                    return "Outputs a constant normalized value.";
                case TerrainStepType.Copy:
                    return "Copies the value from the referenced step.\nUseful for branching or explicitly reusing an earlier result.";
                case TerrainStepType.Random:
                    return "Outputs a deterministic pseudo-random value based on world X/Z coordinates.";
                case TerrainStepType.Invert:
                    return "Replaces the incoming value with 1 - input.";
                case TerrainStepType.Shift:
                    return "Adds a constant amount to the incoming value.";
                case TerrainStepType.BeachMask:
                    return "Keeps the main incoming altitude unchanged but disables beach generation where Mask is above Threshold.\nValue carries the terrain flow; Mask is only used to decide whether beaches are allowed.";
                case TerrainStepType.AddAndMultiply:
                    return "Computes (input + Add) * Then Multiply.";
                case TerrainStepType.MultiplyAndAdd:
                    return "Computes (input * Multiply) + Then Add.";
                case TerrainStepType.Exponential:
                    return "Raises the incoming value to the configured exponent.\nNegative inputs are clamped to 0 before exponentiation.";
                case TerrainStepType.Remap:
                    return "Linearly remaps the incoming value from the source range into the target range.\nThis remap is not clamped; chain Clamp if you want to limit the result.";
                case TerrainStepType.Abs:
                    return "Outputs the absolute value of the incoming input.";
                case TerrainStepType.Terraces:
                    return "Quantizes the incoming value into terrace bands, with optional smoothing between bands and strength blending with the original input.";
                case TerrainStepType.Threshold:
                    return "Reads the referenced input and compares it against Threshold.\nIf it passes, outputs input + If Greater, Add; otherwise outputs If Not, Output.";
                case TerrainStepType.FlattenOrRaise:
                    return "If input is above Min Elevation, adjusts the slope above that threshold by Multiplier.\nUse 0 to flatten to the threshold, 1 to keep the slope, and values above 1 to exaggerate it.";
                case TerrainStepType.Island:
                    return "Creates an island falloff by reducing terrain outside a given radius.\nRadius is in world units.";
                case TerrainStepType.BlendAdditive:
                    return "Outputs A * Weight A + B * Weight B.";
                case TerrainStepType.BlendMultiply:
                    return "Outputs A * B.";
                case TerrainStepType.Min:
                    return "Outputs the smaller of the two inputs.";
                case TerrainStepType.Max:
                    return "Outputs the larger of the two inputs.";
                case TerrainStepType.Subtract:
                    return "Outputs A - B.";
                case TerrainStepType.Divide:
                    return "Outputs A / B.\nIf B is zero or nearly zero, the node outputs 0 to avoid invalid values.";
                case TerrainStepType.Clamp:
                    return "Clamps the incoming value between Min and Max.";
                case TerrainStepType.Select:
                    return "Passes the referenced input through only when it is inside the configured range.\nOutside the range, outputs Outside Value.";
                case TerrainStepType.Fill:
                    return "If the referenced input is inside the configured range, outputs Fill Value.\nOtherwise it preserves the previous running value.";
                case TerrainStepType.Test:
                    return "Outputs 1 when the referenced input is inside the configured range, otherwise 0.";
                default:
                    return "Terrain graph operation.";
            }
        }

        public static string GetPrimaryInputTooltip(TerrainStepType op) {
            switch (op) {
                case TerrainStepType.Copy:
                case TerrainStepType.Threshold:
                case TerrainStepType.Select:
                case TerrainStepType.Test:
                    return "Referenced step read by this operation.";
                case TerrainStepType.BeachMask:
                    return "Main terrain value carried through this node.\nBeachMask does not replace this value; it only affects beach generation.";
                case TerrainStepType.Fill:
                    return "Main incoming value carried through this node.\nIf Ref is outside the configured range, this value passes through unchanged.";
                default:
                    if (HasTwoInputs(op)) {
                        return "Input A.";
                    }
                    if (IsImplicitValueOp(op)) {
                        return "Main incoming value transformed by this operation.";
                    }
                    return null;
            }
        }

        public static string GetSecondaryInputTooltip(TerrainStepType op) {
            switch (op) {
                case TerrainStepType.BeachMask:
                    return "Mask/reference value used to decide whether beaches are allowed.\nIf Mask is above Threshold, beaches are suppressed.";
                case TerrainStepType.Fill:
                    return "Referenced step checked against Range Min and Range Max.\nIf Ref is inside the range, the node outputs Fill Value.";
                default:
                    if (HasTwoInputs(op)) {
                        return "Input B.";
                    }
                    return null;
            }
        }

        public static string GetOutputTooltip(TerrainStepType op) {
            switch (op) {
                case TerrainStepType.BeachMask:
                    return "Pass-through terrain value after applying beach suppression rules.";
                case TerrainStepType.Copy:
                    return "Copied value from the referenced step.";
                case TerrainStepType.Threshold:
                    return "Value produced by the threshold comparison.";
                case TerrainStepType.Fill:
                    return "Either the incoming pass-through value or Fill Value, depending on the referenced range test.";
                case TerrainStepType.Test:
                    return "Boolean-style result as 0 or 1.";
                default:
                    return "Value produced by this step. Connect it to another node or to an output node.";
            }
        }

        public static string GetCategoryLabel(TerrainStepType op) {
            if (IsGenerator(op)) return "Samplers";
            if (IsMathOp(op)) return "Math";
            if (IsBlendOp(op)) return "Blending";
            return "Filters";
        }

        public static Color GetCategoryColor(TerrainStepType op) {
            if (IsGenerator(op)) return new Color(0.18f, 0.42f, 0.18f, 0.9f);            // green
            if (IsMathOp(op)) return new Color(0.55f, 0.35f, 0.1f, 0.9f);                 // orange
            if (IsBlendOp(op)) return new Color(0.35f, 0.18f, 0.5f, 0.9f);                // purple
            return new Color(0.5f, 0.45f, 0.1f, 0.9f);                                    // yellow
        }
    }
}
