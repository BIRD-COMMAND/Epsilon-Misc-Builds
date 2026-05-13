using EpsilonLib.Commands;

namespace CacheEditor.Components.TagTree.Commands
{
    [ExportCommand]
    public class ExportAssCommand : CommandDefinition
    {
        public override string Name => "TagTree.ExportAss";

        public override string DisplayText => "Export ASS...";
    }
}
