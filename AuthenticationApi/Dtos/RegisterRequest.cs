using System.ComponentModel.DataAnnotations;

namespace AuthenticationApi.Dtos;

public class RegisterRequest
{
    [MinLength(Consts.UsernameMinLength, ErrorMessage = Consts.UsernameLengthValidationError)]
    public string? Username { get; set; }

    [EmailAddress(ErrorMessage = Consts.EmailValidationError)]
    public string? Email { get; set; }

    [RegularExpression(Consts.PasswordRegex, ErrorMessage = Consts.PasswordValidationError)]
    public string? Password { get; set; }
}
