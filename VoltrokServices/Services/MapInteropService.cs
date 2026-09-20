﻿using Microsoft.JSInterop;

 namespace VoltrokServices.Services;

 // Handler class for map click events
 public class MapClickHandler(
     Func<MapFeature, Task>? onPointClick = null, 
     Func<MapFeature, Task>? onCountryClick = null, 
     Func<MapCoordinates, string?, string?, string?, Task>? onMapClick = null)
 {
     [JSInvokable]
     public async Task HandlePointClick(MapFeature feature)
     {
         if (onPointClick != null) await onPointClick.Invoke(feature);
     }

     [JSInvokable]
     public async Task HandleCountryClick(MapFeature feature, MapCoordinates coords)
     {
         if (onCountryClick != null) await onCountryClick.Invoke(feature);
         if (onMapClick != null) await onMapClick.Invoke(coords, feature.Properties?.IsoCode2, feature.Properties?.AdmCode, feature.Properties?.Name);
     }

     [JSInvokable]
     public async Task HandleMapClick(MapCoordinates coords, string? isoCode2 = null, string? admCode1 = null, string? name = null)
     {
         if (onMapClick != null) await onMapClick.Invoke(coords, isoCode2, admCode1, name);
     }
 }

 public class MapCoordinates
 {
     [System.Text.Json.Serialization.JsonPropertyName("lng")]
     public decimal Lng { get; set; }
        
     [System.Text.Json.Serialization.JsonPropertyName("lat")]
     public decimal Lat { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("zoom")]
    public double? Zoom { get; set; }
 }

 public class MapFeature
 {
     [System.Text.Json.Serialization.JsonPropertyName("properties")]
     public MapProperties? Properties { get; set; }
        
     [System.Text.Json.Serialization.JsonPropertyName("id")]
     public string? Id { get; set; }
 }

 public class MapProperties
 {
     [System.Text.Json.Serialization.JsonPropertyName("area")]
     public decimal? Area { get; set; }
        
     [System.Text.Json.Serialization.JsonPropertyName("adm_code_1")]
     public string? AdmCode { get; set; }
        
     [System.Text.Json.Serialization.JsonPropertyName("iso_code_2")]
     public string? IsoCode2 { get; set; }

     [System.Text.Json.Serialization.JsonPropertyName("name")]
     public string? Name { get; set; }

     [System.Text.Json.Serialization.JsonPropertyName("playerId")]
     public Guid? PlayerId { get; set; }

     [System.Text.Json.Serialization.JsonPropertyName("code")]
     public string? Code { get; set; }
 }

 public class MapInteropService(IJSRuntime jsRuntime)
 {
    public async Task<IJSObjectReference> InitializeMap(string containerId, double centerLng, double centerLat, double zoom, object? countryStyles = null, string? colorMode = null, string? locale = null, object? localization = null, bool debugMode = false)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         return await module.InvokeAsync<IJSObjectReference>(
             "initializeMap", containerId, centerLng, centerLat, zoom, countryStyles, colorMode, locale, localization, debugMode);
     }

    public async Task SetMapLocalization(IJSObjectReference map, string? locale, object? localization)
    {
        var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/map-interop.js");

        await module.InvokeVoidAsync("setMapLocalization", map, locale, localization);
    }

     public async Task SetWarCountries(IJSObjectReference map, IEnumerable<Guid> ownWarCountryIds, IEnumerable<Guid?>? enemyWarCountryIds)
    {
        var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/map-interop.js");

        await module.InvokeVoidAsync("setWarCountries", map, ownWarCountryIds, enemyWarCountryIds);
    }

    public async Task SetCountryStyles(IJSObjectReference map, object countryStyles)
    {
        var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/map-interop.js");

        await module.InvokeVoidAsync("setCountryStyles", map, countryStyles);
    }

    public async Task SetCountryTooltipData(IJSObjectReference map, object countryTooltipData)
    {
        var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/map-interop.js");

        await module.InvokeVoidAsync("setCountryTooltipData", map, countryTooltipData);
    }

    public async Task SetPlayerTooltipData(IJSObjectReference map, object playerTooltipData)
    {
        var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/map-interop.js");

        await module.InvokeVoidAsync("setPlayerTooltipData", map, playerTooltipData);
    }

    public async Task SetCountryColorMode(IJSObjectReference map, string colorMode)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");

         await module.InvokeVoidAsync("setCountryColorMode", map, colorMode);
     }

     public async Task AddPointsLayer(IJSObjectReference map, object pointsData)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         await module.InvokeVoidAsync("addPointsLayer", map, pointsData);
     }

     public async Task AddConnectionsLayer(IJSObjectReference map, object connectionsData)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         await module.InvokeVoidAsync("addConnectionsLayer", map, connectionsData);
     }

     public async Task AddTransportsLayer(IJSObjectReference map, object transportData)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         await module.InvokeVoidAsync("addTransportsLayer", map, transportData);
     }

    public async Task AddSiegesLayer(IJSObjectReference map, object siegeData)
    {
        var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/map-interop.js");

        await module.InvokeVoidAsync("addSiegesLayer", map, siegeData);
    }

     public async Task UpdateAnimatedTransports(IJSObjectReference map, object transportData)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         await module.InvokeVoidAsync("updateAnimatedTransports", map, transportData);
     }

    public async Task UpdateAnimatedSieges(IJSObjectReference map, object siegeData)
    {
        var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/map-interop.js");

        await module.InvokeVoidAsync("updateAnimatedSieges", map, siegeData);
    }

     public async Task ToggleLayerVisibility(IJSObjectReference map, string layerId, bool visible)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         await module.InvokeVoidAsync("toggleLayerVisibility", map, layerId, visible);
     }

     public async Task AddBaseLayer(IJSObjectReference map, string type)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         await module.InvokeVoidAsync("addBaseLayer", map, type);
     }

     public async Task RemoveBaseLayer(IJSObjectReference map)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         await module.InvokeVoidAsync("removeBaseLayer", map);
     }

     public async Task JumpTo(IJSObjectReference map, MapCoordinates coords)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         await module.InvokeVoidAsync("jumpTo", map, coords);
     }

     public async Task OnPointClick(IJSObjectReference map, DotNetObjectReference<MapClickHandler> handler)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         await module.InvokeVoidAsync("onPointClick", map, handler);
     }

     public async Task OnRegionClick(IJSObjectReference map, DotNetObjectReference<MapClickHandler> handler)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         await module.InvokeVoidAsync("onCountryClick", map, handler);
     }

     public async Task OnCountryClick(IJSObjectReference map, DotNetObjectReference<MapClickHandler> handler)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");

         await module.InvokeVoidAsync("onCountryClick", map, handler);
     }

     public async Task OnMapClick(IJSObjectReference map, DotNetObjectReference<MapClickHandler> handler)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         await module.InvokeVoidAsync("onMapClick", map, handler);
     }

     public async Task SetMapCursor(IJSObjectReference map, string cursorStyle)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         await module.InvokeVoidAsync("setMapCursor", map, cursorStyle);
     }

     public async Task Cleanup(IJSObjectReference map)
     {
         var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
             "import", "./js/map-interop.js");
            
         await module.InvokeVoidAsync("cleanup", map);
     }
 }
