using UnityEditor.ProjectWindowCallback;

namespace Soup.Editor.ScriptTemplates
{
    /// <summary>
    /// Finishes Create > Soup > Node Script once the new file has been named in the Project window. It's a
    /// ScriptableObject, so it lives in a file of its own name.
    /// </summary>
    internal sealed class CreateNodeScriptAction : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            SoupScriptCreator.CreateNodeScriptFromProjectWindow(pathName);
        }
    }
}
