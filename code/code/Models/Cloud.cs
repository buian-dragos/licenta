using System;

namespace code.Domain
{
    // Domain entity representing a cloud model
    public class Cloud
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public CloudType Type { get; set; } = CloudType.SingleScatter;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
    
    public enum CloudType
    {
        SingleScatter,
        MultipleScatter,
        VolumetricRender
    }
}
