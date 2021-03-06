﻿using System.Collections.Generic;

namespace SimulationCore.Models.Graph
{
    public class JsonVertex
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<JsonEdge> Edges { get; set; }
    }
}
