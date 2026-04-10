using System.ComponentModel.DataAnnotations;

namespace PlechPomoshchi.ViewModels;

public class CreateRequestVm
{
    [Required]
    public string Category { get; set; } = "Материальная";

    [Required(ErrorMessage = "Опишите проблему")]
    [MinLength(10, ErrorMessage = "Слишком коротко")]
    public string Description { get; set; } = "";
}
