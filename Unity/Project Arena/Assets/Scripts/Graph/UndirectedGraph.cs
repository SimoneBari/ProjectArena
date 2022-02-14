using System;
using System.Collections.Generic;
using System.Linq;
using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.RankedShortestPath;
using QuikGraph.Algorithms.ShortestPath;
using UnityEngine;

namespace Graph
{
    public readonly struct NodeProperties
    {
        private readonly Dictionary<string, object> properties;

        public NodeProperties(Dictionary<string, object> properties)
        {
            this.properties = properties;
        }

        public object this[string key]
        {
            get => properties.TryGetValue(key, out var rtn) ? rtn : null;
            set => properties[key] = value;
        }
    }

    /// <summary>
    /// Represents a undirected graphs. Wraps many functionalities of Quikgraph under an easier to use interface.
    /// </summary>
    public class UndirectedGraph
    {
        private readonly BidirectionalGraph<int, TaggedEdge<int, float>> graph =
            new BidirectionalGraph<int, TaggedEdge<int, float>>();

        private readonly Dictionary<int, NodeProperties> nodeProperties = new Dictionary<int, NodeProperties>();

        /// <summary>
        /// Adds a node with given id to the graph, if one isn't already present
        /// </summary>
        /// <param name="id">The unique id of the node</param>
        /// <param name="attributes">The list of attributes of the node</param>
        /// <returns>True if the node was added successfully</returns>
        public bool AddNode(int id, params Tuple<string, object>[] attributes)
        {
            if (graph.ContainsVertex(id)) return false;
            var attribDictionary = attributes.ToDictionary(key => key.Item1, value => value.Item2);
            graph.AddVertex(id);
            nodeProperties.Add(id, new NodeProperties(attribDictionary));
            return true;
        }

        /// <summary>
        /// Adds edge connecting nodeAID and nodeBID, if they both exist
        /// </summary>
        /// <param name="nodeAID">The unique id of first node to link</param>
        /// <param name="nodeBID">The unique id of the second node to link</param>
        /// <param name="weight">The weight of the edge</param>
        /// <returns>True if the edge was added successfully</returns>
        public bool AddEdge(int nodeAID, int nodeBID, float weight = 1f)
        {
            return graph.AddEdge(new TaggedEdge<int, float>(nodeAID, nodeBID, weight))
                   & graph.AddEdge(new TaggedEdge<int, float>(nodeBID, nodeAID, weight));
        }

        /// <summary>
        /// Returns whether the graph contains a node with the given id.
        /// </summary>
        public bool HasNode(int id)
        {
            return graph.ContainsVertex(id);
        }

        /// <summary>
        /// Returns an array containing all the ids of the nodes in the graph.
        /// </summary>
        /// <returns></returns>
        public int[] GetNodeIDs()
        {
            return graph.Vertices.ToArray();
        }

        /// <summary>
        /// Gets the id of nodes adjacent to the given one.
        /// </summary>
        public IEnumerable<int> GetAdjacentNodes(int nodeID)
        {
            return graph.OutEdges(nodeID).Select(it => it.Target);
        }

        /// <summary>
        /// Gets the in/out degree of a node.
        /// </summary>
        public int GetNodeDegree(int nodeID)
        {
            return graph.OutDegree(nodeID);
        }

        /// <summary>
        /// Returns the properties stored in the node with the given id.
        /// </summary>
        public NodeProperties GetNodeProperties(int nodeID)
        {
            if (!nodeProperties.ContainsKey(nodeID)) Debug.LogError("!?");
            return nodeProperties[nodeID];
        }

        private static double GetEdgeWeightFunc(TaggedEdge<int, float> edge)
        {
            return edge.Tag;
        }

        /// <summary>
        /// Calculates the lenght of the shortest path connecting source and target.
        /// </summary>
        public float CalculateShortestPathLenght(int source, int target)
        {
            var a = new DijkstraShortestPathAlgorithm<int, TaggedEdge<int, float>>(graph, GetEdgeWeightFunc);
            a.Compute(source);
            return a.TryGetDistance(target, out var rtn) ? (float) rtn : float.MaxValue;
        }

        /// <summary>
        /// Calculates the lenght of the shortest path connecting source and target and the betweeness of each node.
        /// </summary>
        public float CalculateShortestPathLenghtAndBetweeness(
            int source,
            int target,
            Dictionary<int, float> betweeness,
            int kPaths = 1
        )
        {
            // TODO using ranked algorithms to find possible different shortest paths algorithms.
            //  The algorithms tries k different paths, and I filter out those that are not the shortest.
            //  If, in some map, there are more than k shortest paths, I do not consider them, so the
            //  betweeness centrality calculated will be wrong.

            if (kPaths == 1)
            {
                var dijkstra = graph.ShortestPathsDijkstra(GetEdgeWeightFunc, source);
                if (dijkstra(target, out var path))
                {
                    var pathAsList = path.ToList();
                    var pathLength = pathAsList.Sum(it => it.Tag);
                    for (var i = 0; i < pathAsList.Count - 1; i++) betweeness[pathAsList[i].Target]++;
                    return pathLength;
                }

                return float.MaxValue;
            }

            var hoffPav = new HoffmanPavleyRankedShortestPathAlgorithm<int, TaggedEdge<int, float>>(graph,
                GetEdgeWeightFunc) {ShortestPathCount = kPaths};

            hoffPav.Compute(source, target);

            var minDistance = float.MaxValue;
            var foundValue = false;
            foreach (var algPath in hoffPav.ComputedShortestPaths)
            {
                var pathList = algPath.ToList();
                var sum = pathList.Sum(it => it.Tag);
                if (!foundValue)
                {
                    foundValue = true;
                    minDistance = sum;
                }
                else if (sum > minDistance)
                {
                    // TODO are the paths returned in lenght order?
                    break;
                }
                else
                {
                    Debug.LogError("This map has more than one shortest path between nodes!");
                }

                // DO not consider final vertex in path
                pathList.RemoveAt(pathList.Count - 1);
                pathList.ForEach(it => { betweeness[it.Target] = betweeness[it.Target] + 1f / pathList.Count; });
            }

            return minDistance;
        }

        /// <summary>
        /// Returns the edges coming out of a node.
        /// </summary>
        public IEnumerable<Tuple<int, float>> GetOutEdges(int node)
        {
            return graph.OutEdges(node).Select(it => new Tuple<int, float>(it.Target, it.Tag));
        }

        /// <summary>
        /// Removes the node with the given id.
        /// </summary>
        /// <returns>True if the node with the given id was found and removed, false otherwise.</returns>
        public bool RemoveNode(int it)
        {
            return graph.RemoveVertex(it);
        }
    }
}