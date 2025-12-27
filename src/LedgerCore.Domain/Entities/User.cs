using Microsoft.AspNetCore.Identity;
namespace LedgerCore.Domain.Entities
{
    public class User : IdentityUser<Guid>
    {
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsDeleted { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime LastLogin { get; private set; }
        public string? AvatarUrl { get; private set; }
        public ICollection<RefreshToken>RefreshTokens { get; private set; }
        public User() { }

        public User(string firstName, string lastName, string email, string phoneNumber)
        {
            Id = Guid.NewGuid();
            FirstName = firstName;
            LastName = lastName;
            Email = email;
            UserName = email;
            PhoneNumber = phoneNumber;
            IsDeleted = false;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;


        }
        public void BlockUser()
        {
            IsActive = false;
        }

        public void UnblockUser()
        {
            IsActive = true;
        }
        public void DeleteUser()
        {
            IsDeleted = true;
        }

        public void SetLastLoginNow()
        {
            LastLogin = DateTime.Now;
        }

        public void UpdateProfilePhotoUrl(string profilePhotoUrl)
        {
            this.AvatarUrl = profilePhotoUrl;
        }

        public bool IsEmailVerified() => EmailConfirmed;

        public void VerifyEmail()
        {
            EmailConfirmed = true;
        }

    }
}
