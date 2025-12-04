using LedgerCore.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace LedgerCore.Api.Dtos;

public class CreateAccountRequest
{
    [Required(ErrorMessage ="Nazwa jest wymagana!")]
    [MaxLength(100, ErrorMessage = "Nazwa nie może być dłuższa niż 100 znaków!")]
    public string Name { get; set; }

    [Required(ErrorMessage = "Kod jest wymagany.")]
    [StringLength(3,MinimumLength =3, ErrorMessage = "Kod konta nie może być dłuższy niż 3 znaki!")]
    public string Code { get; set; }

    [Required(ErrorMessage = "Typ konta jest wymagany.")]
    [Range(0,4,ErrorMessage = "Nieznany typ konta")]
    public AccountType Type { get; set; } 
}