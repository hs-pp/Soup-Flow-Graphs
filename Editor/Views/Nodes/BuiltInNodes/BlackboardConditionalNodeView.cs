using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes.Blackboards;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Nodes.BuiltInNodes
{
    /// <summary>
    /// The variable under test. Each condition's outport is labelled by the condition itself, through DefineOutports.
    /// </summary>
    [CustomNodeView(typeof(BlackboardConditionalNode))]
    public class BlackboardConditionalNodeView : BaseHorizontalNodeView
    {
        private Label m_summaryLabel;

        protected override void OnAddExtensionsContents(VisualElement extensionsContainer)
        {
            m_summaryLabel = new Label { pickingMode = PickingMode.Ignore };
            extensionsContainer.Add(m_summaryLabel);
        }

        protected override void OnNodeDataChanged(NodeRef nodeRef)
        {
            base.OnNodeDataChanged(nodeRef);

            BlackboardRef blackboard = BlackboardRef.Find(nodeRef.NodeSP?.serializedObject);
            string variableId = new BlackboardKeyRef(nodeRef.NodeSP?.FindPropertyRelative(BlackboardConditionalNode.VARIABLE_FIELD_NAME)).VariableId;
            if (string.IsNullOrEmpty(variableId))
            {
                BlackboardNodeViewUtils.SetSummary(m_summaryLabel, "No variable", true);
            }
            else if (!blackboard.TryGetVariable(variableId, out BlackboardVariableRef variable))
            {
                BlackboardNodeViewUtils.SetSummary(m_summaryLabel, BlackboardNodeViewUtils.MISSING_VARIABLE_TEXT, true);
            }
            else
            {
                BlackboardNodeViewUtils.SetSummary(m_summaryLabel, variable.Name, false);
            }
        }
    }
}
