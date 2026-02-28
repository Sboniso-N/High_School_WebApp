using System.Web;
using System.Web.Optimization;


namespace Avonford_Secondary_School.App_Start
{
    public static class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
           
            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                "~/Scripts/jquery.validate*"
            ));

            BundleTable.EnableOptimizations = false;
        }
    }
}
