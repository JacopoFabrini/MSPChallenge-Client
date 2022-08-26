﻿using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MSP2050.Scripts
{
    public class Options : MonoBehaviour
    {
        private float oldScale;
        private int oldDisplayResolution;
        private int oldGraphicsSettings;
        private bool oldFullscreen;

        private float oldMasterVolume;
        private float oldSoundEffects;

        // Graphics
        public CustomSlider uiScale;
        public CustomDropdown displayResolution;
        //public CustomDropdown qualitySettings;
        public Toggle fullscreenToggle;
        public UnityEvent onDisplaySettingsChange;

        // Audio
        public Slider masterVolume;
        public Slider soundEffects;

        // Other
        public Toggle developerModeToggle;
        public TextMeshProUGUI buildDateText;
        public TextMeshProUGUI buildRevisionText;
        public TextMeshProUGUI apiEndpointText;

        public Button cancel, accept;
        public bool closeWindowOnCancelConfirm = true;

        protected void Awake()
        {
            displayResolution.ClearOptions();
            if (cancel != null)
                cancel.onClick.AddListener(OnCancel);
            if (accept != null)
                accept.onClick.AddListener(OnAccept);
        }

        protected void Start()
        {
            List<string> resNames = new List<string>();

            foreach (Vector2 res in GameSettings.Instance.Resolutions)
            {
                // format 16:10 - 1680 x 1050
                string name = "" + res.x + " x " + res.y;
                resNames.Add(name);
            }
            displayResolution.AddOptions(resNames);

            fullscreenToggle.isOn = GameSettings.Instance.Fullscreen;

            developerModeToggle.isOn = Main.IsDeveloper;
            uiScale.m_onRelease.AddListener(OnUIScaleSliderUp);

            SetBuildInformation();
            SetAPIEndpointInfo();
            SetOptions();
        }

        public void OnAccept()
        {
            if (closeWindowOnCancelConfirm)
                gameObject.SetActive(false);
        }

        // Only apply this on pointer up
        public void OnUIScaleSliderUp()
        {
            GameSettings.Instance.SetUIScale(uiScale.value);
            InterfaceCanvas.Instance?.propertiesWindow.Close();
            onDisplaySettingsChange.Invoke();
        }

        public void OnCancel()
        {
            GameSettings.Instance.SetUIScale(oldScale);
            GameSettings.Instance.SetQualityLevel(oldGraphicsSettings);
            GameSettings.Instance.SetResolution(oldDisplayResolution);
            GameSettings.Instance.SetMasterVolume(oldMasterVolume);
            GameSettings.Instance.SetSFXVolume(oldSoundEffects);
            GameSettings.Instance.SetFullscreen(oldFullscreen);

            if (closeWindowOnCancelConfirm)
                gameObject.SetActive(false);
            onDisplaySettingsChange.Invoke();
        }

        protected void OnEnable()
        {
            SetOptions();
        }

        private void SetBuildInformation()
        {
            if (!ApplicationBuildIdentifier.Instance.GetHasInformation())
                ApplicationBuildIdentifier.Instance.GetUCBManifest();

            buildDateText.text = ApplicationBuildIdentifier.Instance.GetBuildTime();
            buildRevisionText.text = ApplicationBuildIdentifier.Instance.GetGitTag();
        }

        private void SetAPIEndpointInfo()
        {
            apiEndpointText.text = Server.Url;
        }

        private void SetOptions()
        {
            oldScale = GameSettings.Instance.UIScale;
            oldGraphicsSettings = GameSettings.Instance.GraphicsSettings;
            oldMasterVolume = GameSettings.Instance.GetMasterVolume();
            oldSoundEffects = GameSettings.Instance.GetSFXVolume();
            oldFullscreen = GameSettings.Instance.Fullscreen;

            if (Application.isEditor) // This is to fix it for the editor, it only ever displays one resolution
                oldDisplayResolution = 0;
            else
                oldDisplayResolution = GameSettings.Instance.DisplayResolution;

            uiScale.value = oldScale;
            //uiScale.maxValue = GameSettings.GetMaxUIScaleForWidth(Camera.main.pixelWidth);//TODO: disabled for testing

            //qualitySettings.value = oldGraphicsSettings;
            displayResolution.value = oldDisplayResolution;
            masterVolume.value = oldMasterVolume;
            soundEffects.value = oldSoundEffects;

            uiScale.onValueChanged.RemoveAllListeners();
            //qualitySettings.onValueChanged.RemoveAllListeners();
            displayResolution.onValueChanged.RemoveAllListeners();
            masterVolume.onValueChanged.RemoveAllListeners();
            soundEffects.onValueChanged.RemoveAllListeners();
            fullscreenToggle.onValueChanged.RemoveAllListeners();
            masterVolume.onValueChanged.RemoveAllListeners();

            //uiScale.slider.onValueChanged.AddListener((value) => { GameSettings.Instance.SetUIScale(value); });
            // qualitySettings.onValueChanged.AddListener((value) => { GameSettings.Instance.SetQualityLevel(value); });
            displayResolution.onValueChanged.AddListener(OnResolutionChanged);
            fullscreenToggle.onValueChanged.AddListener(b => GameSettings.Instance.SetFullscreen(b));
            developerModeToggle.onValueChanged.AddListener((value) => { Main.IsDeveloper = value; });
            masterVolume.onValueChanged.AddListener((value) => { GameSettings.Instance.SetMasterVolume(value); playSoundEffect(AudioMain.VOLUME_TEST); });
            soundEffects.onValueChanged.AddListener((value) => { GameSettings.Instance.SetSFXVolume(value); playSoundEffect(AudioMain.VOLUME_TEST); });
        }

        private void OnResolutionChanged(int resolutionIndex)
        {
            Vector2 newResolution = GameSettings.Instance.SetResolution(resolutionIndex);
            // float maxUIScale = GameSettings.GetMaxUIScaleForWidth(newResolution.x);
            // uiScale.maxValue = maxUIScale;
            // if (GameSettings.Instance.UIScale > maxUIScale)
            // 	GameSettings.Instance.SetUIScale(maxUIScale);
            //StartCoroutine(LateUpdatePosition());
            if (InterfaceCanvas.Instance != null)
                StartCoroutine(InterfaceCanvas.Instance.gameMenu.LateUpdatePosition());
            onDisplaySettingsChange.Invoke();
        }

        private void playSoundEffect(string audioID)
        {
            AudioSource audioSource = AudioMain.Instance.GetAudioSource(audioID);
            if (audioSource != null && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
    }
}