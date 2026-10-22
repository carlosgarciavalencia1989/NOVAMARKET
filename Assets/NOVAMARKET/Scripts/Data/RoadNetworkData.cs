using System;
using System.Collections.Generic;
using NovaMarket.Simulation.Ambient;
using UnityEngine;
using Num = System.Numerics;

namespace NovaMarket.Data
{
    /// <summary>
    /// The road network of a scene, exported by the environment builder (NOVAMARKET → Ambient →
    /// Setup Ambient Traffic). Stores the plain <see cref="RoadNetworkDefinition"/>; do not edit by hand.
    /// </summary>
    [CreateAssetMenu(menuName = MenuRoot + "Road Network", fileName = "RoadNetwork_")]
    public sealed class RoadNetworkData : NovaDataAsset
    {
        [Serializable]
        private struct Node
        {
            public Vector2 position;
            public RoadNodeKind kind;
            public bool signalized;
        }

        [Serializable]
        private struct Road
        {
            public int from;
            public int to;
            public RoadClass roadClass;
            public bool twoWay;
            public float halfWidth;
            public float speedLimit;
            public float centreShift;
        }

        [Serializable]
        private struct Marker
        {
            public Vector2 point;
            public Vector2 direction;
            public float halfLength;
        }

        [SerializeField] private Node[] nodes = new Node[0];
        [SerializeField] private Road[] roads = new Road[0];
        [SerializeField] private Marker[] busStops = new Marker[0];
        [SerializeField] private Marker[] keepClear = new Marker[0];
        [SerializeField] private Marker[] crosswalks = new Marker[0];

        public int NodeCount => nodes.Length;
        public int RoadCount => roads.Length;

        public RoadNetworkDefinition ToDefinition()
        {
            var definition = new RoadNetworkDefinition();
            foreach (Node node in nodes) definition.AddNode(ToNum(node.position), node.kind, node.signalized);
            foreach (Road road in roads)
                definition.AddRoad(road.from, road.to, road.roadClass, road.twoWay, road.halfWidth, road.speedLimit, road.centreShift);
            foreach (Marker marker in busStops) definition.BusStops.Add(ToDefinition(marker));
            foreach (Marker marker in keepClear) definition.KeepClear.Add(ToDefinition(marker));
            foreach (Marker marker in crosswalks) definition.Crosswalks.Add(ToDefinition(marker));
            return definition;
        }

        /// <summary>Replaces the stored network (used by the Editor exporter).</summary>
        public void SetDefinition(RoadNetworkDefinition definition)
        {
            var nodeList = new List<Node>();
            foreach (RoadNodeDefinition node in definition.Nodes)
                nodeList.Add(new Node { position = ToUnity(node.Position), kind = node.Kind, signalized = node.Signalized });
            var roadList = new List<Road>();
            foreach (RoadDefinition road in definition.Roads)
                roadList.Add(new Road
                {
                    from = road.From, to = road.To, roadClass = road.Class, twoWay = road.TwoWay,
                    halfWidth = road.HalfWidth, speedLimit = road.SpeedLimit, centreShift = road.CentreShift
                });
            nodes = nodeList.ToArray();
            roads = roadList.ToArray();
            busStops = definition.BusStops.ConvertAll(ToMarker).ToArray();
            keepClear = definition.KeepClear.ConvertAll(ToMarker).ToArray();
            crosswalks = definition.Crosswalks.ConvertAll(ToMarker).ToArray();
        }

        private static LaneMarkerDefinition ToDefinition(Marker marker) =>
            new LaneMarkerDefinition(ToNum(marker.point), ToNum(marker.direction), marker.halfLength);

        private static Marker ToMarker(LaneMarkerDefinition marker) => new Marker
        {
            point = ToUnity(marker.Point), direction = ToUnity(marker.Direction), halfLength = marker.HalfLength
        };

        private static Num.Vector2 ToNum(Vector2 v) => new Num.Vector2(v.x, v.y);
        private static Vector2 ToUnity(Num.Vector2 v) => new Vector2(v.X, v.Y);
    }
}
