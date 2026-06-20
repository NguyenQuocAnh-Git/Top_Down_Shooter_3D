using System.Collections.Generic;

public static class GameSessionData
{
    public const string MenuSceneName = "MenuScene";
    public const string GameplaySceneName = "GameplayScene";

    private static readonly List<Weapon_Data> selectedWeapons = new List<Weapon_Data>();
    private static readonly Dictionary<int, string> displayNamesByPlayerId = new Dictionary<int, string>();
    private static readonly Dictionary<int, List<Weapon_Data>> weaponsByPlayerId = new Dictionary<int, List<Weapon_Data>>();

    public static Mission SelectedMission { get; private set; }
    public static bool GameplayRequestedFromMenu { get; private set; }
    public static bool FriendlyFire { get; private set; }
    public static bool HasFriendlyFireSetting { get; private set; }
    public static bool IsCoopSession { get; private set; }
    public static int LevelGenerationSeed { get; private set; }
    public static string LocalDisplayName { get; private set; } = string.Empty;

    public static bool HasSelectedWeapons => selectedWeapons.Count > 0;
    public static bool HasSelectedMission => SelectedMission != null;

    public static void SetSelectedWeapons(IEnumerable<Weapon_Data> weapons)
    {
        selectedWeapons.Clear();

        if (weapons == null)
            return;

        foreach (Weapon_Data weapon in weapons)
        {
            if (weapon != null)
                selectedWeapons.Add(weapon);
        }
    }

    public static List<Weapon_Data> GetSelectedWeapons()
    {
        return new List<Weapon_Data>(selectedWeapons);
    }

    public static void SetSelectedMission(Mission mission)
    {
        SelectedMission = mission;
    }

    public static void SetFriendlyFire(bool friendlyFire)
    {
        FriendlyFire = friendlyFire;
        HasFriendlyFireSetting = true;
    }

    public static void MarkGameplayRequestedFromMenu()
    {
        GameplayRequestedFromMenu = true;
    }

    public static void ClearGameplayRequest()
    {
        GameplayRequestedFromMenu = false;
    }

    public static void BeginCoopGameplaySession(int levelSeed)
    {
        IsCoopSession = true;
        LevelGenerationSeed = levelSeed;
        GameplayRequestedFromMenu = true;
    }

    public static void EndCoopGameplaySession()
    {
        IsCoopSession = false;
        LevelGenerationSeed = 0;
        weaponsByPlayerId.Clear();
    }

    public static void SetLocalDisplayName(string displayName)
    {
        LocalDisplayName = displayName ?? string.Empty;
    }

    public static void SetDisplayNameForPlayer(int playerId, string displayName)
    {
        if (playerId < 0)
            return;

        if (string.IsNullOrWhiteSpace(displayName))
            displayNamesByPlayerId.Remove(playerId);
        else
            displayNamesByPlayerId[playerId] = displayName.Trim();
    }

    public static string GetDisplayNameForPlayer(int playerId)
    {
        if (playerId < 0)
            return string.Empty;

        return displayNamesByPlayerId.TryGetValue(playerId, out string displayName)
            ? displayName
            : string.Empty;
    }

    public static IReadOnlyDictionary<int, string> GetDisplayNamesByPlayerId()
    {
        return displayNamesByPlayerId;
    }

    public static void SetWeaponsForPlayer(int playerId, IEnumerable<Weapon_Data> weapons)
    {
        if (playerId < 0)
            return;

        if (weapons == null)
        {
            weaponsByPlayerId.Remove(playerId);
            return;
        }

        var list = new List<Weapon_Data>();
        foreach (Weapon_Data weapon in weapons)
        {
            if (weapon != null)
                list.Add(weapon);
        }

        weaponsByPlayerId[playerId] = list;
    }

    public static List<Weapon_Data> GetWeaponsForPlayer(int playerId)
    {
        if (playerId < 0 || weaponsByPlayerId.TryGetValue(playerId, out List<Weapon_Data> weapons) == false)
            return new List<Weapon_Data>();

        return new List<Weapon_Data>(weapons);
    }

    public static void ClearCoopLobbySession()
    {
        EndCoopGameplaySession();
        displayNamesByPlayerId.Clear();
        LocalDisplayName = string.Empty;
    }

    public static void ClearGameplaySession()
    {
        selectedWeapons.Clear();
        SelectedMission = null;
        GameplayRequestedFromMenu = false;
        EndCoopGameplaySession();
    }
}
