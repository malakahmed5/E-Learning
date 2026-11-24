using System.Security.Cryptography;
using System.Text;

namespace ELearning.Services.Security
{
    // Random codes that people type by hand: staff invite codes, activation codes, subscription codes.
    public static class SecureCode
    {
        public const int Length = 12;

        // Letters and digits that are easy to read. We leave out 0, O, 1, I and L
        // because people mix them up when they read a code from paper.
        private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        // Creates a new 12-character code, for example "K7QM2XRF9TPA".
        public static string Generate()
        {
            var characters = new char[Length];
            for (var index = 0; index < Length; index++)
            {
                // RandomNumberGenerator is the secure random source. System.Random must not be used
                // for codes, because its output can be predicted.
                var position = RandomNumberGenerator.GetInt32(Alphabet.Length);
                characters[index] = Alphabet[position];
            }
            return new string(characters);
        }

        // Shows a code in groups of four so it is easier to read: "K7QM-2XRF-9TPA".
        public static string Format(string code)
        {
            if (code.Length != Length)
                return code;

            return $"{code.Substring(0, 4)}-{code.Substring(4, 4)}-{code.Substring(8, 4)}";
        }

        // Cleans what the user typed: removes dashes and spaces and makes letters capital.
        public static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var builder = new StringBuilder();
            foreach (var character in input)
            {
                if (character == '-' || char.IsWhiteSpace(character))
                    continue;

                builder.Append(char.ToUpperInvariant(character));
            }
            return builder.ToString();
        }

        // One-way hash (SHA-256) written as 64 hex characters.
        // We store this instead of the real value, so a database leak does not reveal the secret.
        public static string Hash(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
