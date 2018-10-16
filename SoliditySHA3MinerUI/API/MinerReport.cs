using System.Collections.ObjectModel;

namespace SoliditySHA3MinerUI.API
{
    public class MinerReport
    {
        public Summary Summary { get; set; }

        public ObservableCollection<Dashboard> DashboardList { get; set; }

        public MinerReport()
        {
            Summary = new Summary();
            DashboardList = new ObservableCollection<Dashboard>();
        }
    }
}