namespace WallpaperRotator.Services;

/// <summary>Offline sunrise/sunset computation (sunrise equation, J2000 epoch).</summary>
public static class SolarTimes
{
    /// <summary>Local-time sunrise/sunset for the given calendar date and coordinates.</summary>
    public static (DateTime Sunrise, DateTime Sunset) ForDate(DateTime date, double latDeg, double lonDeg)
    {
        var utcNoon = new DateTime(date.Year, date.Month, date.Day, 12, 0, 0, DateTimeKind.Utc);
        double n = (utcNoon - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays;
        double nStar = n - lonDeg / 360.0;

        double m = Mod(357.5291 + 0.98560028 * nStar, 360.0);              // mean anomaly
        double c = 1.9148 * Sin(m) + 0.0200 * Sin(2 * m) + 0.0003 * Sin(3 * m); // equation of center
        double lambda = Mod(m + c + 180.0 + 102.9372, 360.0);              // ecliptic longitude
        double jTransit = 2451545.0 + nStar + 0.0053 * Sin(m) - 0.0069 * Sin(2 * lambda); // solar transit
        double delta = Asin(Sin(lambda) * Sin(23.4397));                   // declination

        double cosOmega = (Sin(-0.83) - Sin(latDeg) * Sin(delta)) / (Cos(latDeg) * Cos(delta));
        cosOmega = Math.Clamp(cosOmega, -1.0, 1.0);   // polar day/night: clamp to 0h/24h daylight
        double omega = Deg(Math.Acos(cosOmega));

        var sunrise = FromJulian(jTransit - omega / 360.0).ToLocalTime();
        var sunset = FromJulian(jTransit + omega / 360.0).ToLocalTime();
        return (sunrise, sunset);
    }

    private static DateTime FromJulian(double j) =>
        new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddDays(j - 2451545.0);

    private static double Mod(double a, double b) => ((a % b) + b) % b;
    private static double Sin(double deg) => Math.Sin(deg * Math.PI / 180.0);
    private static double Cos(double deg) => Math.Cos(deg * Math.PI / 180.0);
    private static double Asin(double x) => Math.Asin(x) * 180.0 / Math.PI;
    private static double Deg(double rad) => rad * 180.0 / Math.PI;
}