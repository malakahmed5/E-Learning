using ELearning.Shared.CommonResponses;

namespace ELearning.Services.Teaching
{
    // Every error the teacher-management services can return, in one place.
    // The code (first text) is stable: the website uses it to show an Arabic or English message.
    public static class TeachingErrors
    {
        // Centers
        public static Error CenterNotFound => Error.NotFound("Center.NotFound", "This center was not found.");
        public static Error CenterNameTaken => Error.Conflict("Center.NameTaken", "Another center already has this name.");
        public static Error CenterHasGroups => Error.Conflict("Center.HasGroups", "This center still has groups. Move or delete them first.");

        // Groups
        public static Error GroupNotFound => Error.NotFound("Group.NotFound", "This group was not found.");
        public static Error GroupNameTaken => Error.Conflict("Group.NameTaken", "Another group in the same grade already has this name.");
        public static Error GroupHasStudents => Error.Conflict("Group.HasStudents", "This group still has students. Move them first.");
        public static Error GroupInactive => Error.Conflict("Group.Inactive", "This group is turned off.");
        public static Error ScheduleInvalidTimes => Error.Validation("Group.ScheduleInvalidTimes", "A lesson must end after it starts.");
        public static Error ScheduleDuplicate => Error.Validation("Group.ScheduleDuplicate", "The same day and start time appear more than once.");

        // Students
        public static Error StudentNotFound => Error.NotFound("Student.NotFound", "This student was not found.");
        public static Error PhoneTaken => Error.Conflict("User.PhoneTaken", "This phone number is already registered with this teacher.");
        public static Error GroupGradeMismatch => Error.Validation("Student.GroupGradeMismatch", "A student can only move to a group of the same grade.");
        public static Error InvalidPhone => Error.Validation("User.InvalidPhone", "The phone number is not valid.");
        public static Error RegistrationFailed => Error.Failure("User.RegistrationFailed", "The person could not be added. Please try again.");

        // Devices
        public static Error NoCurrentTerm => Error.Conflict("Device.NoCurrentTerm", "There is no school term for today's date. Ask the platform team to add it.");
        public static Error DeviceLimitReached => Error.Conflict("Device.LimitReached", "This student has already changed device twice this term.");
        public static Error DeviceNotLinked => Error.Conflict("Device.NotLinked", "This student has no linked device, so there is nothing to unlock.");

        // Assistants
        public static Error AssistantNotFound => Error.NotFound("Assistant.NotFound", "This assistant was not found.");
    }
}
