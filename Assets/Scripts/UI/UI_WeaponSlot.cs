using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_WeaponSlot : MonoBehaviour
{
    public Image weaponIcon;
    public TextMeshProUGUI ammoText;

    private void Awake()
    {
        weaponIcon = GetComponentInChildren<Image>();
        ammoText = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void UpdateWeaponSlot(Weapon myWeapon, bool activeWeapon)
    {
        if (weaponIcon == null)
            weaponIcon = GetComponentInChildren<Image>();

        if (ammoText == null)
            ammoText = GetComponentInChildren<TextMeshProUGUI>();

        if (myWeapon == null || myWeapon.weaponData == null)
        {
            if (weaponIcon != null)
                weaponIcon.color = Color.clear;

            if (ammoText != null)
                ammoText.text = string.Empty;

            return;
        }

        Color newColor = activeWeapon ? Color.white : new Color(1, 1, 1, .35f);

        if (weaponIcon != null)
        {
            weaponIcon.color = newColor;
            weaponIcon.sprite = myWeapon.weaponData.weaponIcon;
        }

        if (ammoText != null)
        {
            ammoText.text = myWeapon.bulletsInMagazine + "/" + myWeapon.totalReserveAmmo;
            ammoText.color = Color.white;
        }
    }
}
