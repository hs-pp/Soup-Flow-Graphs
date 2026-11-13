using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace Soup.Editor.ScriptTemplates
{
    internal sealed class CreateNodeScriptAction : AssetCreationEndAction
    {
        public override void Action(EntityId entityId, string pathName, string resourceFile)
        {
            SoupScriptCreator.CreateNodeScriptFromProjectWindow(pathName);
        }
    }
}
