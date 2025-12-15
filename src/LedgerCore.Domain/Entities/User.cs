using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Mail;
namespace LedgerCore.Domain.Entities
{
    public class User
    {
        public Guid Id { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public UserRole Role { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsDeleted { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime LastLogin { get; private set; }
        public string? PhoneNumber { get; private set; }

        public string Email { get; private set; }
        public int FailedLoginAttempts { get; private set; }
        public DateTime? LockoutEnd { get; private set; }
        public string? AvatarUrl { get; private set; }

        public string PasswordHash { get; private set; }

        private User() { }

        public User(string firstName, string lastName, string email, string passwordHash, UserRole role)
        {
            Id = Guid.NewGuid();
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
            FailedLoginAttempts = 0;
            Role = role;
            FirstName = firstName;
            LastName = lastName;
            IsDeleted = false;
            if (!validateEmail(email))
            {
                throw new IncorrectEmailException(email);
            }
            Email = email;
            this.PasswordHash = passwordHash;
        }

        private bool validateEmail(string email)
        {
            try
            {
                var adress = new MailAddress(email);
                return adress.Address == email;
            }
            catch
            {
                return false;
            }
        }

        public void BlockUser()
        {
            IsActive = false;
        }

        public void UnblockUser()
        {
            IsActive = true;
        }

        public void SetLastLoginNow()
        {
            LastLogin = DateTime.UtcNow;
            FailedLoginAttempts = 0;
            LockoutEnd = null;
        }

        public void AddFailedLoginAttempt(int maxAttempts = 3, int lockoutDurationInMinutes = 15)
        {
            FailedLoginAttempts++;

            if (FailedLoginAttempts >= maxAttempts)
            {
                LockoutEnd = DateTime.UtcNow.AddMinutes(lockoutDurationInMinutes);
            }
        }

        public void DeleteUser()
        {
            IsDeleted = true;
        }
    }
}
