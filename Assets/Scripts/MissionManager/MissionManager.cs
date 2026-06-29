using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissionManager : MonoBehaviour
{
    public static MissionManager instance;


    public Mission currentMission;

    private void Awake()
    {
        instance = this;

        if (GameSessionData.HasSelectedMission)
            currentMission = GameSessionData.SelectedMission;
    }


    private void Update()
    {
        if (GameSessionData.IsCoopSession
            && (CoopNetworkManager.Instance == null || CoopNetworkManager.Instance.IsHosting == false))
            return;

        currentMission?.UpdateMission();
    }

    public void SetCurrentMission(Mission newMission)
    {
        currentMission = newMission;
    }

    public void StartMission() => currentMission.StartMission();

    public bool MissionCompleted() => currentMission.MissionCompleted();


}
