using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_MissionSelectionButton : UI_Button
{
    private UI_MissionSelection missionUI;
    private TextMeshProUGUI myText;
    private Image myImage;
    private bool localInteractionEnabled = true;
    private bool coopMode;

    [SerializeField] private Mission myMission;
    public Mission Mission => myMission;

    private void OnValidate()
    {
        gameObject.name = "Button - Select Mission: " + myMission.missionName;
    }
    public override void Start()
    {
        base.Start();
        missionUI = GetComponentInParent<UI_MissionSelection>();
        myText = GetComponentInChildren<TextMeshProUGUI>();
        myImage = GetComponent<Image>();
        if (myText != null && myMission != null)
            myText.text = myMission.missionName;

        ApplyLocalInteractionState();
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        if (CanUseLocalInteraction() == false)
            return;

        base.OnPointerEnter(eventData);

        if (missionUI != null && myMission != null)
            missionUI.UpdateMissionDesicription(myMission.missionDescription);

        if (CoopNetworkManager.Instance != null
            && CoopNetworkManager.Instance.IsInRoom
            && CoopNetworkManager.Instance.CanLocalPlayerSelectMission)
        {
            CoopNetworkManager.Instance.PreviewCoopMission(myMission);
        }
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        if (CanUseLocalInteraction() == false)
            return;

        base.OnPointerExit(eventData);
        missionUI?.ShowDefaultDescription();

        if (CoopNetworkManager.Instance != null
            && CoopNetworkManager.Instance.IsInRoom
            && CoopNetworkManager.Instance.CanLocalPlayerSelectMission)
        {
            CoopNetworkManager.Instance.ClearCoopMissionPreview();
        }
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (CanUseLocalInteraction() == false)
            return;

        base.OnPointerDown(eventData);

        if (coopMode || (CoopNetworkManager.Instance != null && CoopNetworkManager.Instance.IsInRoom))
        {
            if (CoopNetworkManager.Instance == null || CoopNetworkManager.Instance.CanLocalPlayerSelectMission == false)
            {
                Debug.Log("[COOP FLOW] Mission button click ignored. Only host can select mission.");
                return;
            }

            if (CoopNetworkManager.Instance != null && CoopNetworkManager.Instance.CanLocalPlayerSelectMission)
                CoopNetworkManager.Instance.SelectCoopMission(myMission);

            return;
        }

        GameSessionData.SetSelectedMission(myMission);

        if (MissionManager.instance != null)
            MissionManager.instance.SetCurrentMission(myMission);
    }

    public void SetRemotePreview(bool previewed)
    {
        Color color = previewed ? Color.yellow : Color.white;

        if (myImage == null)
            myImage = GetComponent<Image>();

        if (myText == null)
            myText = GetComponentInChildren<TextMeshProUGUI>();

        if (myImage != null)
            myImage.color = color;

        if (myText != null)
            myText.color = color;
    }

    public void SetLocalInteractionEnabled(bool enabled, bool isCoopMode)
    {
        coopMode = isCoopMode;
        localInteractionEnabled = enabled;
        ApplyLocalInteractionState();

        if (enabled == false)
            SetRemotePreview(false);
    }

    private void ApplyLocalInteractionState()
    {
        Button button = GetComponent<Button>();
        if (button != null)
            button.interactable = localInteractionEnabled;
    }

    private bool CanUseLocalInteraction()
    {
        if (localInteractionEnabled == false)
            return false;

        if (coopMode && (CoopNetworkManager.Instance == null || CoopNetworkManager.Instance.CanLocalPlayerSelectMission == false))
            return false;

        Button button = GetComponent<Button>();
        return button == null || button.interactable;
    }
}
