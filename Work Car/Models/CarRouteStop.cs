using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class CarRouteStop
    {
        [Key]
        public int StopId { get; set; }
        public int ApplyId { get; set; }       
        public int OrderNo { get; set; }       
        public string Place { get; set; }      
        public string Address { get; set; }    
        public decimal? Lat { get; set; }      
        public decimal? Lng { get; set; }      


        [ForeignKey(nameof(ApplyId))]
        public CarApply? Apply { get; set; }
    }
}
