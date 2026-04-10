using System.ComponentModel.DataAnnotations;

namespace PlechPomoshchi.ViewModels;

public class RegisterVm
{
    [Required(ErrorMessage = "Введите фамилию")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Введите имя")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Введите email")]
    [EmailAddress(ErrorMessage = "Некорректный email")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Введите пароль")]
    [MinLength(8, ErrorMessage = "Пароль минимум 8 символов")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Подтвердите пароль")]
    [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают")]
    public string ConfirmPassword { get; set; } = "";
}
