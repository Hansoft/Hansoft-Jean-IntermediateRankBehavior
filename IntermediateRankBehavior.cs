using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using HPMSdk;
using Hansoft.Jean.Behavior;
using Hansoft.ObjectWrapper;

namespace Hansoft.Jean.Behavior.IntermediateRankBehavior
{
    public class IntermediateRankBehavior : AbstractBehavior
    {
        string title;

        string projectName;
        Project project;
        string find;
        EHPMReportViewType viewType;
        ProjectView projectView;
        string columnName;
        HPMProjectCustomColumnsColumn rankColumn;

        bool changeImpact = false;

        public IntermediateRankBehavior(XmlElement configuration)
            : base(configuration) 
        {
            projectName = GetParameter("HansoftProject");
            viewType = GetViewType(GetParameter("View"));
            columnName = GetParameter("ColumnName");
            find = GetParameter("Find");
            title = "IntermediateRankBehavior: " + configuration.InnerText;
        }

        public override string Title
        {
            get { return title; }
        }

        public override void Initialize()
        {
            project = HPMUtilities.FindProject(projectName); 
            if (project == null)
                throw new ArgumentException("Could not find project:" + projectName);
            if (viewType == EHPMReportViewType.AgileBacklog)
                projectView = project.ProductBacklog;
            else
                projectView = project.Schedule;

            rankColumn = projectView.GetCustomColumn(columnName);
            if (rankColumn == null)
                throw new ArgumentException("Could not find custom column:" + columnName);
            DoRenumber();
        }

        // TODO: Subject to refactoring
        private EHPMReportViewType GetViewType(string viewType)
        {
            switch (viewType)
            {
                case ("Agile"):
                    return EHPMReportViewType.AgileMainProject;
                case ("Scheduled"):
                    return EHPMReportViewType.ScheduleMainProject;
                case ("Bugs"):
                    return EHPMReportViewType.AllBugsInProject;
                case ("Backlog"):
                    return EHPMReportViewType.AgileBacklog;
                default:
                    throw new ArgumentException("Unsupported View Type: " + viewType);

            }
        }

        private class RankComparer : IComparer<Task>
        {
            string columnName;
            public string ColumnName
            {
                set { columnName = value; }
            }

            public int Compare(Task x, Task y)
            {
                double xDouble, yDouble;
                string xString = x.GetCustomColumnValue(columnName).ToString();
                string yString = y.GetCustomColumnValue(columnName).ToString();
                if (Double.TryParse(xString, out xDouble) && Double.TryParse(yString, out yDouble) && xDouble -yDouble < 0)
                    return -1;
                else
                    return 1;
            }
        }

        void DoRenumber()
        {
            List<Task> tasks = projectView.Find(find);
            RankComparer comparer = new RankComparer();
            comparer.ColumnName = columnName;
            tasks.Sort(comparer);
            int no = 1;
            foreach (Task task in tasks)
            {
                double dummy;
                if (Double.TryParse(task.GetCustomColumnValue(columnName).ToString(), out dummy))
                {
                    task.SetCustomColumnValue(rankColumn, no);
                    no += 1;
                }
            }
        }

        public override void OnBeginProcessBufferedEvents(EventArgs e)
        {
            changeImpact = false;
        }

        public override void OnEndProcessBufferedEvents(EventArgs e)
        {
            if (BufferedEvents && changeImpact)
                DoRenumber();
        }

        public override void OnTaskCreate(TaskCreateEventArgs e)
        {
        }

        public override void OnTaskDelete(TaskDeleteEventArgs e)
        {
        }

        public override void OnTaskMove(TaskMoveEventArgs e)
        {
        }

        public override void OnTaskChangeCustomColumnData(TaskChangeCustomColumnDataEventArgs e)
        {
            if (e.Data.m_ColumnHash == rankColumn.m_Hash)
            {
                if (!BufferedEvents)
                    DoRenumber();
                else
                    changeImpact = true;
            }
        }

    }
}
