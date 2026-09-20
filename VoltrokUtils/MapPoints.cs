namespace VoltrokUtils;

public struct Point
{
    public double Lng { get; set; }
    public double Lat { get; set; }
}

public struct BoundingBox
{
    public double MinLng { get; set; }
    public double MaxLng { get; set; }
    public double MinLat { get; set; }
    public double MaxLat { get; init; }
}

public class GeoJsonPoint
{
    public string Type { get; set; } = "Feature";
    public GeoJsonGeometry? Geometry { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
}

public class GeoJsonGeometry
{
    public string? Type { get; set; }
    public object? Coordinates { get; set; }
}

public class GeoJsonFeatureCollection
{
    public string Type { get; set; } = "FeatureCollection";
    public List<GeoJsonPoint> Features { get; set; } = [];
}

public static class MapPoints
{
    /// <summary>
    /// Check if a point is inside a polygon using ray casting algorithm
    /// </summary>
    public static bool IsPointInPolygon(Point point, double[][] polygon)
    {
        var inside = false;
        var x = point.Lng;
        var y = point.Lat;

        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var xi = polygon[i][0];
            var yi = polygon[i][1];
            var xj = polygon[j][0];
            var yj = polygon[j][1];

            var intersect = (yi > y) != (yj > y) && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi;

            if (intersect) inside = !inside;
        }

        return inside;
    }

    /// <summary>
    /// Get bounding box from polygon coordinates
    /// </summary>
    private static BoundingBox GetBoundingBox(double[][][] coordinates)
    {
        var minLng = double.MaxValue;
        var maxLng = double.MinValue;
        var minLat = double.MaxValue;
        var maxLat = double.MinValue;

        foreach (var ring in coordinates)
        {
            foreach (var coord in ring)
            {
                minLng = Math.Min(minLng, coord[0]);
                maxLng = Math.Max(maxLng, coord[0]);
                minLat = Math.Min(minLat, coord[1]);
                maxLat = Math.Max(maxLat, coord[1]);
            }
        }

        return new BoundingBox
        {
            MinLng = minLng,
            MaxLng = maxLng,
            MinLat = minLat,
            MaxLat = maxLat
        };
    }

    /// <summary>
    /// Generate random points within a polygon
    /// </summary>
    public static List<Point> GenerateRandomPointsInPolygon(double[][][] coordinates, int count)
    {
        var points = new List<Point>();
        var bbox = GetBoundingBox(coordinates);
        var mainPolygon = coordinates[0]; // Use the outer ring
        var random = new Random();

        var attempts = 0;
        var maxAttempts = count * 100; // Prevent infinite loops

        while (points.Count < count && attempts < maxAttempts)
        {
            attempts++;

            // Generate random point within bounding box
            var randomPoint = new Point
            {
                Lng = bbox.MinLng + random.NextDouble() * (bbox.MaxLng - bbox.MinLng),
                Lat = bbox.MinLat + random.NextDouble() * (bbox.MaxLat - bbox.MinLat)
            };

            // Check if point is inside polygon
            if (IsPointInPolygon(randomPoint, mainPolygon))
            {
                points.Add(randomPoint);
            }
        }

        return points;
    }

    /// <summary>
    /// Generate random points for all features in a GeoJSON
    /// </summary>
    public static GeoJsonFeatureCollection GeneratePointsForFeatures(List<dynamic> features, int pointsPerFeature = 100)
    {
        var pointFeatures = new List<GeoJsonPoint>();

        foreach (var feature in features)
        {
            // Skip features without geometry or properties
            if (feature?.geometry == null || feature?.properties == null)
            {
                continue;
            }

            string geometryType = feature!.geometry.type;

            if (geometryType == "Polygon")
            {
                double[][][] coordinates = feature.geometry.coordinates;
                var points = GenerateRandomPointsInPolygon(coordinates, pointsPerFeature);

                var index = 0;
                foreach (var point in points)
                {
                    var geoJsonPoint = new GeoJsonPoint
                    {
                        Geometry = new GeoJsonGeometry
                        {
                            Type = "Point",
                            Coordinates = new[] { point.Lng, point.Lat }
                        },
                        Properties = new Dictionary<string, object>
                        {
                            { "id", $"{feature.properties.iso_a2}-{index}" },
                            { "country", feature.properties.iso_a2 },
                            { "regionName", feature.properties.name },
                            { "regionData", new {
                                name = feature.properties.name,
                                country = feature.properties.admin,
                                type = feature.properties.type_en,
                                label = feature.properties.name
                            }}
                        }
                    };
                    pointFeatures.Add(geoJsonPoint);
                    index++;
                }
            }
            else if (geometryType == "MultiPolygon")
            {
                // Handle MultiPolygon by processing each polygon
                double[][][][] coordinates = feature.geometry.coordinates;
                var pointsPerPolygon = Math.Max(1, pointsPerFeature / coordinates.Length);

                var index = 0;
                foreach (var polygonCoords in coordinates)
                {
                    var points = GenerateRandomPointsInPolygon(polygonCoords, pointsPerPolygon);

                    foreach (var point in points)
                    {
                        var geoJsonPoint = new GeoJsonPoint
                        {
                            Geometry = new GeoJsonGeometry
                            {
                                Type = "Point",
                                Coordinates = new[] { point.Lng, point.Lat }
                            },
                            Properties = new Dictionary<string, object>
                            {
                                { "id", $"{feature.properties.iso_a2}-{index}" },
                                { "country", feature.properties.iso_a2 },
                                { "regionName", feature.properties.name },
                                { "regionData", new {
                                    name = feature.properties.name,
                                    country = feature.properties.admin,
                                    type = feature.properties.type_en,
                                    label = feature.properties.name
                                }}
                            }
                        };
                        pointFeatures.Add(geoJsonPoint);
                        index++;
                    }
                }
            }
        }

        return new GeoJsonFeatureCollection { Features = pointFeatures };
    }
}
