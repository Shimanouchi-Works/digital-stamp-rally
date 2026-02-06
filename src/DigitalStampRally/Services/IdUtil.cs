namespace DigitalStampRally.Services;

public static class IdUtil
{
    public static long NewId()
        => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L
           + Random.Shared.Next(1000);
}
