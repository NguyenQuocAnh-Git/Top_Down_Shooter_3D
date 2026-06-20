using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_ComicPanel : MonoBehaviour, IPointerDownHandler
{
    private Image myImage;

    [SerializeField] private Image[] comicPanel;
    [SerializeField] private GameObject buttonToEnable;

    private bool comicShowOver;
    private int imageIndex;
    private bool playButtonInteractable = true;
    private bool pointerAdvanceEnabled = true;
    private bool started;
    private Button mainMenuButton;

    private void Awake()
    {
        CacheMainMenuButton();
    }

    private void Start()
    {
        myImage = GetComponent<Image>();
        ResetComicShow();
        StartComicShow();
    }

    private void OnEnable()
    {
        if (started == false)
            return;

        ResetComicShow();
        StartComicShow();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    public void RestartComicShow()
    {
        ResetComicShow();
        StartComicShow();
    }

    private void ResetComicShow()
    {
        StopAllCoroutines();
        comicShowOver = false;
        imageIndex = 0;

        if (myImage == null)
            myImage = GetComponent<Image>();

        if (myImage != null)
            myImage.raycastTarget = true;

        foreach (Image image in comicPanel)
        {
            if (image == null)
                continue;

            Color color = image.color;
            image.color = new Color(color.r, color.g, color.b, 0);
        }

        if (buttonToEnable != null)
            buttonToEnable.SetActive(false);
    }

    private void StartComicShow()
    {
        started = true;
        ShowNextImage();
    }

    protected void ShowNextImage()
    {
        if (comicShowOver)
            return;

        StartCoroutine(ChangeImageAlpha(1,1.5f,ShowNextImage));
    }

    private IEnumerator ChangeImageAlpha(float targetAlpha, float duration, System.Action onComplete)
    {
        float time = 0;
        Color currentColor = comicPanel[imageIndex].color;
        float startAlpha = currentColor.a;

        while (time < duration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);

            comicPanel[imageIndex].color = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);
            yield return null;
        }

        comicPanel[imageIndex].color = new Color(currentColor.r, currentColor.g, currentColor.b, targetAlpha);

        imageIndex++;

        if(imageIndex >= comicPanel.Length)
        {
            FinishComicShow();
        }

        // Call the cimpletion method if it exists
        onComplete?.Invoke();
    }

    private void FinishComicShow()
    {
        StopAllCoroutines();
        comicShowOver = true;
        if (buttonToEnable != null)
            buttonToEnable.SetActive(true);

        ApplyPlayButtonInteractable();

        if (myImage != null)
            myImage.raycastTarget = false;
    }

    public void SetPlayButtonInteractable(bool interactable)
    {
        playButtonInteractable = interactable;
        ApplyPlayButtonInteractable();
    }

    public void ConfigurePlayButton(bool interactable, UnityEngine.Events.UnityAction onClickAction)
    {
        playButtonInteractable = interactable;

        Button playButton = buttonToEnable != null
            ? buttonToEnable.GetComponent<Button>()
            : null;

        if (playButton != null && onClickAction != null)
        {
            playButton.onClick = new Button.ButtonClickedEvent();
            playButton.onClick.AddListener(onClickAction);
        }

        ApplyPlayButtonInteractable();
    }

    public void SetPointerAdvanceEnabled(bool enabled)
    {
        pointerAdvanceEnabled = enabled;
    }

    public void ConfigureForCoop()
    {
        SetMainMenuButtonVisible(false);
    }

    public void ConfigureForSinglePlayer()
    {
        SetMainMenuButtonVisible(true);
    }

    private void CacheMainMenuButton()
    {
        if (mainMenuButton != null)
            return;

        Button[] buttons = GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button.name != "Button - Main Menu")
                continue;

            mainMenuButton = button;
            return;
        }
    }

    private void SetMainMenuButtonVisible(bool visible)
    {
        if (mainMenuButton != null)
            mainMenuButton.gameObject.SetActive(visible);
    }

    private void ApplyPlayButtonInteractable()
    {
        if (buttonToEnable == null)
            return;

        Button playButton = buttonToEnable.GetComponent<Button>();
        if (playButton != null)
            playButton.interactable = true;

        CanvasGroup canvasGroup = buttonToEnable.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = buttonToEnable.AddComponent<CanvasGroup>();

        canvasGroup.interactable = playButtonInteractable;
        canvasGroup.blocksRaycasts = playButtonInteractable;
        canvasGroup.alpha = 1f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pointerAdvanceEnabled == false)
            return;

        ShowNextImageOnClick();
    }

    private void ShowNextImageOnClick()
    {
        if (comicShowOver || imageIndex >= comicPanel.Length)
            return;

        comicPanel[imageIndex].color = Color.white;
        imageIndex++;

        if (imageIndex >= comicPanel.Length)
            FinishComicShow();

        if (comicShowOver)
            return;

        ShowNextImage();
    }
}
