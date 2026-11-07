using System;
using Soup.Runtime.Blackboards;
using UnityEngine;
using UnityEngine.Events;

namespace Soup.Runtime
{
    /// <summary>
    /// Simple component to run a Soup flow graph.
    /// </summary>
    public class FlowGraphController : MonoBehaviour
    {
        [SerializeField]
        private AFlowGraph m_flowGraph;

        [SerializeField]
        private bool m_useOverrides = false;
        [SerializeReference]
        private AGraphProperties m_overrideProperties = null;

        [SerializeField]
        private bool m_runOnStart = false;

        public GraphRunner GraphRunner { get; private set; }

        public UnityEvent OnGraphStart = new();
        public UnityEvent OnGraphStop = new();
        public UnityEvent<ANode> OnActiveNodeChange = new();

        private void Start()
        {
            if (m_runOnStart)
            {
                StartGraph();
            }
        }

        private void Update()
        {
            GraphRunner?.UpdateGraph();
        }

        private void OnDestroy()
        {
            StopGraph();
        }

        public void SetGraph(AFlowGraph flowGraph, AGraphProperties overrideProperties = null)
        {
            m_flowGraph = flowGraph;
            m_useOverrides = overrideProperties != null;
            m_overrideProperties = overrideProperties;
        }

        public void StartGraph()
        {
            if (m_flowGraph == null)
            {
                Debug.LogError("No flow graph set!", this);
                return;
            }

            if (GraphRunner != null && GraphRunner.IsRunning)
            {
                Debug.LogError("Flow graph is already running!", this);
                return;
            }

            AGraphProperties properties = ResolveGraphProperties();
            Blackboard blackboard = m_flowGraph.Blackboard?.Clone();
            if (blackboard == null)
            {
                blackboard = new Blackboard();
            }
            GraphRunner = new GraphRunner(m_flowGraph.RootCollection, properties, blackboard);
            GraphRunner.OnGraphStart += () => { OnGraphStart?.Invoke(); };
            GraphRunner.OnGraphStop += () => { OnGraphStop?.Invoke(); };
            GraphRunner.OnActiveNodeChanged += node => { OnActiveNodeChange?.Invoke(node); };

            GraphRunner.StartGraph();
        }

        private AGraphProperties ResolveGraphProperties()
        {
            if (m_useOverrides)
            {
                Type graphPropertiesType = m_flowGraph.GraphProperties?.GetType();
                if (m_overrideProperties != null && m_overrideProperties.GetType() == graphPropertiesType)
                {
                    return m_overrideProperties.Clone();
                }

                Debug.LogError($"'{name}' has overrides of type '{m_overrideProperties?.GetType().Name ?? "none"}', but '{m_flowGraph.name}' uses '{graphPropertiesType?.Name ?? "none"}'. Running with the graph's own values.", this);
            }

            // Otherwise just clone the defaults from the FlowGraph Asset.
            return m_flowGraph.GraphProperties?.Clone();
        }

        public void StopGraph()
        {
            GraphRunner?.StopGraph();
        }

        /** For editor use. */
#if UNITY_EDITOR
        internal const string FLOW_GRAPH_FIELD_NAME = nameof(m_flowGraph);
        internal const string RUN_ON_START_FIELD_NAME = nameof(m_runOnStart);
        internal const string USE_OVERRIDES_FIELD_NAME = nameof(m_useOverrides);
        internal const string OVERRIDE_PROPERTIES_FIELD_NAME = nameof(m_overrideProperties);
#endif
    }
}
