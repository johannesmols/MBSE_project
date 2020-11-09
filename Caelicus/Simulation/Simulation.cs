using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Caelicus.Enums;
using Caelicus.Graph;
using Caelicus.Helpers;
using Caelicus.Models.Graph;
using Caelicus.Models.Vehicles;
using Caelicus.Simulation.History;
using GeoCoordinatePortable;
using Newtonsoft.Json;

namespace Caelicus.Simulation
{
    public class Simulation
    {
        public IProgress<SimulationProgress> ProgressReporter { get; private set; }
        public readonly SimulationParameters Parameters;
        public readonly SimulationHistory SimulationHistory;
        public List<VehicleInstance> Vehicles { get; } = new List<VehicleInstance>();
        public List<Order> OpenOrders { get; } = new List<Order>();
        public List<CompletedOrder> ClosedOrders { get; set; } = new List<CompletedOrder>();

        public readonly double SecondsPerSimulationStep;
        public int SimulationStep { get; private set; }


        /// <summary>
        /// Generates list of vehicles and orders based on the simulation parameters
        /// </summary>
        /// <param name="parameters">simulation parameters</param>
        public Simulation(SimulationParameters parameters)
        {
            Parameters = parameters;
            SimulationHistory = new SimulationHistory(parameters);
            SecondsPerSimulationStep = 1d / Parameters.SimulationSpeed;

            var allBases = Parameters.Graph.Vertices.Where(v => v.Info.Type == VertexType.Base).ToList();
            var allTargets = Parameters.Graph.Vertices.Where(v => v.Info.Type == VertexType.Target).ToList();

            // Equally split vehicles up to base stations
            var currentBaseIndex = 0;
            for (var i = 0; i < Parameters.NumberOfVehicles; i++)
            {
                if (currentBaseIndex == allBases.Count)
                {
                    currentBaseIndex = 0;
                }

                Vehicles.Add(new VehicleInstance(this, Parameters.VehicleTemplate, allBases[new Random(Parameters.RandomSeed + i).Next(allBases.Count)]));
                currentBaseIndex++;
            }

            // Generate random orders
            for (var i = 0; i < Parameters.NumberOfOrders; i++)
            {
                // TODO: Generate semi-random payload weight
                OpenOrders.Add(new Order(allBases[new Random(Parameters.RandomSeed + i).Next(allBases.Count)], allTargets[new Random(Parameters.RandomSeed + i).Next(allTargets.Count)], 10));
            }
        }

        /// <summary>
        /// Main thread of running the simulation, posting updates, and finally returning the results
        /// </summary>
        /// <param name="progress">Can be used to send status updates back to the UI</param>
        /// <param name="cancellationToken">Can be used to cancel the operation from the UI</param>
        /// <returns></returns>
        public async Task<SimulationHistory> Simulate(IProgress<SimulationProgress> progress, CancellationToken cancellationToken)
        {
            ProgressReporter = progress;
            ProgressReporter.Report(new SimulationProgress(Parameters.SimulationIdentifier, $"Starting simulation with  { Parameters.NumberOfVehicles } { Parameters.VehicleTemplate.Name }"));

            while (!IsDone())
            {
                ProgressReporter.Report(new SimulationProgress(Parameters.SimulationIdentifier, 
                    $"Simulating at step { SimulationStep }: " +
                    $"({ OpenOrders.Count } open orders, " +
                    $"{ ClosedOrders.Count } closed orders, " +
                    $"{ Vehicles.Where(v => v.CurrentOrder != null && v.State == VehicleState.MovingToTarget).ToList().Count } orders in progress, " +
                    $"{ Vehicles.Where(v => v.CurrentOrder != null && v.State == VehicleState.PickingUpOrder).ToList().Count } in pickup)"));

                // Record the current state of the simulation
                RecordSimulationStep();

                // Advance the simulation by one step
                Advance();

                // Wait for an amount of time corresponding to the simulation speed (e.g. speed of 1 = 1 step per second, speed of 2 = 2 steps per second, ...)
                await Task.Delay((int) (SecondsPerSimulationStep * 1000));

                // Use this snippet to repeatedly check for cancellation in each iteration of the simulation
                if (cancellationToken.IsCancellationRequested)
                {
                    ProgressReporter.Report(new SimulationProgress(Parameters.SimulationIdentifier, $"Stopped simulation with { Parameters.NumberOfVehicles } { Parameters.VehicleTemplate.Name }"));
                    throw new TaskCanceledException();
                }
            }

            ProgressReporter.Report(new SimulationProgress(Parameters.SimulationIdentifier, $"Finished simulation with { Parameters.NumberOfVehicles } { Parameters.VehicleTemplate.Name }"));

            return SimulationHistory;
        }

