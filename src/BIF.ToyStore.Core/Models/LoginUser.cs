using BIF.ToyStore.Core.Enums;

namespace BIF.ToyStore.Core.Models
{
    public class LoginUser
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public UserRole Role { get; set; }
    }
}
