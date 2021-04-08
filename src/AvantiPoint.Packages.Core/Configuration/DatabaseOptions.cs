using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Core
{
    public class DatabaseOptions
    {
        public string Type { get; set; }

        [Required]
        public string ConnectionString { get; set; }
    }
}
