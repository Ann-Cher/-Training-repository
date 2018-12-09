﻿using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App
{
	public partial class SettingsManager
	{

		/// <summary>
		/// Apply all settings to XML.
		/// </summary>
		public bool ApplyAllSettingsToXML()
		{
			var padControls = MainForm.Current.PadControls;
			for (int i = 0; i < padControls.Length; i++)
			{
				// Get pad control with settings.
				var padControl = MainForm.Current.PadControls[i];
				var setting = padControl.GetSelectedSetting();
				// Skip if not selected.
				if (setting == null)
					continue;
				var padSetting = padControl.CloneCurrentPadSetting();
				// If setting doesn't exists then...
				if (!PadSettings.Items.Any(x => x.PadSettingChecksum == padSetting.PadSettingChecksum))
				{
					// Add setting to configuration.
					PadSettings.Items.Add(padSetting);
				}
				// If pad setting checksum changed then...
				if (setting.PadSettingChecksum != padSetting.PadSettingChecksum)
				{
					// Assign updated checksum.
					setting.PadSettingChecksum = padSetting.PadSettingChecksum;
					var ud = SettingsManager.GetDevice(setting.InstanceGuid);
					setting.Completion = UserSetting.GetCompletionPoints(padSetting, ud);
				}
			}
			CleanupPadSettings();
			return true;
		}

		/// <summary>
		/// Remove PAD settings, not attached to any device.
		/// </summary>
		public void CleanupPadSettings()
		{
			// Get all records used by Settings.
			var usedPadSettings = UserSettings.Items.Select(x => x.PadSettingChecksum).Distinct().ToList();
			// Get all records used by Summaries.
			var usedPadSettings2 = Summaries.Items.Select(x => x.PadSettingChecksum).Distinct().ToList();
			// Get all records used by Presets.
			var usedPadSettings3 = Presets.Items.Select(x => x.PadSettingChecksum).Distinct().ToList();
			// Combine all pad settings.
			usedPadSettings.AddRange(usedPadSettings2);
			usedPadSettings.AddRange(usedPadSettings3);
			// Get all stored padSettings.
			var allPadSettings = PadSettings.Items.Select(x => x.PadSettingChecksum).Distinct().ToArray();
			// Wipe all not used pad settings.
			var notUsed = allPadSettings.Except(usedPadSettings);
			foreach (var nu in notUsed)
			{
				var notUsedItems = PadSettings.Items.Where(x => x.PadSettingChecksum == nu).ToArray();
				PadSettings.Remove(notUsedItems);
			}
		}

		/// <summary>
		/// Insert missing pad settings and clean-up the list.
		/// </summary>
		/// <param name="list"></param>
		public void UpsertPadSettings(params PadSetting[] list)
		{
			foreach (var item in list)
			{
				// If pad setting was not found then...
				if (!PadSettings.Items.Any(x => x.PadSettingChecksum == item.PadSettingChecksum))
					// Add pad setting.
					PadSettings.Add(item);
			}
		}

		/// <summary>
		/// Insert missing settings.
		/// </summary>
		/// <param name="list"></param>
		public void UpsertSettings(params UserSetting[] list)
		{
			foreach (var item in list)
			{
				var old = UserSettings.Items.FirstOrDefault(x => x.SettingId == item.SettingId);
				if (old == null)
				{
					UserSettings.Add(item);
				}
				// If item was updated then...
				else if (item.DateUpdated > old.DateUpdated)
				{
					JocysCom.ClassLibrary.Runtime.Helper.CopyDataMembers(item, old);
				}
			}
		}

		public static bool ValidatePropertyNames(SettingsMapItem[] maps, out PropertyInfo[] propertiesToSet)
		{
			var availableNames = maps.Select(x => x.PropertyName);
			var properties = typeof(PadSetting).GetProperties();
			propertiesToSet = properties.Where(x => x.PropertyType == typeof(string) && x.Name != "ButtonBig").ToArray();
			var requiredNames = propertiesToSet.Select(x => x.Name);
			var missing = requiredNames.Except(availableNames);
			if (missing.Count() > 0)
			{
				var list = string.Join(", ", missing);
				MessageBox.Show("'PadSetting' class property names must match 'SettingName' class property names. Please make sure that these properties exists in 'SettingName' class:\r\n\r\n" + list);
				return false;
			}
			return true;
		}

		/// <summary>
		/// Get PadSetting from currently selected device.
		/// </summary>
		/// <param name="padIndex">Source pad index.</param>
		public PadSetting GetCurrentPadSetting(MapTo padIndex)
		{
			// Get settings related to PAD.
			var maps = SettingsMap.Where(x => x.MapTo == padIndex).ToArray();
			PropertyInfo[] properties;
			if (!ValidatePropertyNames(maps, out properties))
				return null;
			var ps = new PadSetting();
			foreach (var p in properties)
			{
				var map = maps.First(x => x.PropertyName == p.Name);
				var key = map.IniPath.Split('\\')[1];
				// Get setting value from the form.
				var v = GetSettingValue(map.Control);
				// If value is default then...
				if (v == map.DefaultValue as string)
					// Remove default value.
					v = null;
				// Set value onto padSetting.
				p.SetValue(ps, v ?? "", null);
			}
			ps.PadSettingChecksum = ps.CleanAndGetCheckSum();
			return ps;
		}

		public void LoadPadSettingAndCleanup(UserSetting setting, PadSetting ps, bool add = false)
		{
			// Link setting with pad setting.
			setting.PadSettingChecksum = ps.PadSettingChecksum;
			// Insert pad setting first, because it will be linked with the setting.
			UpsertPadSettings(ps);
			// Insert setting if not in the list.
			if (add)
				UserSettings.Add(setting);
			// Clean-up pad settings.
			Current.CleanupPadSettings();
		}

		/// <summary>
		/// Load PAD settings to form.
		/// </summary>
		/// <param name="padSetting">Settings to read.</param>
		/// <param name="padIndex">Destination pad index.</param>
		public void LoadPadSettingsIntoSelectedDevice(MapTo padIndex, PadSetting ps)
		{
			// Get pad control with settings.
			var padControl = MainForm.Current.PadControls[(int)padIndex - 1];
			// Get selected setting.
			var setting = padControl.GetSelectedSetting();
			// Return if nothing selected.
			if (setting == null)
				return;
			// If setting not supplied then use empty (clear settings).
			if (ps == null)
				ps = new PadSetting();
			LoadPadSettingAndCleanup(setting, ps);
			SyncFormFromPadSetting(padIndex, ps);
            RaiseSettingsChanged(null);
			loadCount++;
			var ev = ConfigLoaded;
			if (ev != null)
			{
				ev(this, new SettingEventArgs(typeof(PadSetting).Name, loadCount));
			}
		}

		public void SyncFormFromPadSetting(MapTo padIndex, PadSetting ps)
		{
			// Get setting maps for specified PAD Control.
			var maps = SettingsMap.Where(x => x.MapTo == padIndex).ToArray();
			PropertyInfo[] properties;
			if (!ValidatePropertyNames(maps, out properties))
				return;
			// Suspend form events (do not track setting changes on the form).
			SuspendEvents();
			foreach (var p in properties)
			{
				var map = maps.First(x => x.PropertyName == p.Name);
				var key = map.IniPath.Split('\\')[1];
				var v = (string)p.GetValue(ps, null) ?? "";
				// If value is not set then...
				if (string.IsNullOrEmpty(v))
					// Restore default value.
					v = string.Format("{0}", map.DefaultValue ?? "");
				LoadSetting(map.Control, key, v);
			}
			// Resume form events (track setting changes on the form).
			ResumeEvents();
		}

	}
}
