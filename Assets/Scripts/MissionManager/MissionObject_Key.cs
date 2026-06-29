using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissionObject_Key : MonoBehaviour
{
    private GameObject player;
    public static event Action OnKeyPickedUp;

    private void Awake()
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
                CompletePickupFromHost();
            else
                CoopNetworkManager.Instance.SendCoopKeyPickupRequest(transform.position);

            return;
        }

        if (other.gameObject != player)
            return;

        CompletePickupFromHost();
    }

    public void CompletePickupFromHost()
    {
        Vector3 position = transform.position;
        OnKeyPickedUp?.Invoke();
        Destroy(gameObject);

        if (GameSessionData.IsCoopSession && CoopNetworkManager.Instance.IsHosting)
            CoopNetworkManager.Instance.BroadcastCoopKeyRemoved(position);
    }

    public void HideWithoutMissionEvent()
    {
        Destroy(gameObject);
    }
}
