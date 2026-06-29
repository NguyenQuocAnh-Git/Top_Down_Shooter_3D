using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissionEnd_Trigger : MonoBehaviour
{
    private GameObject player;

    private void Start()
    {
        player = GameObject.Find("Player");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (GameSessionData.IsCoopSession)
        {
            NetworkPlayer networkPlayer = other.GetComponentInParent<NetworkPlayer>();
            if (networkPlayer == null || networkPlayer.Object == null)
                return;

            if (CoopNetworkManager.Instance.IsHosting)
            {
                CoopMissionSync.Instance?.TryCompleteAtExtraction(networkPlayer);
            }
            else
            {
                CoopNetworkManager.Instance.SendCoopExtractionReached(networkPlayer.Object.InputAuthority.PlayerId);
            }

            return;
        }

        if (other.gameObject != player)
            return;

        if (MissionManager.instance.MissionCompleted())
        {
            GameManager.instance.GameCompleted();
            Debug.Log("Level completed!");
        }
    }
}
