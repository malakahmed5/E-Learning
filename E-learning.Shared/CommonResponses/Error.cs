namespace ELearning.Shared.CommonResponses
{
    public class Error
    {
        public string Code { get; }
        public string Description { get; }
        public ErrorType ErrorType { get; }

        private Error(string code, string description, ErrorType errorType)
        {
            Code = code;
            Description = description;
            ErrorType = errorType;
        }

        #region Factory Methods
        public static Error Failure(
            string code = "General.Failure",
            string description = "A general failure has occurred")
            => new(code, description, ErrorType.Failure);

        public static Error Validation(
            string code = "General.Validation",
            string description = "A validation error has occurred")
            => new(code, description, ErrorType.Validation);

        public static Error NotFound(
            string code = "General.NotFound",
            string description = "The requested resource was not found")
            => new(code, description, ErrorType.NotFound);

        public static Error UnAuthorized(
            string code = "General.UnAuthorized",
            string description = "You are not authorized to perform this action")
            => new(code, description, ErrorType.UnAuthorized);

        public static Error Forbidden(
            string code = "General.Forbidden",
            string description = "You don't have access to this resource")
            => new(code, description, ErrorType.Forbidden);

        public static Error InvalidCredentials(
            string code = "General.InvalidCredentials",
            string description = "The provided credentials are not valid")
            => new(code, description, ErrorType.InvalidCredentials);

        public static Error Conflict(
            string code = "General.Conflict",
            string description = "The resource already exists or conflicts with the current state")
            => new(code, description, ErrorType.Conflict);
        #endregion
    }
}
