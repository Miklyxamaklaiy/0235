using System.ComponentModel.DataAnnotations;

namespace PlechPomoshchi.ViewModels;

public class ForgotPasswordVm
{
    [Required(ErrorMessage = "Введите email")]
    [EmailAddress(ErrorMessage = "Некорректный email")]
    public string Email { get; set; } = "";

    public string? GeneratedLink { get; set; }
}
