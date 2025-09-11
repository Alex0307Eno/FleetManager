using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Cars.Models

{
    public class DispatchLink
    {
        public int ParentDispatchId { get; set; }    // 母單
        public int ChildDispatchId { get; set; }     // 子單
        public Dispatch ParentDispatch { get; set; } // 導覽屬性
        public Dispatch ChildDispatch { get; set; }  // 導覽屬性

    }
}
