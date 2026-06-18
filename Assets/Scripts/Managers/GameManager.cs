using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public Player player;

    [Header("Settings")]
    public bool friendlyFire;
    [Space]
    public bool quickStart;
    [SerializeField] private List<Weapon_Data> fallbackWeaponData;

    private void Awake()
    {
        instance = this;

        if (GameSessionData.HasFriendlyFireSetting)
            friendlyFire = GameSessionData.FriendlyFire;

        player = FindObjectOfType<Player>();
    }

  
    public void GameStart()
    {
        SetDefaultWeaponsForPlayer();

        //LevelGenerator.instance.InitializeGeneration();
        // We start selected mission in a LevelGenerator script ,after we done with level creation.
    }

    public void RestartScene() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

    public void GameCompleted()
    {
        UI.instance.ShowVictoryScreenUI();
        ControlsManager.instance.controls.Character.Disable();
        player.health.currentHealth += 99999; // So player won't die in last second.
    }
    public void GameOver()
    {
        TimeManager.instance.SlowMotionFor(1.5f);
        UI.instance.ShowGameOverUI();
        CameraManager.instance.ChangeCameraDistance(5);
    }

    private void SetDefaultWeaponsForPlayer()
    {
        List<Weapon_Data> newList = GameSessionData.GetSelectedWeapons();

        if (newList.Count == 0 && UI.instance != null && UI.instance.weaponSelection != null)
            newList = UI.instance.weaponSelection.SelectedWeaponData();

        if (newList.Count == 0 && fallbackWeaponData != null)
            newList = new List<Weapon_Data>(fallbackWeaponData);

        player.weapon.SetDefaultWeapon(newList);
    }
}
