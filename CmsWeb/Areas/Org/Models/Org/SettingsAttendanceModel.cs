using CmsData;
using CmsWeb.Code;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web.Mvc;
using UtilityExtensions;

namespace CmsWeb.Areas.Org.Models
{
    public class SettingsAttendanceModel
    {
        public CMSDataContext CurrentDatabase
        {
            get => _currentDatabase;
            set
            {
                _currentDatabase = value;
                _org = null;
            }
        }

        private Organization _org;
        public Organization Org
        {
            get
            {
                if (_org == null && Id > 0 && CurrentDatabase != null)
                {
                    _org = CurrentDatabase.LoadOrganizationById(Id);
                }
                return _org;
            }
        }

        public int Id { get; set; }

        public SettingsAttendanceModel()
        {
        }

        public SettingsAttendanceModel(int id, CMSDataContext dataContext)
        {
            _currentDatabase = dataContext;
            Id = id;
            this.CopyPropertiesFrom(Org);
        }

        public void Update()
        {
            if (!HasSchedules())
            {
                schedules = new List<ScheduleInfo>();
            }
            this.CopyPropertiesTo(Org);
            CurrentDatabase.SubmitChanges();
        }

        public SelectList AttendCreditList()
        {
            return CodeValueModel.AttendCredits().ToSelect();
        }
        public string NewMeetingTime
        {
            get
            {
                var sc = Org.OrgSchedules.FirstOrDefault(); // SCHED
                if (sc != null && sc.SchedTime != null)
                {
                    return sc.SchedTime.ToString2("t");
                }

                return "08:00 AM";
            }
        }
        public DateTime PrevMeetingDate
        {
            get
            {
                var sc = Org?.OrgSchedules.FirstOrDefault(); // SCHED
                if (sc != null && sc.SchedTime != null && sc.SchedDay < 9)
                {
                    var dt = Util.Now.Date.Sunday().AddDays(sc.SchedDay ?? 0);
                    if (dt >= Util.Now)
                    {
                        dt = dt.AddDays(-7);
                    }

                    return dt.Add(sc.SchedTime.Value.TimeOfDay);
                }
                return Util.Now.Date;
            }
        }
        public DateTime NextMeetingDate
        {
            get { return PrevMeetingDate.AddDays(7); }
        }
        public void UpdateSchedules(List<OrgSchedule> SchedulesToFix = null)
        {
            int IdCount = 1;
            var db = CurrentDatabase;
            var orgSchedules = SchedulesToFix ?? Org.OrgSchedules.ToList();

            for (int i = orgSchedules.Count - 1; i >= 0; i--)
            {
                var s = orgSchedules[i];
                if (!schedules.Any(ss => ss.Id == s.Id))
                {
                    foreach (var memtag in Org.MemberTags.Where(m => m.ScheduleId == s.Id))
                    {
                        memtag.ScheduleId = null;
                        db.SubmitChanges();
                    }
                    db.OrgSchedules.DeleteOnSubmit(s);
                    orgSchedules.Remove(s);
                }
            }
            db.SubmitChanges();
            foreach (var s in schedules.OrderBy(ss => ss.Id))
            {
                bool IsNew = true;
                if (s.Id == 0)
                {
                    s.Id = (orgSchedules.Count > 0) ? orgSchedules.Max(ss => ss.Id) + 1 : 1;
                }

                var schedule = orgSchedules.FirstOrDefault(ss => ss.Id == s.Id);
                if (schedule != null)
                {
                    IsNew = false;
                    db.OrgSchedules.DeleteOnSubmit(schedule);
                    orgSchedules.Remove(schedule);
                    db.SubmitChanges();
                }

                if (SchedulesToFix.IsNotNull())
                {
                    s.Id = IdCount;
                    IdCount++;
                }
                CreateScheduleObj(IsNew, s, ref orgSchedules);
            }
            db.SubmitChanges();
            if (SchedulesToFix.IsNull())
            {
                FixgSchedulesIds();
            }
        }
        private void CreateScheduleObj(bool IsNew, ScheduleInfo s, ref List<OrgSchedule> orgSchedules)
        {
            var schedule = new OrgSchedule
            {
                OrganizationId = Id,
                Id = s.Id,
                SchedDay = s.SchedDay.Value.ToInt(),
                SchedTime = s.Time.ToDate(),
                MeetingTime = s.SchedDay.ToDate(),
                AttendCreditId = s.AttendCredit.Value.ToInt()
            };
            if (IsNew)
            {
                schedule.NextMeetingDate = s.NextMeetingTime;
            }

            CurrentDatabase.OrgSchedules.InsertOnSubmit(schedule);
            orgSchedules.Add(schedule);
        }
        private void FixgSchedulesIds()
        {
            var Schedules = CurrentDatabase.OrgSchedules.Where(x => x.OrganizationId == Org.OrganizationId).ToList();
            UpdateSchedules(Schedules);
        }
        public SelectList SchedulesPrev()
        {
            var q = new SelectList(Schedules.OrderBy(cc => cc.Id), "ValuePrev", "Display");
            return q;
        }
        public SelectList SchedulesNext()
        {
            var q = new SelectList(Schedules.OrderBy(cc => cc.Id), "ValueNext", "Display");
            return q;
        }

