using BIF.ToyStore.Core.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace BIF.ToyStore.Core.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
    }
}
