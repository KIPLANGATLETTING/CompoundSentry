using Microsoft.AspNetCore.Identity;
using System;

namespace Compound_Sentry.Models
{
    public class AdminUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string? Role { get; set; } = "Admin";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;
        public int LoginAttempts { get; set; } = 0;
    }
}