        /// <summary>
        /// Determines whether all orders have been fulfilled successfully
        /// </summary>
        /// <returns></returns>
        public bool IsDone()
        {
            if (OpenOrders.Count == 0 && Vehicles.All(v => v.CurrentOrder == null))
            {
                return true;
            }

            // Check whether there are any open orders that can not be completed because the distance is too great, or because the payload exceeds the vehicles maximum
            if (OpenOrders.Count > 0)
            {
                if (Vehicles.All(v => v.State == VehicleState.Idle))
                {
                    foreach (var order in OpenOrders)
                    {
                        var vehicleTypeValues = Vehicles.First();
                        var maxTravelDistance = vehicleTypeValues.GetMaximumTravelDistance(order.PayloadWeight);
                        var orderTravelDistance = Parameters.Graph.FindShortestPath(Parameters.Graph, order.Start, order.Target).Item2;
                        if (orderTravelDistance > maxTravelDistance || order.PayloadWeight > vehicleTypeValues.MaxPayload)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Advance the simulation by a single step
        /// </summary>
        public void Advance()
        {
            // Assign open orders to any available vehicle
            foreach (var vehicle in Vehicles.Where(vehicle => vehicle.State == VehicleState.Idle))
            {
                var availableOrders = OpenOrders.Where(o =>
                    o.Start == vehicle.CurrentVertexPosition &&
                    o.PayloadWeight <= vehicle.MaxPayload &&
                    Parameters.Graph.FindShortestPath(Parameters.Graph, o.Start, o.Target).Item2 <=
                    vehicle.GetMaximumTravelDistance(o.PayloadWeight)
                ).ToList();

                if (availableOrders.Count > 0)
                {
                    vehicle.AssignOrder(availableOrders.First());
                }
                else
                {
                    var (order, target) = GetNearestOpenOrder(vehicle);
                    if (order != null && target != null)
                    {
                        vehicle.AssignOrderAtDifferentBase(order, target);
                    }
                }
            }

            // Advance all vehicles and their assigned orders
            Vehicles.ForEach(v => v.Advance());

            SimulationStep++;
        }

        /// <summary>
        /// Get the nearest open order available for pickup from the current location
        /// </summary>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        public Tuple<Order, Vertex<VertexInfo, EdgeInfo>> GetNearestOpenOrder(VehicleInstance vehicle)
        {
            var nearestBaseStation = GetNearestBaseStationWithOpenOrder(vehicle);
            var order = OpenOrders.FirstOrDefault(o => o.Start == nearestBaseStation);

            return Tuple.Create(order, nearestBaseStation);
        }

        /// <summary>
        /// Get the nearest base station to the current position that has open orders available
        /// </summary>
        /// <param name="vehicle">The vehicle</param>
        /// <returns></returns>
        public Vertex<VertexInfo, EdgeInfo> GetNearestBaseStationWithOpenOrder(VehicleInstance vehicle)
        {
            var nearestBaseStation = Parameters.Graph
                .Where(x => x.Info.Type == VertexType.Base)
                .Where(x => OpenOrders.Any(y => 
                    y.Start.Info == x.Info &&
                    Parameters.Graph.FindShortestPath(Parameters.Graph, y.Start, y.Target).Item2 <= vehicle.GetMaximumTravelDistance(y.PayloadWeight)))
                .Select(x => Tuple.Create(Parameters.Graph.FindShortestPath(Parameters.Graph, vehicle.CurrentVertexPosition, x).Item2, x))
                .OrderBy(x => x.Item1)
                .FirstOrDefault();

            return nearestBaseStation?.Item2;
        }

        /// <summary>
        /// Record the current state of the simulation for analysis purposes
        /// </summary>
        private void RecordSimulationStep()
        {
            var simHistoryStep = new SimulationHistoryStep()
            {
                SimulationStep = SimulationStep,
                Vehicles = new List<VehicleStepState>(),
                OpenOrders = new List<HistoryOrder>(),
                ClosedOrders = new List<HistoryCompletedOrder>()
            };

            foreach (var vehicle in Vehicles)
            {
                if (vehicle != null)
                {
                    var vehicleState = new VehicleStepState(vehicle.Vehicle)
                    {
                        State = vehicle.State,
                        CurrentVertexPosition = vehicle.CurrentVertexPosition?.Id,
                        Target = vehicle.Target?.Id,
                        CurrentOrder = new HistoryCompletedOrder(
                            new HistoryOrder()
                            {
                                Start = vehicle.CurrentOrder?.Order?.Start?.Id,
                                Target = vehicle.CurrentOrder?.Order?.Target?.Id,
                                PayloadWeight = vehicle.CurrentOrder?.Order?.PayloadWeight
                            },
                            vehicle.CurrentOrder?.DeliveryTime,
                            vehicle.CurrentOrder?.DeliveryDistance,
                            vehicle.CurrentOrder?.DeliveryPath?.Select(p => p.Id).ToList()
                        ),
                        PathToTarget = vehicle.PathToTarget?.Select(p => p.Id).ToList(),
                        DistanceToTarget = vehicle.TotalDistanceToTarget,
                        DistanceTraveled = vehicle.DistanceTraveled
                    };

                    simHistoryStep.Vehicles.Add(vehicleState);
                }
            }

            foreach (var openOrder in OpenOrders)
            {
                if (openOrder != null)
                {
                    var order = new HistoryOrder()
                    {
                        Start = openOrder.Start?.Id,
                        Target = openOrder.Target?.Id,
                        PayloadWeight = openOrder.PayloadWeight
                    };

                    simHistoryStep.OpenOrders.Add(order);
                }
            }

            foreach (var closedOrder in ClosedOrders)
            {
                if (closedOrder != null)
                {
                    var order = new HistoryCompletedOrder(new HistoryOrder(closedOrder.Order?.Start?.Id, closedOrder.Order?.Target?.Id, closedOrder.Order?.PayloadWeight))
                    {
                        DeliveryDistance = closedOrder.DeliveryDistance,
                        DeliveryTime = closedOrder.DeliveryTime,
                        DeliveryPath = closedOrder.DeliveryPath?.Select(p => p.Id).ToList()
                    };

                    simHistoryStep.ClosedOrders.Add(order);
                }
            }

            SimulationHistory.Steps.Add(simHistoryStep);
        }
    }
}