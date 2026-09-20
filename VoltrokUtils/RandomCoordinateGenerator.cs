using System.Text.Json;

namespace VoltrokUtils;

public static class RandomCoordinateGenerator
{
    private static readonly Random Random = new();

    public static (double Latitude, double Longitude) GetRandomLandCoordinateForRegion(string? isoCode2, string? admCode1, string? name, string geoJsonPath)
    {
        using var stream = File.OpenRead(geoJsonPath);
        using var document = JsonDocument.Parse(stream);
        document.RootElement.TryGetProperty("features", out var features);
        if (features.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("GeoJSON features array is missing.");
        }

        var feature = FindFeature(features, isoCode2, admCode1, name);
        if (!feature.HasValue)
        {
            throw new InvalidOperationException("No matching region feature found in GeoJSON.");
        }

        feature.Value.TryGetProperty("geometry", out var geometry);
        var polygons = ExtractPolygons(geometry);
        if (polygons.Count == 0)
        {
            throw new InvalidOperationException("No polygon geometry found for region.");
        }

        for (var attempt = 0; attempt < 500; attempt++)
        {
            var polygon = polygons[Random.Next(polygons.Count)];
            var (minLon, maxLon, minLat, maxLat) = GetBounds(polygon);
            var candidateLon = RandomBetween(minLon, maxLon);
            var candidateLat = RandomBetween(minLat, maxLat);
            if (IsPointInPolygon(candidateLon, candidateLat, polygon))
            {
                return (candidateLat, candidateLon);
            }
        }

        var fallback = polygons[0];
        var (centroidLon, centroidLat) = GetCentroid(fallback);
        return (centroidLat, centroidLon);
    }

    private static double RandomBetween(double min, double max)
        => min + Random.NextDouble() * (max - min);

    private static JsonElement? FindFeature(JsonElement features, string? isoCode2, string? admCode1, string? name)
    {
        var iso = (isoCode2 ?? string.Empty).Trim().ToUpperInvariant();
        var adm = (admCode1 ?? string.Empty).Trim().ToUpperInvariant();
        var label = (name ?? string.Empty).Trim().ToUpperInvariant();

        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("properties", out var props))
            {
                continue;
            }

            var propIso = GetPropertyValue(props, "iso_code_2", "iso_3166_2", "iso_a2");
            var propAdm = GetPropertyValue(props, "adm_code_1", "ADM1_PCODE");
            var propName = GetPropertyValue(props, "name", "NAME");

            if (!string.IsNullOrWhiteSpace(iso) && string.Equals(propIso, iso, StringComparison.OrdinalIgnoreCase))
            {
                return feature;
            }

            if (!string.IsNullOrWhiteSpace(adm) && string.Equals(propAdm, adm, StringComparison.OrdinalIgnoreCase))
            {
                return feature;
            }

            if (!string.IsNullOrWhiteSpace(label) && string.Equals(propName, label, StringComparison.OrdinalIgnoreCase))
            {
                return feature;
            }
        }

        return null;
    }

    private static string? GetPropertyValue(JsonElement props, params string[] names)
    {
        foreach (var name in names)
        {
            if (props.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static List<List<(double Lon, double Lat)>> ExtractPolygons(JsonElement geometry)
    {
        var polygons = new List<List<(double Lon, double Lat)>>();
        if (!geometry.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
        {
            return polygons;
        }

        var type = typeProp.GetString();
        if (type == "Polygon")
        {
            if (geometry.TryGetProperty("coordinates", out var coords))
            {
                AddPolygon(coords, polygons);
            }
        }
        else if (type == "MultiPolygon")
        {
            if (geometry.TryGetProperty("coordinates", out var multiCoords))
            {
                foreach (var polygon in multiCoords.EnumerateArray())
                {
                    AddPolygon(polygon, polygons);
                }
            }
        }

        return polygons;
    }

    private static void AddPolygon(JsonElement coords, List<List<(double Lon, double Lat)>> polygons)
    {
        if (coords.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var ring = coords.EnumerateArray().FirstOrDefault();
        if (ring.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var points = new List<(double Lon, double Lat)>();
        foreach (var point in ring.EnumerateArray())
        {
            if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() < 2)
            {
                continue;
            }

            var lon = point[0].GetDouble();
            var lat = point[1].GetDouble();
            points.Add((lon, lat));
        }

        if (points.Count > 2)
        {
            polygons.Add(points);
        }
    }

    private static (double MinLon, double MaxLon, double MinLat, double MaxLat) GetBounds(List<(double Lon, double Lat)> points)
    {
        var minLon = double.MaxValue;
        var maxLon = double.MinValue;
        var minLat = double.MaxValue;
        var maxLat = double.MinValue;

        foreach (var (lon, lat) in points)
        {
            minLon = Math.Min(minLon, lon);
            maxLon = Math.Max(maxLon, lon);
            minLat = Math.Min(minLat, lat);
            maxLat = Math.Max(maxLat, lat);
        }

        return (minLon, maxLon, minLat, maxLat);
    }

    private static (double Lon, double Lat) GetCentroid(List<(double Lon, double Lat)> points)
    {
        var sumLon = 0d;
        var sumLat = 0d;
        foreach (var (lon, lat) in points)
        {
            sumLon += lon;
            sumLat += lat;
        }

        var count = points.Count == 0 ? 1 : points.Count;
        return (sumLon / count, sumLat / count);
    }

    private static bool IsPointInPolygon(double lon, double lat, List<(double Lon, double Lat)> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var (xi, yi) = polygon[i];
            var (xj, yj) = polygon[j];
            var intersect = ((yi > lat) != (yj > lat))
                && (lon < (xj - xi) * (lat - yi) / (yj - yi + double.Epsilon) + xi);
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
