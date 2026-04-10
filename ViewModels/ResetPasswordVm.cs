using System.ComponentModel.DataAnnotations;

namespace PlechPomoshchi.ViewModels;

public class ResetPasswordVm
{
    public string Token { get; set; } = "";

    [Required(ErrorMessage = "Введите пароль")]
    [MinLength(8, ErrorMessage = "Пароль минимум 8 символов")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Подтвердите пароль")]
    [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают")]
    public string ConfirmPassword { get; set; } = "";
}
