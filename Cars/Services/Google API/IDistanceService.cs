namespace Cars.Services
{
    public interface IDistanceService
    {
        Task<(decimal km, double minutes)> GetDistanceAsync(string origin, string destination);
    }

}
