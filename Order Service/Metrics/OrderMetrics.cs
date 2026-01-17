using Prometheus;

namespace Order_Service.Metrics
{
    public class OrderMetrics
    {
        public static readonly Counter OrdersCreated =
          Prometheus.Metrics.CreateCounter(
              "orders_created_total",
              "Total number of orders created");

        public static readonly Gauge OutboxPending =
            Prometheus.Metrics.CreateGauge(
                "outbox_pending_messages",
                "Number of unprocessed outbox messages");
    }
}
