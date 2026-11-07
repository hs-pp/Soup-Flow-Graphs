using System;
using Soup.Runtime.Blackboards;
using UnityEngine;
using UnityEngine.Events;

namespace Soup.Runtime
{
    public class FlowGraphController : MonoBehaviour
    {
        [SerializeField]
        private AFlowGraph m_flowGraph;
        
        [SerializeField]
        private bool m_useOverrides = false;
        [SerializeReference]
        private AGraphProperties m_overrideProperties = null;
        public GraphRunner GraphRunner { get; private set; }

        /// <summary>
        /// The current run's blackboard, or null before the first StartGraph.
        /// </summary>
        public Blackboard Blackboard => GraphRunner?.Blackboard;

        public UnityEvent OnGraphStart = null;
        public UnityEvent OnGraphStop = null;
        public UnityEvent<ANode> OnNodeChange = null;

        public void SetGraph(AFlowGraph flowGraph)
        {
            m_flowGraph = flowGraph;
        }

        /// <summary>
        /// The current run's graph properties, or null before the first StartGraph or when they aren't a T.
        /// </summary>
        public T GetGraphProperties<T>() where T : AGraphProperties
        {
            return GraphRunner?.GraphProperties as T;
        }

        /// <summary>
        /// Starts a fresh run with its own copies of the graph properties and the blackboard. To run with values from code, write
        /// them right after this returns: <c>StartGraph(); GetGraphProperties&lt;T&gt;().Speed = 2f; Blackboard.SetValue("Lives", 3);</c>
        /// Only the entry node has run by then, and it only queues its transition.
        /// </summary>
        public void StartGraph()
        {
            if(m_flowGraph == null)
            {
                Debug.LogError("No flow graph set!", this);
                return;
            }

            // Tear down any previous run before replacing it.
            StopGraph();

            AGraphProperties properties = ResolveGraphProperties();

            GraphRunner = new GraphRunner(m_flowGraph, properties, m_flowGraph.Blackboard?.Clone() ?? new Blackboard());
            GraphRunner.OnGraphStart += () => { OnGraphStart?.Invoke(); };
            GraphRunner.OnGraphStop += () => { OnGraphStop?.Invoke(); };
            GraphRunner.OnNodeChange += Node => { OnNodeChange?.Invoke(Node); };

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

                Debug.LogError($"[Soup] FlowGraphController: '{name}' has overrides of type '{m_overrideProperties?.GetType().Name ?? "none"}', but '{m_flowGraph.name}' uses '{graphPropertiesType?.Name ?? "none"}'. Running with the graph's own values.", this);
            }

            return m_flowGraph.GraphProperties?.Clone();
        }

        public void Update()
        {
            GraphRunner?.UpdateGraph();
        }

        public void StopGraph()
        {
            GraphRunner?.StopGraph();
        }

        private void OnDestroy()
        {
            StopGraph();
        }

#if UNITY_EDITOR
        internal const string FlowGraphFieldName = nameof(m_flowGraph);
        internal const string UseOverridesFieldName = nameof(m_useOverrides);
        internal const string OverridePropertiesFieldName = nameof(m_overrideProperties);
#endif
    }
}
