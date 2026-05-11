using UnityEditor.Experimental.GraphView;

namespace VoxelPlay {

    public enum TerrainGraphDiagnosticSeverity {
        Error = 0,
        Warning = 1,
        Info = 2
    }

    public sealed class TerrainGraphDiagnostic {
        public TerrainGraphDiagnosticSeverity Severity { get; }
        public string Title { get; }
        public string Message { get; }
        public GraphElement Target { get; }

        public TerrainGraphDiagnostic(TerrainGraphDiagnosticSeverity severity, string title, string message, GraphElement target = null) {
            Severity = severity;
            Title = title;
            Message = message;
            Target = target;
        }
    }
}
