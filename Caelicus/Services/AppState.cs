﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Caelicus.Models;
using Caelicus.Models.Graph;
using Caelicus.Models.Vehicles;
using Caelicus.Simulation;
using Caelicus.Simulation.History;
using Microsoft.AspNetCore.Components;
using Newtonsoft.Json;

namespace Caelicus.Services
{
    public class AppState
    {
        // Graphs

        public List<JsonGraphRootObject> Graphs { get; private set; } = new List<JsonGraphRootObject>();

        public void UpdateGraphs(ComponentBase source, List<JsonGraphRootObject> graphs)
        {
            Graphs = graphs;
            NotifyStateChanged(source, nameof(Graphs));
        }


        // Vehicles

        public List<Tuple<Vehicle, bool, int, int, int>> Vehicles { get; set; } = new List<Tuple<Vehicle, bool, int, int, int>>();

        public void UpdateVehicles(ComponentBase source, List<Tuple<Vehicle, bool, int, int, int>> vehicles)
        {
            Vehicles = vehicles;
            NotifyStateChanged(source, nameof(Vehicles));
        }


        // Simulation
        public string SimulationUpdates { get; set; } = string.Empty;

        public void UpdateSimulationUpdates(ComponentBase source, string updates)
        {
            SimulationUpdates = updates;
            NotifyStateChanged(source, nameof(SimulationUpdates));
        }


        // Orders
        public int NumberOfOrders { get; set; } = 100;

        public void UpdateNumberOfOrders(ComponentBase source, int numberOfOrders)
        {
            NumberOfOrders = numberOfOrders;
            NotifyStateChanged(source, nameof(NumberOfOrders));
        }


        // Simulation Results / History

        public SimulationHistory CurrentSimulationHistory { get; set; } = new SimulationHistory(new SimulationParameters());

        public void UpdateSimulationHistory(ComponentBase source, SimulationHistory simulationHistory)
        {
            CurrentSimulationHistory = simulationHistory;
            NotifyStateChanged(source, nameof(CurrentSimulationHistory));
        }

        // Events

        public event Action<ComponentBase, string> StateChanged;

        private void NotifyStateChanged(ComponentBase source, string property) => StateChanged?.Invoke(source, property);
    }
}
