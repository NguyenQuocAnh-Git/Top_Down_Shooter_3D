using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class CoopPickupCoordinator : MonoBehaviour
{
    private const float InteractionRange = 2.5f;
    private const float PositionTolerance = 0.8f;

    public static CoopPickupCoordinator Instance { get; private set; }

    private readonly HashSet<string> claimedPickups = new HashSet<string>();
    private NetworkRunner runner;

    public void Initialize(NetworkRunner activeRunner)
    {
        Instance = this;
        runner = activeRunner;
        CoopNetworkManager.Instance.OnCoopPickupClaimRequested += HandleClaimRequest;
        CoopNetworkManager.Instance.OnCoopPickupResolved += HandleClaimResolved;
    }

    public void RequestNearestPickup(NetworkPlayer player)
    {
        if (player == null || player.Object == null || player.Object.HasInputAuthority == false)
            return;

        Interactable nearest = FindNearest(player.transform.position, InteractionRange, -1);
        if (nearest != null)
            CoopNetworkManager.Instance.SendCoopPickupClaim(EncodeClaim(PickupKind(nearest), nearest.transform.position, -1));
    }

    private void HandleClaimRequest(int playerId, byte[] payload)
    {
        if (runner == null || runner.IsServer == false || TryDecodeClaim(payload, out byte kind, out Vector3 requestedPosition, out _) == false)
            return;

        NetworkPlayer claimant = FindPlayer(playerId);
        if (claimant == null || claimant.Health == null || claimant.Health.IsDead)
            return;

        Interactable pickup = FindNearest(requestedPosition, PositionTolerance, kind);
        if (pickup == null || Vector3.Distance(claimant.transform.position, pickup.transform.position) > InteractionRange)
            return;

        if (claimedPickups.Add(PickupId(kind, pickup.transform.position)) == false)
            return;

        CoopNetworkManager.Instance.BroadcastCoopPickupResolved(EncodeClaim(kind, pickup.transform.position, playerId));
    }

    private void HandleClaimResolved(byte[] payload)
    {
        if (TryDecodeClaim(payload, out byte kind, out Vector3 position, out int claimantId) == false)
            return;

        claimedPickups.Add(PickupId(kind, position));
        Interactable pickup = FindNearest(position, PositionTolerance, kind);
        if (pickup == null)
            return;

        if (runner != null && runner.LocalPlayer.PlayerId == claimantId && runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject playerObject))
        {
            NetworkPlayerWeapon weapon = playerObject.GetComponent<NetworkPlayerWeapon>();
            (pickup as Pickup_Ammo)?.GrantToNetworkPlayer(weapon);
            (pickup as Pickup_Weapon)?.GrantToNetworkPlayer(weapon);
        }

        pickup.gameObject.SetActive(false);
    }

    private static NetworkPlayer FindPlayer(int playerId)
    {
        foreach (NetworkPlayer player in FindObjectsOfType<NetworkPlayer>())
        {
            if (player.Object != null && player.Object.InputAuthority.PlayerId == playerId)
                return player;
        }
        return null;
    }

    private static Interactable FindNearest(Vector3 position, float maxDistance, int requiredKind)
    {
        Interactable nearest = null;
        float nearestDistance = maxDistance;

        foreach (Pickup_Ammo pickup in FindObjectsOfType<Pickup_Ammo>())
            Consider(pickup, 0);
        foreach (Pickup_Weapon pickup in FindObjectsOfType<Pickup_Weapon>())
            Consider(pickup, 1);

        return nearest;

        void Consider(Interactable candidate, int kind)
        {
            if (requiredKind >= 0 && kind != requiredKind)
                return;

            float distance = Vector3.Distance(position, candidate.transform.position);
            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }
    }

    private static byte PickupKind(Interactable pickup) => pickup is Pickup_Weapon ? (byte)1 : (byte)0;

    private static string PickupId(byte kind, Vector3 position) =>
        $"{kind}:{Mathf.RoundToInt(position.x * 10f)}:{Mathf.RoundToInt(position.y * 10f)}:{Mathf.RoundToInt(position.z * 10f)}";

    private static byte[] EncodeClaim(byte kind, Vector3 position, int claimantId)
    {
        byte[] payload = new byte[17];
        payload[0] = kind;
        Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, payload, 1, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, payload, 5, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, payload, 9, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(claimantId), 0, payload, 13, 4);
        return payload;
    }

    private static bool TryDecodeClaim(byte[] payload, out byte kind, out Vector3 position, out int claimantId)
    {
        kind = 0;
        position = default;
        claimantId = -1;
        if (payload == null || payload.Length < 17)
            return false;

        kind = payload[0];
        position = new Vector3(BitConverter.ToSingle(payload, 1), BitConverter.ToSingle(payload, 5), BitConverter.ToSingle(payload, 9));
        claimantId = BitConverter.ToInt32(payload, 13);
        return kind <= 1;
    }

    private void OnDestroy()
    {
        if (CoopNetworkManager.Instance != null)
        {
            CoopNetworkManager.Instance.OnCoopPickupClaimRequested -= HandleClaimRequest;
            CoopNetworkManager.Instance.OnCoopPickupResolved -= HandleClaimResolved;
        }

        if (Instance == this)
            Instance = null;
    }
}
