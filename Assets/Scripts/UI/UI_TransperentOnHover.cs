using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_TransperentOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Dictionary<Image,Color> originalImageColors = new Dictionary<Image,Color>();
    private Dictionary<TextMeshProUGUI, Color> originalTextColors = new Dictionary<TextMeshProUGUI, Color>();


    private bool hasUIWeaponSlots;
    private Player_WeaponController playerWeaponController;

    private void Start()
    {
        hasUIWeaponSlots = GetComponentInChildren<UI_WeaponSlot>() != null;

        if (hasUIWeaponSlots && GameSessionData.IsCoopSession == false)
            playerWeaponController = ResolveActivePlayerWeaponController();

        // Chache Image components and their original colors
        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            originalImageColors[image] = image.color;
        }

        // Chache TextMeshProUGUI components and their original colors
        foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            originalTextColors[text] = text.color;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {

        //Set all images to transperent
        foreach (var image in originalImageColors.Keys)
        {
            var color = image.color;
            color.a = .15f;
            image.color = color;
        }

        //Set all texts to transperent
        foreach (var text in originalTextColors.Keys)
        {
            var color = text.color;
            color.a = .15f;
            text.color = color;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Restore original colors for images
        foreach (var image in originalImageColors.Keys)
        {
            image.color = originalImageColors[image];
        }

        // Restore original colors for texts
        foreach (var text in originalTextColors.Keys)
        {
            text.color = originalTextColors[text];
        }

        if (playerWeaponController != null)
            playerWeaponController.UpdateWeaponUI();
    }

    private static Player_WeaponController ResolveActivePlayerWeaponController()
    {
        if (GameManager.instance != null
            && GameManager.instance.player != null
            && GameManager.instance.player.gameObject.activeInHierarchy)
        {
            return GameManager.instance.player.weapon;
        }

        Player_WeaponController[] controllers = FindObjectsOfType<Player_WeaponController>(true);

        foreach (Player_WeaponController controller in controllers)
        {
            if (controller != null && controller.gameObject.activeInHierarchy)
                return controller;
        }

        return null;
    }
}
