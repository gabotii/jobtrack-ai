namespace JobTrack.Domain.Enums;

/// <summary>Stages of a job application. The numeric order reflects the natural progression of the active pipeline.</summary>
public enum ApplicationStatus
{
    Saved = 0,
    ToApply = 1,
    Applied = 2,
    Screening = 3,
    Interview = 4,
    Offer = 5,
    Accepted = 6,
    Rejected = 7,
    Withdrawn = 8,
}

public enum WorkMode
{
    OnSite = 0,
    Hybrid = 1,
    Remote = 2,
}

public enum ContractType
{
    FullTime = 0,
    PartTime = 1,
    Contract = 2,
    Freelance = 3,
    Internship = 4,
    Temporary = 5,
    Other = 6,
}

/// <summary>Where the user found the offer or how the application started.</summary>
public enum ApplicationSource
{
    CompanyWebsite = 0,
    LinkedIn = 1,
    JobBoard = 2,
    Referral = 3,
    Recruiter = 4,
    Other = 5,
}

public enum CalendarEventType
{
    Interview = 0,
    Call = 1,
    FollowUp = 2,
    Reminder = 3,
    Other = 4,
}

public enum CalendarEventStatus
{
    Scheduled = 0,
    Completed = 1,
    Cancelled = 2,
}

public enum ReminderPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
}
