using System.Collections.Generic;

public static class GameSessionData
{
    public const string MenuSceneName = "MenuScene";
    public const string GameplaySceneName = "GameplayScene";

    private static readonly List<Weapon_Data> selectedWeapons = new List<Weapon_Data>();

    public static Mission SelectedMission { get; private set; }
    public static bool GameplayRequestedFromMenu { get; private set; }
    public static bool FriendlyFire { get; private set; }
    public static bool HasFriendlyFireSetting { get; private set; }

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

    public static void ClearGameplaySession()
    {
        selectedWeapons.Clear();
        SelectedMission = null;
        GameplayRequestedFromMenu = false;
    }
}
