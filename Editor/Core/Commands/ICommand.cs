using Soup.Editor.State;

namespace Soup.Editor.Core.Commands
{
    public interface ICommand { }

    public interface IGraphCommand : ICommand
    {
        string GetUndoName() => GetType().Name;

        void ExecuteCommand(LoadedFlowGraph.CommandScope scope);
    }
    
    public interface IWindowCommand : ICommand
    {
        void ExecuteCommand(GraphEditorState state, LoadedFlowGraph loadedFlowGraph);
    }
}
