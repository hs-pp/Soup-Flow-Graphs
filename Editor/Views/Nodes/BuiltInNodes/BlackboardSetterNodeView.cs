using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes.Blackboards;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Nodes.BuiltInNodes
{
    [CustomNodeView(typeof(BlackboardSetterNode))]
    public class BlackboardSetterNodeView : BaseHorizontalNodeView
    {
        private Label m_summaryLabel;

        protected override void OnAddExtensionsContents(VisualElement extensionsContainer)
        {
            m_summaryLabel = new Label { pickingMode = PickingMode.Ignore };
            extensionsContainer.Add(m_summaryLabel);
        }

        // Reads "Speed += 1". Runs after every edit to the node or the blackboard, so renames and deletes show up here.
        protected override void OnNodeDataChanged(NodeRef nodeRef)
        {
            base.OnNodeDataChanged(nodeRef);

            BlackboardRef blackboard = BlackboardRef.Find(nodeRef.NodeSP?.serializedObject);
            string variableId = new BlackboardKeyRef(nodeRef.NodeSP?.FindPropertyRelative(BlackboardSetterNode.VARIABLE_FIELD_NAME)).VariableId;
            if (string.IsNullOrEmpty(variableId))
            {
                BlackboardNodeViewUtils.SetSummary(m_summaryLabel, "No variable", true);
                return;
            }

            if (!blackboard.TryGetVariable(variableId, out BlackboardVariableRef variable))
            {
                BlackboardNodeViewUtils.SetSummary(m_summaryLabel, BlackboardNodeViewUtils.MISSING_VARIABLE_TEXT, true);
                return;
            }

            // Behaviour, not data: the label is the setter's to make, from fields that match the last apply.
            ABlackboardSetter setter = nodeRef.NodeSP.FindPropertyRelative(BlackboardSetterNode.SETTER_FIELD_NAME)?.managedReferenceValue as ABlackboardSetter;
            bool usable = setter != null && setter.ValueType == variable.GetValueType();
            BlackboardNodeViewUtils.SetSummary(m_summaryLabel, setter != null ? setter.GetLabel(variable.Name) : variable.Name, !usable);
        }
    }
}
