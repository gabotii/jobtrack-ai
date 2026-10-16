// Mirrors the API contracts (JobTrack.Application DTOs). Enum values are camelCase strings.

export type ApplicationStatus =
  | 'saved'
  | 'toApply'
  | 'applied'
  | 'screening'
  | 'interview'
  | 'offer'
  | 'accepted'
  | 'rejected'
  | 'withdrawn'

export type WorkMode = 'onSite' | 'hybrid' | 'remote'
export type ContractType =
  'fullTime' | 'partTime' | 'contract' | 'freelance' | 'internship' | 'temporary' | 'other'
export type ApplicationSource =
  'companyWebsite' | 'linkedIn' | 'jobBoard' | 'referral' | 'recruiter' | 'other'
export type CalendarEventType = 'interview' | 'call' | 'followUp' | 'reminder' | 'other'
export type CalendarEventStatus = 'scheduled' | 'completed' | 'cancelled'
export type ReminderPriority = 'low' | 'medium' | 'high'

/** ISO 8601 instant in UTC, e.g. "2026-10-14T08:00:00.000Z". */
export type Instant = string
/** Calendar date, "yyyy-MM-dd". */
export type IsoDate = string

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface User {
  id: string
  email: string
  fullName: string
  timeZone: string
  createdAt: Instant
}

export interface AuthResponse {
  accessToken: string
  accessTokenExpiresAt: Instant
  user: User
}

export interface Company {
  id: string
  name: string
  website: string | null
  industry: string | null
  location: string | null
  description: string | null
  contactName: string | null
  contactEmail: string | null
  contactPhone: string | null
  notes: string | null
  jobOfferCount: number
  activeApplicationCount: number
  createdAt: Instant
  updatedAt: Instant
}

export interface SaveCompanyRequest {
  name: string
  website: string | null
  industry: string | null
  location: string | null
  description: string | null
  contactName: string | null
  contactEmail: string | null
  contactPhone: string | null
  notes: string | null
}

export interface LinkedApplication {
  id: string
  status: ApplicationStatus
}

export interface JobOfferListItem {
  id: string
  companyId: string
  companyName: string
  title: string
  location: string | null
  workMode: WorkMode
  contractType: ContractType
  salaryMin: number | null
  salaryMax: number | null
  currency: string | null
  requiredSkills: string[]
  deadline: IsoDate | null
  savedAt: Instant
  application: LinkedApplication | null
}

export interface JobOffer extends JobOfferListItem {
  description: string | null
  url: string | null
  preferredSkills: string[]
  publishedOn: IsoDate | null
  notes: string | null
  createdAt: Instant
  updatedAt: Instant
}

export interface SaveJobOfferRequest {
  companyId: string
  title: string
  description: string | null
  url: string | null
  location: string | null
  workMode: WorkMode
  contractType: ContractType
  salaryMin: number | null
  salaryMax: number | null
  currency: string | null
  requiredSkills: string[]
  preferredSkills: string[]
  publishedOn: IsoDate | null
  deadline: IsoDate | null
  notes: string | null
}

export interface ApplicationSummary {
  id: string
  jobOfferId: string
  jobTitle: string
  companyId: string
  companyName: string
  location: string | null
  workMode: WorkMode
  status: ApplicationStatus
  statusChangedAt: Instant
  appliedOn: IsoDate | null
  nextAction: string | null
  followUpOn: IsoDate | null
  createdAt: Instant
  updatedAt: Instant
  allowedTransitions: ApplicationStatus[]
}

export interface ApplicationStatusHistoryEntry {
  id: string
  fromStatus: ApplicationStatus | null
  toStatus: ApplicationStatus
  changedAt: Instant
  note: string | null
}

export interface ApplicationDetail {
  summary: ApplicationSummary
  resumeId: string | null
  resumeFileName: string | null
  coverLetter: string | null
  source: ApplicationSource | null
  notes: string | null
  rejectionReason: string | null
  history: ApplicationStatusHistoryEntry[]
}

