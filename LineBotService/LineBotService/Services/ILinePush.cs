namespace LineBotService.Services
{
    public interface ILinePush
    {
        Task PushAsync(string to, string text);
    }
}
