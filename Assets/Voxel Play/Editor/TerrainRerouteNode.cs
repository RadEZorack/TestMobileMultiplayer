using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;

namespace VoxelPlay {

    public class TerrainRerouteNode : Node {
        public const float NodeSize = 22.5f;
        const float DotSize = 15f;
        const float PortSize = 8f;
        const float DotVerticalOffset = 3f;

        public Port InputPort { get; private set; }
        public Port OutputPort { get; private set; }

        public TerrainRerouteNode() {
            title = string.Empty;
            capabilities &= ~Capabilities.Copiable;

            AddToClassList("vp-terrain-reroute");

            titleContainer.style.display = DisplayStyle.None;
            extensionContainer.style.display = DisplayStyle.None;
            topContainer.style.backgroundColor = Color.clear;
            inputContainer.style.backgroundColor = Color.clear;
            outputContainer.style.backgroundColor = Color.clear;
            inputContainer.style.position = Position.Absolute;
            outputContainer.style.position = Position.Absolute;
            inputContainer.style.left = 0;
            inputContainer.style.top = 0;
            outputContainer.style.left = 0;
            outputContainer.style.top = 0;
            inputContainer.style.width = NodeSize;
            inputContainer.style.height = NodeSize;
            outputContainer.style.width = NodeSize;
            outputContainer.style.height = NodeSize;
            inputContainer.style.flexGrow = 0f;
            outputContainer.style.flexGrow = 0f;
            inputContainer.style.paddingLeft = 0;
            inputContainer.style.paddingRight = 0;
            outputContainer.style.paddingLeft = 0;
            outputContainer.style.paddingRight = 0;
            inputContainer.style.paddingTop = 0;
            inputContainer.style.paddingBottom = 0;
            outputContainer.style.paddingTop = 0;
            outputContainer.style.paddingBottom = 0;
            inputContainer.style.justifyContent = Justify.FlexStart;
            outputContainer.style.justifyContent = Justify.FlexStart;
            mainContainer.style.backgroundColor = Color.clear;
            mainContainer.style.position = Position.Relative;
            mainContainer.style.borderLeftWidth = 0;
            mainContainer.style.borderRightWidth = 0;
            mainContainer.style.borderTopWidth = 0;
            mainContainer.style.borderBottomWidth = 0;
            mainContainer.style.paddingLeft = 0;
            mainContainer.style.paddingRight = 0;
            mainContainer.style.paddingTop = 0;
            mainContainer.style.paddingBottom = 0;

            style.minHeight = 0;
            style.width = NodeSize;
            style.height = NodeSize;
            style.minWidth = NodeSize;
            style.minHeight = NodeSize;
            style.maxWidth = NodeSize;
            style.maxHeight = NodeSize;

            var dot = new VisualElement {
                pickingMode = UnityEngine.UIElements.PickingMode.Ignore
            };
            dot.style.position = Position.Absolute;
            dot.style.left = (NodeSize - DotSize) * 0.5f;
            dot.style.top = (NodeSize - DotSize) * 0.5f + DotVerticalOffset;
            dot.style.width = DotSize;
            dot.style.height = DotSize;
            dot.style.backgroundColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            dot.style.borderLeftWidth = 2;
            dot.style.borderRightWidth = 2;
            dot.style.borderTopWidth = 2;
            dot.style.borderBottomWidth = 2;
            dot.style.borderLeftColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            dot.style.borderRightColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            dot.style.borderTopColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            dot.style.borderBottomColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            dot.style.borderTopLeftRadius = DotSize * 0.5f;
            dot.style.borderTopRightRadius = DotSize * 0.5f;
            dot.style.borderBottomLeftRadius = DotSize * 0.5f;
            dot.style.borderBottomRightRadius = DotSize * 0.5f;
            hierarchy.Add(dot);

            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(float));
            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
            ConfigurePort(InputPort);
            ConfigurePort(OutputPort);
            InputPort.tooltip = "Incoming connection for this reroute point.";
            OutputPort.tooltip = "Outgoing connection for this reroute point.";
            inputContainer.Add(InputPort);
            outputContainer.Add(OutputPort);

            RefreshExpandedState();
            RefreshPorts();
        }

        static void ConfigurePort(Port port) {
            if (port == null) return;
            port.portName = string.Empty;
            port.style.position = Position.Absolute;
            port.style.left = 0;
            port.style.top = 0;
            port.style.marginTop = 0;
            port.style.marginBottom = 0;
            port.style.minHeight = NodeSize;
            port.style.height = NodeSize;
            port.style.paddingLeft = 0;
            port.style.paddingRight = 0;
            port.style.paddingTop = 0;
            port.style.paddingBottom = 0;
            port.style.minWidth = NodeSize;
            port.style.width = NodeSize;
            port.style.flexGrow = 0;
            port.style.justifyContent = Justify.Center;
            port.style.alignItems = Align.Center;

            var label = port.Q<Label>();
            if (label != null) {
                label.style.display = DisplayStyle.None;
            }

            var connector = port.Q(className: "connector");
            if (connector != null) {
                connector.style.position = Position.Absolute;
                connector.style.left = (NodeSize - PortSize) * 0.5f;
                connector.style.top = (NodeSize - PortSize) * 0.5f;
                connector.style.marginTop = 0;
                connector.style.marginBottom = 0;
                connector.style.width = PortSize;
                connector.style.height = PortSize;
            }
        }
    }
}
