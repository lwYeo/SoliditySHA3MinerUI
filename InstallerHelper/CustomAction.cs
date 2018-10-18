using System;
using System.IO;
using System.Linq;
using Microsoft.Deployment.WindowsInstaller;

namespace InstallerHelper
{
    public class CustomActions
    {
        private static DirectoryInfo LocalAppParentDir => new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        public static DirectoryInfo[] LocalAppDirectories => new DirectoryInfo(Path.Combine(LocalAppParentDir.FullName, "lwyeo@github")).GetDirectories("SoliditySHA3MinerUI*");

        [CustomAction]
        public static ActionResult DeleteLocalAppDir(Session session)
        {
            try
            {
                var mainFeature = session.Features.FirstOrDefault();

                if (mainFeature == null || (mainFeature.CurrentState != InstallState.Local || mainFeature.RequestState != InstallState.Absent))
                    return ActionResult.Success;

                if (LocalAppDirectories.Any())
                    foreach (var localAppDirectory in LocalAppDirectories)
                        localAppDirectory.Delete(true);

                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                session.Log(ex.ToString());
                return ActionResult.Failure;
            }
        }
    }
}
