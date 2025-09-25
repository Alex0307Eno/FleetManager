using Cars.Models;

namespace Cars.Helpers
{
    public static class DriverHelper
    {
        public static (int driverId, string driverName) ResolveDriver(Schedule s, Driver d, DriverDelegation? dg, Driver? agent)
        {
            if (dg != null && agent != null && s.IsPresent == false)
                return (agent.DriverId, agent.DriverName + " (代)");
            return (d.DriverId, d.DriverName);
        }
    }

}
