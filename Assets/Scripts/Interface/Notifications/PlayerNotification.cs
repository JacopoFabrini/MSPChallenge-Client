﻿using UnityEngine.Events;

namespace MSP2050.Scripts
{
	public static class PlayerNotifications
	{
		public delegate void AddNotificationDelegate(NotificationData data);
		public static event AddNotificationDelegate OnAddNotification;

		public delegate void RemoveNotificationDelegate(string identifier);
		public static event RemoveNotificationDelegate OnRemoveNotification;

		private static void AddNotification(NotificationData notificationData)
		{
			if (OnAddNotification != null)
			{
				OnAddNotification.Invoke(notificationData);
			}
		}

		public static void RemoveNotification(string identifier)
		{
			if (OnRemoveNotification != null)
			{
				OnRemoveNotification.Invoke(identifier);
			}
		}

		public static void AddNotification(string identifier, string summary, string description, string buttonText = null, UnityAction onButtonClick = null)
		{
			NotificationData data = new NotificationData(identifier, summary, description)
			{
				buttonText = buttonText,
				onButtonPress = onButtonClick
			};
			AddNotification(data);
		}

		public static void AddApprovalActionRequiredNotification(Plan targetPlan)
		{
			NotificationData data = new NotificationData(string.Format("ApprovalAction.{0}", targetPlan.ID),
				"Approval Action Required",
				string.Format("Plan {0} requires an approval decision from your team", targetPlan.Name));
			data.buttonText = "Open Plan Monitor";
			data.onButtonPress = () => {
				if (Main.InEditMode || Main.EditingPlanDetailsContent)
				{
					DialogBoxManager.instance.NotificationWindow("Editing plan content", "Other plans cannot be viewed while editing a plan's content.", () => { }, "Dismiss");
				}
				else
				{
					InterfaceCanvas.Instance.menuBarPlansMonitor.toggle.isOn = true;
					PlanDetails.SelectPlan(targetPlan);
					PlansMonitor.instance.planDetails.TabSelect(PlanDetails.EPlanDetailsTab.Feedback);
					PlansMonitor.instance.plansMinMax.Maximize();
				}
			};
			AddNotification(data);
		}

		public static void RemoveApprovalActionRequiredNotification(Plan targetPlan)
		{
			RemoveNotification(string.Format("ApprovalAction.{0}", targetPlan.ID));
		}

		public static void AddPlanIssueNotification(Plan targetPlan)
		{
			NotificationData data = new NotificationData(string.Format("PlanIssue.{0}", targetPlan.ID),
				"Issues in Plan",
				string.Format("Plan \"{0}\" has issues which need to be resolved", targetPlan.Name))
			{
				buttonText = "View Plan",
				onButtonPress = () => {
					if (Main.InEditMode || Main.EditingPlanDetailsContent)
					{
						DialogBoxManager.instance.NotificationWindow("Editing plan content", "Other plans cannot be viewed while editing a plan's content.", () => { }, "Dismiss");
					}
					else
					{
						InterfaceCanvas.Instance.menuBarPlansMonitor.toggle.isOn = true;
						PlanDetails.SelectPlan(targetPlan);
						PlansMonitor.instance.planDetails.TabSelect(PlanDetails.EPlanDetailsTab.Issues);
						PlansMonitor.instance.plansMinMax.Maximize();
					}
				}
			};
			AddNotification(data);
		}

		public static void RemovePlanIssueNotification(Plan targetPlan)
		{
			RemoveNotification(string.Format("PlanIssue.{0}", targetPlan.ID));
		}
	}
}
