using System.Security.Cryptography;
using System.Text;

namespace ELearning.Services.Security
{
    // Time-based one-time passwords: the 6-digit codes of apps like Google Authenticator.
    // This follows the public standard RFC 6238 (SHA-1, 30-second steps, 6 digits),
    // which is what authenticator apps expect.
    public static class Totp
    {
        private const int StepSeconds = 30;
        private const int Digits = 6;
        private const int SecretSizeBytes = 20;
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        // Creates a new random secret. The user adds it to the authenticator app once.
        public static string GenerateSecret()
        {
            var bytes = RandomNumberGenerator.GetBytes(SecretSizeBytes);
            return ToBase32(bytes);
        }

        // The link that authenticator apps understand. The frontend draws it as a QR code.
        public static string BuildUri(string issuer, string accountName, string secret)
        {
            var label = Uri.EscapeDataString($"{issuer}:{accountName}");
            var escapedIssuer = Uri.EscapeDataString(issuer);
            return $"otpauth://totp/{label}?secret={secret}&issuer={escapedIssuer}&digits={Digits}&period={StepSeconds}";
        }

        // Time is counted in 30-second steps since 1970. Each step has its own code.
        public static long GetStep(DateTimeOffset time)
        {
            return time.ToUnixTimeSeconds() / StepSeconds;
        }

        // The 6-digit code for one time step.
        public static string GetCode(string secret, long step)
        {
            var key = FromBase32(secret);

            // The step number as 8 bytes, biggest byte first (required by the standard).
            var stepBytes = BitConverter.GetBytes(step);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(stepBytes);

            var hash = HMACSHA1.HashData(key, stepBytes);

            // "Dynamic truncation" from the standard: the last 4 bits of the hash say where to read 4 bytes.
            var offset = hash[hash.Length - 1] & 0x0F;
            var number = ((hash[offset] & 0x7F) << 24)
                       | (hash[offset + 1] << 16)
                       | (hash[offset + 2] << 8)
                       | hash[offset + 3];

            var code = number % 1_000_000;
            return code.ToString().PadLeft(Digits, '0');
        }

        // Checks a code the user typed. Returns the time step it belongs to, or null when it is wrong.
        // We accept the current step and one step before and after,
        // because the clock of the phone and the clock of the server are never exactly equal.
        public static long? FindMatchingStep(string secret, string code, DateTimeOffset now)
        {
            var currentStep = GetStep(now);

            for (var step = currentStep - 1; step <= currentStep + 1; step++)
            {
                var expected = GetCode(secret, step);
                if (FixedTimeEquals(expected, code))
                    return step;
            }
            return null;
        }

        // Compares two texts in a way that always takes the same time,
        // so an attacker cannot learn digits by measuring how fast we answer.
        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.UTF8.GetBytes(left);
            var rightBytes = Encoding.UTF8.GetBytes(right);
            return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }

        // Base32 is the text form authenticator apps use for secrets: every 5 bits become one character.
        public static string ToBase32(byte[] data)
        {
            var builder = new StringBuilder();
            var buffer = 0;
            var bitsInBuffer = 0;

            foreach (var value in data)
            {
                buffer = (buffer << 8) | value;
                bitsInBuffer += 8;

                while (bitsInBuffer >= 5)
                {
                    var index = (buffer >> (bitsInBuffer - 5)) & 0x1F;
                    builder.Append(Base32Alphabet[index]);
                    bitsInBuffer -= 5;
                }
            }

            if (bitsInBuffer > 0)
            {
                var index = (buffer << (5 - bitsInBuffer)) & 0x1F;
                builder.Append(Base32Alphabet[index]);
            }
            return builder.ToString();
        }

        public static byte[] FromBase32(string text)
        {
            var bytes = new List<byte>();
            var buffer = 0;
            var bitsInBuffer = 0;

            foreach (var character in text.ToUpperInvariant())
            {
                var index = Base32Alphabet.IndexOf(character);
                if (index < 0)
                    throw new FormatException("The two-factor secret is not valid Base32 text.");

                buffer = (buffer << 5) | index;
                bitsInBuffer += 5;

                if (bitsInBuffer >= 8)
                {
                    bytes.Add((byte)((buffer >> (bitsInBuffer - 8)) & 0xFF));
                    bitsInBuffer -= 8;
                }
            }
            return bytes.ToArray();
        }
    }
}
