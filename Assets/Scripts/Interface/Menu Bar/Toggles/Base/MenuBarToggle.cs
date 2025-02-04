﻿using UnityEngine;

namespace MSP2050.Scripts
{
	public class MenuBarToggle : MonoBehaviour
	{
		public enum Selection { Logo, Layers, PlanWizard, ObjectivesMonitor, PlansMonitor, ImpactTool, ActiveLayers, GameMenu };

		[Header("Connects to the correct toggle")]
		public Selection connectTo;
	
		public CustomToggle toggle;

		void Start()
		{
			toggle = GetComponent<CustomToggle>();

			switch (connectTo) {
				case Selection.Logo:
					if (Main.MspGlobalData != null)               
						SetRegionButtonCallback();                
					else               
						Main.OnGlobalDataLoaded += GlobalDataLoaded;               
					break;
				case Selection.Layers:
					toggle.isOn = InterfaceCanvas.Instance.layerPanel.gameObject.activeSelf; // Init
					toggle.onValueChanged.AddListener((b) => InterfaceCanvas.Instance.layerPanel.gameObject.SetActive(toggle.isOn));
					toggle.onValueChanged.AddListener((b) => InterfaceCanvas.Instance.layerPanel.DisableLayerSelect(toggle.isOn));
					break;
				case Selection.PlanWizard:
					toggle.isOn = InterfaceCanvas.Instance.planWizard.gameObject.activeSelf; // Init
					toggle.onValueChanged.AddListener((b) => InterfaceCanvas.Instance.planWizard.gameObject.SetActive(toggle.isOn));
					toggle.onValueChanged.AddListener((bool b) => { if (b) InterfaceCanvas.Instance.planWizard.SetToPlan(null); } );
					break;
				case Selection.ObjectivesMonitor:
					if (InterfaceCanvas.Instance.objectivesMonitor) {
						toggle.isOn = false; // Init
						toggle.onValueChanged.AddListener((b) => InterfaceCanvas.Instance.objectivesMonitor.SetWindowActive(toggle.isOn));
					}
					break;
				case Selection.PlansMonitor:
					toggle.isOn = InterfaceCanvas.Instance.plansMonitor.gameObject.activeSelf; // Init
					toggle.onValueChanged.AddListener((b) => InterfaceCanvas.Instance.plansMonitor.gameObject.SetActive(toggle.isOn));
					break;
				case Selection.ImpactTool:
					toggle.isOn = InterfaceCanvas.Instance.impactToolWindow.gameObject.activeSelf;
					toggle.onValueChanged.AddListener((b) => InterfaceCanvas.Instance.impactToolWindow.gameObject.SetActive(toggle.isOn));
					Main.OnGlobalDataLoaded += GlobalDataLoaded;
					break;
				case Selection.ActiveLayers:
					toggle.isOn = InterfaceCanvas.Instance.activeLayers.gameObject.activeSelf; // Init
					toggle.onValueChanged.AddListener((b) => InterfaceCanvas.Instance.activeLayers.gameObject.SetActive(toggle.isOn));
					break;
				case Selection.GameMenu:
					toggle.isOn = InterfaceCanvas.Instance.gameMenu.gameObject.activeSelf; // Init
					toggle.onValueChanged.AddListener((b) => InterfaceCanvas.Instance.gameMenu.gameObject.SetActive(toggle.isOn));
					break;
			}
		}

		//Called from close buttons of associated windows
		public void ToggleValue()
		{
			toggle.isOn = !toggle.isOn;

		}

		void GlobalDataLoaded()
		{
			if (connectTo == Selection.Logo)
			{
				Main.OnGlobalDataLoaded -= GlobalDataLoaded;
				SetRegionButtonCallback();
			}
			else if (connectTo == Selection.ImpactTool)
			{
				toggle.gameObject.SetActive(Main.MspGlobalData.dependencies != null); // Make sure this toggle is visible based on whether or not we have the data.
			}
		}

		void SetRegionButtonCallback()
		{
			toggle.onValueChanged.AddListener((b) =>
			{
				float scale = InterfaceCanvas.Instance.canvas.scaleFactor;
				InterfaceCanvas.Instance.webViewWindow.CreateWebViewWindow(Main.MspGlobalData.region_base_url + '/' + TeamManager.CurrentTeam.name);
			});
	
		}
	}
}