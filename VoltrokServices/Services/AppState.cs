
using VoltrokEF;
using VoltrokUtils.Enums;
using VoltrokUtils.Models;

namespace VoltrokServices.Services;

public class AppState
{
    public event Action? OnChange;
    public event Action? OnBuildingInfoOpened;
    public ModalTypesEnum ActiveModal { get; private set; } = ModalTypesEnum.None;
    public required Player MyPlayer { get; set; } = new();
    public PlayerOverviewTabEnum ActivePlayerOverviewTabEnum { get; private set; } = PlayerOverviewTabEnum.Overview;
    public bool IsChatOpen { get; private set; }
    public ChatChannel ActiveChatChannel { get; private set; } = ChatChannel.General;
    public Guid? DirectChatTargetPlayerId { get; private set; }
    public string? DirectChatTargetPlayerName { get; private set; }
    public int ChatUnreadCount { get; private set; }
    public string? CountryIsoCode2 { get; private set; }
    public Guid? PendingMilitaryDestinationId { get; private set; }
    public string? PendingMilitaryMissionType { get; private set; }
    public IReadOnlyList<string> OnboardingTargets { get; private set; } = Array.Empty<string>();
    public IReadOnlySet<string> OnboardingClickedTargets { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public long OnboardingOpenRequestId { get; private set; }

   public Guid ClickedLastPlayer { get; private set; }
    public Guid? ClickedLastCountryId { get; private set; }

    public string SelectedTheme { get; private set; } = "theme-crown";
    public string SelectedLanguage { get; private set; } = "en";
    public string MapBaseLayer { get; private set; } = "none";
    public string MapColorMode { get; private set; } = "countries";
    public bool MapShowPlayersLayer { get; private set; } = true;
    public bool MapShowCountriesLayer { get; private set; } = true;
    public bool MapShowAttackLayer { get; private set; } = true;
    public bool MapShowSiegeLayer { get; private set; } = true;
    public bool MapShowTradeLayer { get; private set; } = true;
    public decimal? MapFocusLng { get; private set; }
    public decimal? MapFocusLat { get; private set; }
    public double? MapFocusZoom { get; private set; }
    public long MapFocusRequestId { get; private set; }
    private readonly Dictionary<Guid, PlayerMapTooltipStats> _playerTooltipStatsByPlayerId = [];

    public void OpenModal(ModalTypesEnum newModal)
    {
        if (ActiveModal == newModal)
        {
            ActiveModal = ModalTypesEnum.None;
            OnChange?.Invoke();
            return;
        }

        ActiveModal = newModal;
        OnChange?.Invoke();
        if (newModal == ModalTypesEnum.Player)
        {
            OnBuildingInfoOpened?.Invoke();
        }
    }

    public void OpenChat(ChatChannel channel, Guid? targetPlayerId = null, string? targetPlayerName = null)
    {
        IsChatOpen = true;
        ActiveChatChannel = channel;
        DirectChatTargetPlayerId = targetPlayerId;
        DirectChatTargetPlayerName = targetPlayerName;
        OnChange?.Invoke();
    }

    public void CloseChat()
    {
        IsChatOpen = false;
        OnChange?.Invoke();
    }

    public void SetChatUnreadCount(int unreadCount)
    {
        var normalized = Math.Max(0, unreadCount);
        if (ChatUnreadCount == normalized)
        {
            return;
        }

        ChatUnreadCount = normalized;
        OnChange?.Invoke();
    }

    public void SetMyPlayer(Player player)
    {
        MyPlayer = player;
        OnChange?.Invoke();
    }

    public void SetCountryIsoCode2(string? isoCode2)
    {
        CountryIsoCode2 = isoCode2;
        OnChange?.Invoke();
    }

    public void CloseModal()
    {
        ActiveModal = ModalTypesEnum.None;
        ActivePlayerOverviewTabEnum = PlayerOverviewTabEnum.Overview;
        OnChange?.Invoke();
    }

    public void SetBuildingInfoTab(PlayerOverviewTabEnum tabEnum)
    {
        if (ActivePlayerOverviewTabEnum == tabEnum)
        {
            return;
        }

        ActivePlayerOverviewTabEnum = tabEnum;
        OnChange?.Invoke();
    }

    public void SetPlayerInfo(Guid buildingId)
    {
        ClickedLastPlayer = buildingId;
        OnChange?.Invoke();

        if (ActiveModal == ModalTypesEnum.Player)
        {
            OnBuildingInfoOpened?.Invoke();
        }
    }

    public void ClearPlayerInfo()
    {
        if (ClickedLastPlayer == Guid.Empty)
        {
            return;
        }

        ClickedLastPlayer = Guid.Empty;
        OnChange?.Invoke();
    }

    public void SetCountryInfo(Guid countryId)
    {
        if (ClickedLastCountryId == countryId)
        {
            return;
        }

        ClickedLastCountryId = countryId;
        OnChange?.Invoke();
    }

    public void ClearCountryInfo()
    {
        if (!ClickedLastCountryId.HasValue)
        {
            return;
        }

        ClickedLastCountryId = null;
        OnChange?.Invoke();
    }

    public void SetPlayerTooltipStatsCache(IEnumerable<PlayerMapTooltipStats> stats)
    {
        _playerTooltipStatsByPlayerId.Clear();
        foreach (var item in stats)
        {
            _playerTooltipStatsByPlayerId[item.PlayerId] = item;
        }
    }

    public bool TryGetPlayerTooltipStats(Guid playerId, out PlayerMapTooltipStats? stats)
    {
        if (_playerTooltipStatsByPlayerId.TryGetValue(playerId, out var cached))
        {
            stats = cached;
            return true;
        }

        stats = null;
        return false;
    }

    public void SetPendingMilitaryAction(Guid? destinationId, string? missionType)
    {
        PendingMilitaryDestinationId = destinationId;
        PendingMilitaryMissionType = missionType;
    }

    public void SetOnboardingTargets(params string[] targets)
    {
        var normalized = targets
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Select(target => target.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (OnboardingTargets.SequenceEqual(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        OnboardingTargets = normalized;
        OnChange?.Invoke();
    }

    public bool IsOnboardingTarget(string target)
        => OnboardingTargets.Any(existing => string.Equals(existing, target, StringComparison.OrdinalIgnoreCase));

    public void MarkOnboardingTargetClicked(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        var normalized = target.Trim();
        if (OnboardingClickedTargets.Contains(normalized))
        {
            return;
        }

        var updated = new HashSet<string>(OnboardingClickedTargets, StringComparer.OrdinalIgnoreCase)
        {
            normalized
        };

        OnboardingClickedTargets = updated;
        OnChange?.Invoke();
    }

    public bool HasOnboardingTargetBeenClicked(string target)
        => !string.IsNullOrWhiteSpace(target) && OnboardingClickedTargets.Contains(target.Trim());

    public void RequestOnboardingOpen()
    {
        OnboardingOpenRequestId++;
        OnChange?.Invoke();
    }

    public void ResetOnboardingClicks()
    {
        if (OnboardingClickedTargets.Count == 0)
        {
            return;
        }

        OnboardingClickedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        OnChange?.Invoke();
    }

    public void SetTheme(string theme)
    {
        if (SelectedTheme == theme)
        {
            return;
        }

        SelectedTheme = theme;
        OnChange?.Invoke();
    }

    public void SetLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            languageCode = "en";
        }

        var normalized = languageCode.Trim();
        var primary = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        normalized = string.IsNullOrWhiteSpace(primary) ? "en" : primary;
        if (SelectedLanguage == normalized)
        {
            return;
        }

        SelectedLanguage = normalized;
        OnChange?.Invoke();
    }

    public void SetMapBaseLayer(string layerId)
    {
        var normalized = string.IsNullOrWhiteSpace(layerId) ? "none" : layerId.Trim();
        if (MapBaseLayer == normalized)
        {
            return;
        }

        MapBaseLayer = normalized;
        OnChange?.Invoke();
    }

    public void SetMapColorMode(string colorMode)
    {
        var normalized = string.IsNullOrWhiteSpace(colorMode) ? "countries" : colorMode.Trim();
        if (MapColorMode == normalized)
        {
            return;
        }

        MapColorMode = normalized;
        OnChange?.Invoke();
    }

    public void SetMapShowCountriesLayer(bool showCountriesLayer)
    {
        if (MapShowCountriesLayer == showCountriesLayer)
        {
            return;
        }

        MapShowCountriesLayer = showCountriesLayer;
        OnChange?.Invoke();
    }

    public void SetMapShowPlayersLayer(bool showPlayersLayer)
    {
        if (MapShowPlayersLayer == showPlayersLayer)
        {
            return;
        }

        MapShowPlayersLayer = showPlayersLayer;
        OnChange?.Invoke();
    }

    public void SetMapShowAttackLayer(bool showAttackLayer)
    {
        if (MapShowAttackLayer == showAttackLayer)
        {
            return;
        }

        MapShowAttackLayer = showAttackLayer;
        OnChange?.Invoke();
    }

    public void SetMapShowSiegeLayer(bool showSiegeLayer)
    {
        if (MapShowSiegeLayer == showSiegeLayer)
        {
            return;
        }

        MapShowSiegeLayer = showSiegeLayer;
        OnChange?.Invoke();
    }

    public void SetMapShowTradeLayer(bool showTradeLayer)
    {
        if (MapShowTradeLayer == showTradeLayer)
        {
            return;
        }

        MapShowTradeLayer = showTradeLayer;
        OnChange?.Invoke();
    }

    public void RequestMapFocus(decimal lng, decimal lat, double? zoom = null)
    {
        MapFocusLng = lng;
        MapFocusLat = lat;
        MapFocusZoom = zoom;
        MapFocusRequestId++;
        OnChange?.Invoke();
    }
}
