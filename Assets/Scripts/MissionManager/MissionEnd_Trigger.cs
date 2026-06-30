using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissionEnd_Trigger : MonoBehaviour
{
    [SerializeField, Min(0f)] private float activationPadding = 1.5f;

    private GameObject player;
    private bool localCoopExtractionReported;
    private bool offlineVictoryTriggered;

    private void Start()
    {
        Player scenePlayer = FindObjectOfType<Player>();
        player = scenePlayer != null ? scenePlayer.gameObject : null;
    }

    private void Update()
    {
        if (GameSessionData.IsCoopSession || offlineVictoryTriggered || player == null)
            return;

        if (ContainsWorldPosition(player.transform.position))
            TryCompleteOffline(player.GetComponent<Player>());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (TryHandleCoopExtraction(other))
            return;

        TryCompleteOffline(other.GetComponentInParent<Player>());
    }

    private void OnTriggerStay(Collider other)
    {
        // Network movement and generated level setup can make a client appear
        // inside the trigger without receiving OnTriggerEnter. Stay provides a
        // reliable fallback while the host also validates the player position.
        if (TryHandleCoopExtraction(other) == false)
            TryCompleteOffline(other.GetComponentInParent<Player>());
    }

    public bool ContainsWorldPosition(Vector3 worldPosition)
    {
        Collider[] triggerColliders = GetComponents<Collider>();
        foreach (Collider triggerCollider in triggerColliders)
        {
            if (triggerCollider == null || triggerCollider.enabled == false)
                continue;

            Bounds activationBounds = triggerCollider.bounds;
            activationBounds.Expand(activationPadding * 2f);
            if (activationBounds.Contains(worldPosition))
                return true;
        }

        return false;
    }

    private bool TryHandleCoopExtraction(Collider other)
    {
        if (GameSessionData.IsCoopSession == false)
            return false;

        NetworkPlayer networkPlayer = other.GetComponentInParent<NetworkPlayer>();
        if (networkPlayer == null || networkPlayer.Object == null)
            return true;

        CoopNetworkManager manager = CoopNetworkManager.Instance;
        if (manager == null)
            return true;

        if (manager.IsHosting)
        {
            CoopMissionSync.Instance?.TryCompleteAtExtraction(networkPlayer);
            return true;
        }

        // A client may only report its own authoritative player. Remote
        // replicas entering this local trigger must not be reported as local.
        if (networkPlayer.Object.HasInputAuthority == false || localCoopExtractionReported)
            return true;

        localCoopExtractionReported = true;
        Debug.Log($"[COOP] Local client reached extraction as player {networkPlayer.Object.InputAuthority.PlayerId}.");
        manager.SendCoopExtractionReached(networkPlayer.Object.InputAuthority.PlayerId);
        return true;
    }

    private void TryCompleteOffline(Player candidate)
    {
        if (offlineVictoryTriggered
            || candidate == null
            || candidate.gameObject != player
            || GameManager.instance == null)
            return;

        offlineVictoryTriggered = true;
        Debug.Log("[MISSION] Player reached the end-path extraction. Showing victory UI without mission requirement.");
        GameManager.instance.GameCompleted();
    }
}
