namespace Cars.Application.Services
{
    public interface IDistanceService
    {
        // 取得距離與時間
        Task<(decimal km, double minutes)> GetDistanceAsync(string origin, string destination);
        // 驗證地點是否有效
        Task<bool> IsValidLocationAsync(string location);
    }

}
