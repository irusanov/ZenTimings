using ZenTimings.Export;

namespace ZenTimings.ViewModels
{
    public partial class MainViewModel
    {
        // Set by the export dialog right before GetJSON() is called, the defaults give the main window sections
        internal SnapshotSource ExportSource { get; set; }

        internal SnapshotOptions ExportOptions { get; set; }

        partial void ExportJson(ref string json)
        {
            json = SnapshotWriter.ToJson(SnapshotBuilder.Build(ExportSource, ExportOptions));
        }
    }
}
