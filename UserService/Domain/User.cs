
using System.ComponentModel.DataAnnotations;

namespace UserService.Domain;

public class User
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, StringLength(100)]
    public string Name { get; set; } = default!;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = default!;
}
