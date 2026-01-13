using System;

namespace LenovoLegionToolkit.Lib.Settings;

public class UpdateCheckSettings() : AbstractSettings<UpdateCheckSettings.UpdateCheckSettingsStore>("update_check.json")
{
    public class UpdateCheckSettingsStore
    {
        public DateTime? LastUpdateCheckDateTime { get; set; }
        public UpdateCheckFrequency UpdateCheckFrequency { get; set; }
        
        /// <summary>
        /// Version that user chose to skip (won't be prompted again for this version)
        /// </summary>
        public string? SkippedVersion { get; set; }
        
        /// <summary>
        /// When user chose "Remind Me Later", don't show update prompt until this time
        /// </summary>
        public DateTime? RemindLaterDateTime { get; set; }
        
        /// <summary>
        /// How long to wait before reminding (in hours)
        /// </summary>
        public int RemindLaterHours { get; set; } = 24;
    }

    protected override UpdateCheckSettingsStore Default => new()
    {
        LastUpdateCheckDateTime = null,
        UpdateCheckFrequency = UpdateCheckFrequency.PerDay,
        SkippedVersion = null,
        RemindLaterDateTime = null,
        RemindLaterHours = 24
    };
}