        [Display(Description = @"
This is where you indicate the weekly schedule. You can have mutiple schedules.
The top one is the default that shows up on lists.
Schedules can be 'Every Meeting' for 100% credit or they can be 'One a Week' for 100% credit.
"), UIHint("Schedules")]
        public List<ScheduleInfo> Schedules
        {
            get
            {
                if (schedules == null && Id != 0 && CurrentDatabase != null)
                {
                    var q = from sc in CurrentDatabase.OrgSchedules
                            where sc.OrganizationId == Id
                            select sc;
                    var u = from s in q
                            orderby s.Id
                            select new ScheduleInfo(s);
                    schedules = u.ToList();
                }
                if (schedules == null)
                {
                    schedules = new List<ScheduleInfo>();
                }

                return schedules;
            }
            set
            {
                schedules = value ?? new List<ScheduleInfo>();
            }
        }
        private List<ScheduleInfo> schedules;
        private CMSDataContext _currentDatabase;

        public bool HasSchedules()
        {
            return schedules != null;
        }

        [Display(Name = "Does NOT meet weekly",
            Description = @"
**Check** this if the org does not meet weekly. 
Leave **unchecked** for weekly meetings.
")]
        public bool NotWeekly { get; set; }

        [Display(Name = "Send an attendance link to leaders at the start of each meeting",
            Description = @"
Allows Sub-Group leaders to receive a text message or email with
a link to the attendance roster page for the group.
")]
        public bool SendAttendanceLink { get; set; }

        [Display(Name = "Filter Attendance Roster By Subgroup",
            Description = @"
Allows Sub-Group leaders to be specified on the Sub-Group Members page. 
These leaders' attendance rosters will be filtered by Sub-Group in the Mobile App.
")]
        public bool AttendanceBySubGroups { get; set; }

        [Display(Name = "Allow Attendance Overlap",
            Description = @"
This allows persons to attend two different orgs that start at the same time.
e.g. a meeting that spans several hours vs another that is one hour.
")]
        public bool AllowAttendOverlap { get; set; }

        [Display(Name = "Offsite Trip",
            Description = @"
This causes any absents during the period of an offsite trip start and end dates 
to not be counted negatively for attendance purposes.
")]
        public bool Offsite { get; set; }
        
        [Display(Description = @"
Causes people who visited prior to this date to drop off the recent visitor list.
For when visitors promote to the next grade.
Also used to display the meeting dates on 'User Chooses Class' type registrations.
")]
        public DateTime? FirstMeetingDate { get; set; }

        [Display(Description = @"
Used to display the meeting dates on 'User Chooses Class' type registrations.
")]
        public DateTime? LastMeetingDate { get; set; }

        [Display(Name = "Rollsheet Guest Weeks",
            Description = @"
Default is 3 weeks.
Guests will drop off the rollsheet or checkin screen if they haven't visited in this number of weeks.
Some teachers prefer them to show up for a long time.
")]
        public int? RollSheetVisitorWks { get; set; }

        [Display(Description = @"
Default is 2 weeks.
Number of consecutive absents that causes person to show on Recent Absents report.
")]
        public int? ConsecutiveAbsentsThreshold { get; set; }
    }
}
