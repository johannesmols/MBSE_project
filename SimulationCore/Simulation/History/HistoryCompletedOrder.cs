﻿using System.Collections.Generic;

namespace SimulationCore.Simulation.History
{
    public class HistoryCompletedOrder : HistoryOrder
    {
        public double? DeliveryTime { get; set; }

        public double? DeliveryDistance { get; set; }

        public double? DeliveryCost { get; set; }

        public List<string> DeliveryPath { get; set; }

        public HistoryCompletedOrder(HistoryOrder order, double? deliveryTime, double? deliveryDistance, double? deliverCost, List<string> deliveryPath) : base(order)
        {
            DeliveryTime = deliveryTime;
            DeliveryDistance = deliveryDistance;
            DeliveryCost = deliverCost;
            DeliveryPath = deliveryPath;
        }

        public HistoryCompletedOrder(HistoryOrder order) : base(order)
        {
        }

        public HistoryCompletedOrder()
        {

        }
    }
}