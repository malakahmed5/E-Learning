namespace ELearning.Shared.CommonResponses
{
    public class Result
    {
        private readonly List<Error> _errors = [];

        public bool IsSuccess => _errors.Count == 0;
        public bool IsFailure => !IsSuccess;
        public IReadOnlyList<Error> Errors => _errors;

        protected Result() { }
        protected Result(Error error) => _errors.Add(error);
        protected Result(List<Error> errors) => _errors.AddRange(errors);

        #region Factory Methods
        public static Result Ok() => new();
        public static Result Fail(Error error) => new(error);
        public static Result Fail(List<Error> errors) => new(errors);
        #endregion

        public static implicit operator Result(Error error) => Fail(error);
        public static implicit operator Result(List<Error> errors) => Fail(errors);
    }

    public class Result<TValue> : Result
    {
        private readonly TValue _value;
        public TValue Value => _value;

        private Result(TValue value) : base() => _value = value;
        private Result(Error error) : base(error) => _value = default!;
        private Result(List<Error> errors) : base(errors) => _value = default!;

        #region Factory Methods
        public static Result<TValue> Ok(TValue value) => new(value);
        public static new Result<TValue> Fail(Error error) => new(error);
        public static new Result<TValue> Fail(List<Error> errors) => new(errors);
        #endregion

        public static implicit operator Result<TValue>(TValue value) => Ok(value);
        public static implicit operator Result<TValue>(Error error) => Fail(error);
        public static implicit operator Result<TValue>(List<Error> errors) => Fail(errors);
    }
}
