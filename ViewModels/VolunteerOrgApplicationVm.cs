using System.ComponentModel.DataAnnotations;

namespace PlechPomoshchi.ViewModels;

public class VolunteerOrgApplicationVm
{
    [Required(ErrorMessage = "Введите название организации")]
    public string OrgName { get; set; } = "";

    [Required(ErrorMessage = "Выберите категорию")]
    public string Category { get; set; } = "";

    [Required(ErrorMessage = "Введите адрес")]
    public string Address { get; set; } = "";

    [Required(ErrorMessage = "Введите контакт")]
    public string Contact { get; set; } = "";

    [Required(ErrorMessage = "Введите комментарий")]
    [MinLength(10, ErrorMessage = "Слишком коротко")]
    public string Message { get; set; } = "";
}
