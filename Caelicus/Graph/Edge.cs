using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Caelicus.Graph
{
    public class Edge<TVertex, TEdge>
    {
        public Guid Id { get; }

        public TEdge Info { get; set; }

        public Vertex<TVertex, TEdge> Origin { get; internal set; }

        public Vertex<TVertex, TEdge> Destination { get; internal set; }

        internal Edge(TEdge info)
        {
            Id = Guid.NewGuid();
            Info = info;
        }
    }
}