export interface CreateApplicationRequest {
  jobOfferId: string
  status: ApplicationStatus
  appliedOn: IsoDate | null
  resumeId: string | null
  coverLetter: string | null
  source: ApplicationSource | null
  notes: string | null
  nextAction: string | null
  followUpOn: IsoDate | null
}

export type UpdateApplicationRequest = Omit<CreateApplicationRequest, 'jobOfferId' | 'status'>

export interface ChangeStatusRequest {
  status: ApplicationStatus
  note: string | null
  rejectionReason: string | null
}

export interface CalendarEvent {
  id: string
  title: string
  type: CalendarEventType
  status: CalendarEventStatus
  startsAt: Instant
  endsAt: Instant
  timeZone: string
  location: string | null
  meetingUrl: string | null
  notes: string | null
  companyId: string | null
  companyName: string | null
  applicationId: string | null
  applicationJobTitle: string | null
  createdAt: Instant
  updatedAt: Instant
}

export interface SaveCalendarEventRequest {
  title: string
  type: CalendarEventType
  startsAt: Instant
  endsAt: Instant
  timeZone: string
  location: string | null
  meetingUrl: string | null
  notes: string | null
  companyId: string | null
  applicationId: string | null
}

export interface Reminder {
  id: string
  title: string
  description: string | null
  dueAt: Instant
  priority: ReminderPriority
  isCompleted: boolean
  completedAt: Instant | null
  applicationId: string | null
  applicationJobTitle: string | null
  companyName: string | null
  calendarEventId: string | null
  calendarEventTitle: string | null
  createdAt: Instant
  updatedAt: Instant
}

export interface SaveReminderRequest {
  title: string
  description: string | null
  dueAt: Instant
  priority: ReminderPriority
  applicationId: string | null
  calendarEventId: string | null
}

export interface DashboardTotals {
  totalApplications: number
  activeApplications: number
  upcomingInterviews: number
  savedOffers: number
  rejectedApplications: number
  pendingFollowUps: number
  overdueReminders: number
  companies: number
  jobOffers: number
}

export interface Dashboard {
  today: IsoDate
  totals: DashboardTotals
  applicationsPerWeek: { weekStart: IsoDate; count: number }[]
  statusDistribution: { status: ApplicationStatus; count: number }[]
  upcomingEvents: CalendarEvent[]
  followUps: ApplicationSummary[]
  pendingReminders: Reminder[]
  recentActivity: {
    id: string
    applicationId: string
    jobTitle: string
    companyName: string
    fromStatus: ApplicationStatus | null
    toStatus: ApplicationStatus
    changedAt: Instant
  }[]
}

export interface Resume {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
  characterCount: number
  analysisCount: number
  createdAt: Instant
}

export interface ResumeDetail {
  resume: Resume
  extractedText: string
}

export interface AnalysisProvider {
  id: string
  name: string
  available: boolean
  external: boolean
  model: string | null
  description: string
}

export interface ResumeAnalysisSummary {
  id: string
  resumeId: string
  resumeFileName: string
  jobOfferId: string | null
  jobTitle: string
  companyName: string
  provider: string
  model: string
  matchScore: number
  createdAt: Instant
}

export interface ResumeAnalysisResult {
  matchScore: number
  summary: string
  matchedSkills: { skill: string; resumeEvidence: string | null; offerEvidence: string | null }[]
  missingSkills: { skill: string; required: boolean; offerEvidence: string | null }[]
  relevantExperience: { description: string; resumeEvidence: string | null }[]
  missingKeywords: string[]
  strengths: string[]
  improvementAreas: string[]
  recommendations: string[]
}

export interface ResumeAnalysis {
  summary: ResumeAnalysisSummary
  promptVersion: string
  result: ResumeAnalysisResult
}
