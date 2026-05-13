using EpsilonLib.Commands;

namespace CacheEditor.Components.TagTree.Commands
{
    [ExportCommand]
    public class ExtractAllModelsCommand : CommandDefinition
    {
        public override string Name => "TagTree.ExtractAllModels";

        public override string DisplayText => "Extract All Models...";
    }
}
