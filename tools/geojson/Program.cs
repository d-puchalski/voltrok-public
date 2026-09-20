using System.Text.Json;
using System.Xml.Linq;

namespace geojson;

public class GeoJsonToTmxConverter
{
    public class GeoJsonFeature
    {
        public string Type { get; set; }
        public Geometry Geometry { get; set; }
        public Dictionary<string, JsonElement> Properties { get; set; }
    }

    public class Geometry
    {
        public string Type { get; set; }
        public JsonElement Coordinates { get; set; }
    }

    public class GeoJsonRoot
    {
        public string Type { get; set; }
        public List<GeoJsonFeature> Features { get; set; }
    }

    public static void ConvertGeoJsonToTmx(string geoJsonPath, string tmxOutputPath,
        int tileWidth = 32, int tileHeight = 32, int mapWidth = 100, int mapHeight = 100)
    {
        // Read and parse GeoJSON
        var geoJsonContent = File.ReadAllText(geoJsonPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var geoJson = JsonSerializer.Deserialize<GeoJsonRoot>(geoJsonContent, options);

        if (geoJson == null || geoJson.Features == null || geoJson.Features.Count == 0)
        {
            Console.WriteLine("Błąd: Plik GeoJSON jest pusty lub nieprawidłowy");
            return;
        }

        // Calculate bounds to map coordinates to tile indices
        var bounds = CalculateBounds(geoJson);

        // Check if bounds are valid
        if (bounds.minLon == double.MaxValue || bounds.minLat == double.MaxValue)
        {
            Console.WriteLine("Błąd: Nie można obliczyć granic danych geograficznych");
            return;
        }

        // Create tile data grid (0 = empty, 1+ = tile ID)
        var tileGrid = new int[mapHeight, mapWidth];

        // Convert GeoJSON features to tiles
        foreach (var feature in geoJson.Features)
        {
            if (feature.Geometry == null) continue;

            if (feature.Geometry.Type == "Point")
            {
                var coords = feature.Geometry.Coordinates.Deserialize<double[]>();
                if (coords != null && coords.Length >= 2)
                {
                    var tilePos = MapCoordinatesToTile(coords[0], coords[1], bounds, mapWidth, mapHeight);
                    if (IsValidTilePosition(tilePos.x, tilePos.y, mapWidth, mapHeight))
                    {
                        tileGrid[tilePos.y, tilePos.x] = 1; // Tile ID 1
                    }
                }
            }
            else if (feature.Geometry.Type == "Polygon")
            {
                var coords = feature.Geometry.Coordinates.Deserialize<double[][][]>();
                if (coords != null && coords.Length > 0)
                {
                    FillPolygon(tileGrid, coords[0], bounds, mapWidth, mapHeight);
                }
            }
            else if (feature.Geometry.Type == "LineString")
            {
                var coords = feature.Geometry.Coordinates.Deserialize<double[][]>();
                if (coords != null)
                {
                    DrawLine(tileGrid, coords, bounds, mapWidth, mapHeight);
                }
            }
        }

        // Generate TMX XML
        var tmx = GenerateTmxXml(tileGrid, mapWidth, mapHeight, tileWidth, tileHeight);

        // Save to file
        tmx.Save(tmxOutputPath);
        Console.WriteLine($"TMX file created: {tmxOutputPath}");
    }

    private static (double minLon, double maxLon, double minLat, double maxLat) CalculateBounds(GeoJsonRoot geoJson)
    {
        double minLon = double.MaxValue, maxLon = double.MinValue;
        double minLat = double.MaxValue, maxLat = double.MinValue;

        foreach (var feature in geoJson.Features)
        {
            if (feature.Geometry == null) continue;

            if (feature.Geometry.Type == "Point")
            {
                var coords = feature.Geometry.Coordinates.Deserialize<double[]>();
                if (coords != null && coords.Length >= 2)
                {
                    minLon = Math.Min(minLon, coords[0]);
                    maxLon = Math.Max(maxLon, coords[0]);
                    minLat = Math.Min(minLat, coords[1]);
                    maxLat = Math.Max(maxLat, coords[1]);
                }
            }
            else if (feature.Geometry.Type == "Polygon")
            {
                var coords = feature.Geometry.Coordinates.Deserialize<double[][][]>();
                if (coords != null)
                {
                    foreach (var ring in coords)
                    {
                        foreach (var point in ring)
                        {
                            if (point != null && point.Length >= 2)
                            {
                                minLon = Math.Min(minLon, point[0]);
                                maxLon = Math.Max(maxLon, point[0]);
                                minLat = Math.Min(minLat, point[1]);
                                maxLat = Math.Max(maxLat, point[1]);
                            }
                        }
                    }
                }
            }
            else if (feature.Geometry.Type == "LineString")
            {
                var coords = feature.Geometry.Coordinates.Deserialize<double[][]>();
                if (coords != null)
                {
                    foreach (var point in coords)
                    {
                        if (point != null && point.Length >= 2)
                        {
                            minLon = Math.Min(minLon, point[0]);
                            maxLon = Math.Max(maxLon, point[0]);
                            minLat = Math.Min(minLat, point[1]);
                            maxLat = Math.Max(maxLat, point[1]);
                        }
                    }
                }
            }
        }

        return (minLon, maxLon, minLat, maxLat);
    }

    private static (int x, int y) MapCoordinatesToTile(double lon, double lat,
        (double minLon, double maxLon, double minLat, double maxLat) bounds,
        int mapWidth, int mapHeight)
    {
        var lonRange = bounds.maxLon - bounds.minLon;
        var latRange = bounds.maxLat - bounds.minLat;

        // Avoid division by zero
        if (lonRange == 0) lonRange = 1;
        if (latRange == 0) latRange = 1;

        var x = (int)((lon - bounds.minLon) / lonRange * (mapWidth - 1));
        var y = (int)((bounds.maxLat - lat) / latRange * (mapHeight - 1)); // Flip Y

        return (x, y);
    }

    private static bool IsValidTilePosition(int x, int y, int width, int height)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    private static void FillPolygon(int[,] grid, double[][] polygon,
        (double minLon, double maxLon, double minLat, double maxLat) bounds,
        int mapWidth, int mapHeight)
    {
        if (polygon == null || polygon.Length < 3) return;

        // Convert polygon points to tile coordinates
        var tilePoints = new List<(int x, int y)>();
        for (var i = 0; i < polygon.Length; i++)
        {
            if (polygon[i] != null && polygon[i].Length >= 2)
            {
                var tilePos = MapCoordinatesToTile(polygon[i][0], polygon[i][1], bounds, mapWidth, mapHeight);
                tilePoints.Add(tilePos);
            }
        }

        if (tilePoints.Count < 3) return;

        // Draw connected contour lines
        for (var i = 0; i < tilePoints.Count - 1; i++)
        {
            DrawLineBetween(grid, tilePoints[i], tilePoints[i + 1], mapWidth, mapHeight);
        }
        // Close the polygon
        DrawLineBetween(grid, tilePoints[tilePoints.Count - 1], tilePoints[0], mapWidth, mapHeight);

        // Fill the interior using scanline algorithm
        ScanlineFill(grid, tilePoints, mapWidth, mapHeight);
    }

    private static void DrawLine(int[,] grid, double[][] line,
        (double minLon, double maxLon, double minLat, double maxLat) bounds,
        int mapWidth, int mapHeight)
    {
        if (line == null || line.Length < 2) return;

        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] != null && line[i].Length >= 2 && line[i + 1] != null && line[i + 1].Length >= 2)
            {
                var start = MapCoordinatesToTile(line[i][0], line[i][1], bounds, mapWidth, mapHeight);
                var end = MapCoordinatesToTile(line[i + 1][0], line[i + 1][1], bounds, mapWidth, mapHeight);
                DrawLineBetween(grid, start, end, mapWidth, mapHeight);
            }
        }
    }

    private static void DrawLineBetween(int[,] grid, (int x, int y) start, (int x, int y) end,
        int mapWidth, int mapHeight)
    {
        // Bresenham's line algorithm
        var dx = Math.Abs(end.x - start.x);
        var dy = Math.Abs(end.y - start.y);
        var sx = start.x < end.x ? 1 : -1;
        var sy = start.y < end.y ? 1 : -1;
        var err = dx - dy;

        int x = start.x, y = start.y;
        while (true)
        {
            if (IsValidTilePosition(x, y, mapWidth, mapHeight))
            {
                grid[y, x] = 1;
            }

            if (x == end.x && y == end.y) break;

            var e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }

    private static void ScanlineFill(int[,] grid, List<(int x, int y)> polygon,
        int mapWidth, int mapHeight)
    {
        if (polygon.Count < 3) return;

        // Find bounds of polygon
        var minY = polygon.Min(p => p.y);
        var maxY = polygon.Max(p => p.y);

        // Scanline fill algorithm
        for (var y = minY; y <= maxY; y++)
        {
            var intersections = new List<int>();

            // Find all intersections with this scanline
            for (var i = 0; i < polygon.Count; i++)
            {
                var j = (i + 1) % polygon.Count;
                var p1 = polygon[i];
                var p2 = polygon[j];

                if ((p1.y <= y && p2.y > y) || (p2.y <= y && p1.y > y))
                {
                    // Calculate intersection x coordinate
                    if (p2.y != p1.y) // Avoid division by zero
                    {
                        var x = p1.x + (double)(y - p1.y) / (p2.y - p1.y) * (p2.x - p1.x);
                        intersections.Add((int)Math.Round(x));
                    }
                }
            }

            // Sort intersections
            intersections.Sort();

            // Fill between pairs of intersections
            for (var i = 0; i < intersections.Count - 1; i += 2)
            {
                for (var x = intersections[i]; x <= intersections[i + 1]; x++)
                {
                    if (IsValidTilePosition(x, y, mapWidth, mapHeight))
                    {
                        grid[y, x] = 1;
                    }
                }
            }
        }
    }

    private static XDocument GenerateTmxXml(int[,] tileGrid, int width, int height,
        int tileWidth, int tileHeight)
    {
        var map = new XElement("map",
            new XAttribute("version", "1.10"),
            new XAttribute("tiledversion", "1.10.2"),
            new XAttribute("orientation", "isometric"),
            new XAttribute("renderorder", "right-down"),
            new XAttribute("width", width),
            new XAttribute("height", height),
            new XAttribute("tilewidth", tileWidth),
            new XAttribute("tileheight", tileHeight),
            new XAttribute("infinite", "0")
        );

        // Add tileset with tiles.png
        var tileset = new XElement("tileset",
            new XAttribute("firstgid", "1"),
            new XAttribute("name", "geojson_tiles"),
            new XAttribute("tilewidth", tileWidth),
            new XAttribute("tileheight", tileHeight),
            new XAttribute("tilecount", "1"),
            new XAttribute("columns", "1"),
            new XElement("image",
                new XAttribute("source", "tiles.png"),
                new XAttribute("width", tileWidth),
                new XAttribute("height", tileHeight)
            )
        );
        map.Add(tileset);

        // Create layer with tile data
        var layer = new XElement("layer",
            new XAttribute("id", "1"),
            new XAttribute("name", "GeoJSON Layer"),
            new XAttribute("width", width),
            new XAttribute("height", height)
        );

        // Convert grid to CSV format
        var dataLines = new List<string>();
        for (var y = 0; y < height; y++)
        {
            var row = new List<string>();
            for (var x = 0; x < width; x++)
            {
                row.Add(tileGrid[y, x].ToString());
            }
            dataLines.Add(string.Join(",", row));
        }

        var data = new XElement("data",
            new XAttribute("encoding", "csv"),
            string.Join(",\n", dataLines)
        );

        layer.Add(data);
        map.Add(layer);

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), map);
    }

    // Example usage
    public static void Main(string[] args)
    {
        var geoJsonFile = "overture-division_area-15.524236564204465-41.89756542149638-12.477049323137066.geojson";
        var tmxFile = "output.tmx";

        if (args.Length >= 2)
        {
            geoJsonFile = args[0];
            tmxFile = args[1];
        }

        try
        {
            ConvertGeoJsonToTmx(geoJsonFile, tmxFile,
                tileWidth: 128,
                tileHeight: 64,
                mapWidth: 512,
                mapHeight: 512);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd: {ex.Message}");
        }
    }
}