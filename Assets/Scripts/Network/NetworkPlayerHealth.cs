using Fusion;
using UnityEngine;

public class NetworkPlayerHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 100;

    [Networked] public int CurrentHealth { get; private set; }
    [Networked] public NetworkBool IsDead { get; private set; }

    private NetworkPlayer networkPlayer;

    public int MaxHealth => maxHealth;

    private void Awake()
    {
        networkPlayer = GetComponent<NetworkPlayer>();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
            CurrentHealth = maxHealth;

        RefreshLocalHealthUI();
    }

    public void ApplyDamageFromHost(int damage)
    {
        if (Object.HasStateAuthority == false || IsDead)
            return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);

        if (CurrentHealth <= 0)
            IsDead = true;
    }

    public override void Render()
    {
        RefreshLocalHealthUI();
    }

    private void RefreshLocalHealthUI()
    {
        if (Object.HasInputAuthority == false || UI.instance == null || UI.instance.inGameUI == null)
            return;

        UI.instance.inGameUI.UpdateHealthUI(CurrentHealth, maxHealth);
    }
}
