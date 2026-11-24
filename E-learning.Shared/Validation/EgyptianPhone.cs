using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ELearning.Shared.Validation
{
    // Egyptian mobile numbers. We always store them in one form: 11 digits starting with 01 (example: 01012345678).
    public static class EgyptianPhone
    {
        // Turns what the user typed into the stored form.
        // Accepts spaces and dashes, and the country code written as +20 or 0020.
        // Returns false when the text is not a valid Egyptian mobile number.
        public static bool TryNormalize(string? input, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Refuse text that has anything other than digits and the usual phone punctuation.
            // Without this check, "01012345678; some text" would be accepted as a phone number.
            if (!HasOnlyPhoneCharacters(input))
                return false;

            var digits = KeepDigitsOnly(input);

            // 00201012345678 -> 201012345678
            if (digits.StartsWith("0020"))
                digits = digits.Substring(2);

            // 201012345678 -> 01012345678
            if (digits.StartsWith("20") && digits.Length == 12)
                digits = "0" + digits.Substring(2);

            if (!IsValidStoredForm(digits))
                return false;

            normalized = digits;
            return true;
        }

        // Hides the middle of a number: 01012345678 -> 010****5678.
        // Used when an assistant looks at a parent's number.
        public static string Mask(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 8)
                return "****";

            var start = phoneNumber.Substring(0, 3);
            var end = phoneNumber.Substring(phoneNumber.Length - 4);
            return start + "****" + end;
        }

        // Allowed: the digits 0-9, spaces, dashes, and a plus sign (for +20).
        private static bool HasOnlyPhoneCharacters(string input)
        {
            foreach (var character in input.Trim())
            {
                var isDigit = character >= '0' && character <= '9';
                var isPunctuation = character == ' ' || character == '-' || character == '+';

                if (!isDigit && !isPunctuation)
                    return false;
            }
            return true;
        }

        private static string KeepDigitsOnly(string input)
        {
            var builder = new StringBuilder();
            foreach (var character in input)
            {
                // Only the western digits 0-9. Arabic-Indic digits are rejected on purpose.
                if (character >= '0' && character <= '9')
                    builder.Append(character);
            }
            return builder.ToString();
        }

        private static bool IsValidStoredForm(string digits)
        {
            if (digits.Length != 11 || !digits.StartsWith("01"))
                return false;

            // The third digit is the mobile network: 0 Vodafone, 1 Etisalat, 2 Orange, 5 WE.
            var network = digits[2];
            return network == '0' || network == '1' || network == '2' || network == '5';
        }
    }

    // Put [EgyptianPhoneNumber] on a request property to reject invalid numbers before the service runs.
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EgyptianPhoneNumberAttribute : ValidationAttribute
    {
        public EgyptianPhoneNumberAttribute()
        {
            ErrorMessage = "Phone number must be a valid Egyptian mobile number.";
        }

        public override bool IsValid(object? value)
        {
            // Empty values are handled by [Required], not here.
            if (value is null)
                return true;

            return value is string text && EgyptianPhone.TryNormalize(text, out _);
        }
    }
}
