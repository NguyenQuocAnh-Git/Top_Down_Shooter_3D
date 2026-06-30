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
        Debug.Log($"[COOP] Showing local match result: {(victory ? "VICTORY" : "GAME OVER")}.");

        // Show the result first so an unrelated input/time effect can never
        // prevent the Win/Lose screen from becoming visible.
        if (victory)
            UI.instance?.ShowVictoryScreenUI();
        else
            UI.instance?.ShowGameOverUI("TEAM WIPED!");

        ControlsManager.instance?.SwitchToUIControls();

        if (victory == false)
            TimeManager.instance?.SlowMotionFor(1.5f);

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
