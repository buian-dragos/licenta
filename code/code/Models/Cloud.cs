using System;

namespace code.Models
{
    /// <summary>
    /// Enumerates the supported cloud types.
    /// </summary>
    public enum CloudType
    {
        Cumulonimbus,
        Cumulus,
        Stratus,
        Stratocumulus,
        Nimbostratus,
        Altostratus,
        Altocumulus,
        Cirrostratus,
        Cirrocumulus,
        Cirrus
    }

    public enum RenderingPreset
    {
        Fast,
        Quality
    }

    public enum CameraPosition
    {
        GroundLevel,
        CloudLevel,
        AboveClouds
    }

    public class Cloud
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public CloudType Type { get; set; }

        public double Altitude { get; set; }

        public double Temperature { get; set; }

        public double Pressure { get; set; }

        public double WindSpeed { get; set; }

        public double Humidity { get; set; }

        public RenderingPreset RenderingPreset { get; set; }
        
        public CameraPosition CameraPosition { get; set; }

        public DateTime CreatedAt { get; set; }
        
        public string? PreviewImagePath { get; set; }

        public string? StoragePath { get; set; } // New property
    }
}
