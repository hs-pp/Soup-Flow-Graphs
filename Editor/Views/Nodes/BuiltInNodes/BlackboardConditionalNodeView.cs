using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes.Blackboards;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Nodes.BuiltInNodes
{
    [CustomNodeView(typeof(BlackboardConditionalNode))]
    public class BlackboardConditionalNodeView : BaseHorizontalNodeView
    {
        private const string WARNING_CLASS = "nodeWarningLabel";
        
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
                SetSummary(m_summaryLabel, "No variable", true);
            }
            else if (!blackboard.TryGetVariable(variableId, out BlackboardVariableRef variable))
            {
                SetSummary(m_summaryLabel, "Missing variable", true);
            }
            else
            {
                SetSummary(m_summaryLabel, variable.Name, false);
            }
        }
        
        private void SetSummary(Label label, string text, bool isWarning)
        {
            label.text = isWarning ? "⚠ " + text : text;
            label.EnableInClassList(WARNING_CLASS, isWarning);
        }
    }
}
