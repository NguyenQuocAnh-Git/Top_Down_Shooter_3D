using System.Collections;
using Fusion;
using UnityEngine;

public class CoopPostMatchFlow : MonoBehaviour
{
    [SerializeField] private float returnDelay = 5f;
    private NetworkRunner runner;
    private bool resultShown;

    public void Initialize(NetworkRunner activeRunner)
    {
        runner = activeRunner;
        CoopNetworkManager.Instance.OnCoopMatchResultReceived += HandleMatchResult;
    }

    private void HandleMatchResult(bool victory)
    {
        if (resultShown)
            return;

        resultShown = true;
        ControlsManager.instance?.SwitchToUIControls();

        if (victory == false)
            TimeManager.instance?.SlowMotionFor(1.5f);

        if (victory)
            UI.instance?.ShowVictoryScreenUI();
        else
            UI.instance?.ShowGameOverUI("TEAM WIPED!");

        if (runner != null && runner.IsServer)
            StartCoroutine(ReturnHostToLobby());
    }

    private IEnumerator ReturnHostToLobby()
    {
        yield return new WaitForSecondsRealtime(returnDelay);
        CoopNetworkManager.Instance.ReturnPostMatchToCoopLobby();
    }

    private void OnDestroy()
    {
        if (CoopNetworkManager.Instance != null)
            CoopNetworkManager.Instance.OnCoopMatchResultReceived -= HandleMatchResult;
    }
}
