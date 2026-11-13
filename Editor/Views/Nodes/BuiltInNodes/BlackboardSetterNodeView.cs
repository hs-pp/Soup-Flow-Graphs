using Soup.Editor.Core.DataRefs;
using Soup.Runtime.BuiltInNodes.Blackboards;
using UnityEngine.UIElements;

namespace Soup.Editor.Views.Nodes.BuiltInNodes
{
    [CustomNodeView(typeof(BlackboardSetterNode))]
    public class BlackboardSetterNodeView : BaseHorizontalNodeView
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

            BlackboardRef blackboard = new(nodeRef.NodeSP?.serializedObject);
            string variableId = new BlackboardKeyRef(nodeRef.NodeSP?.FindPropertyRelative(BlackboardSetterNode.VARIABLE_FIELD_NAME)).VariableId;
            if (string.IsNullOrEmpty(variableId))
            {
                SetSummary(m_summaryLabel, "No variable", true);
                return;
            }

            if (!blackboard.TryGetVariable(variableId, out BlackboardVariableRef variable))
            {
                SetSummary(m_summaryLabel, "Missing variable", true);
                return;
            }

            ABlackboardSetter setter = nodeRef.NodeSP.FindPropertyRelative(BlackboardSetterNode.SETTER_FIELD_NAME)?.managedReferenceValue as ABlackboardSetter;
            bool usable = setter != null && setter.ValueType == variable.GetValueType();
            SetSummary(m_summaryLabel, setter != null ? setter.GetLabel(variable.Name) : variable.Name, !usable);
        }
        
        private void SetSummary(Label label, string text, bool isWarning)
        {
            label.text = isWarning ? "⚠ " + text : text;
            label.EnableInClassList(WARNING_CLASS, isWarning);
        }
    }
}
