using System;

namespace ReklamaMaket.Core
{
    /// <summary>Foydalanuvchiga to'g'ridan-to'g'ri ko'rsatiladigan xato (masalan, "logo tanlanmagan").</summary>
    public sealed class UserException : Exception
    {
        public UserException(string message)
            : base(message)
        {
        }

        public UserException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
